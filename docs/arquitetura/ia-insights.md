# IA: insights sobre os resultados do dashboard

**Funcionalidade escolhida** (dentre as opções do enunciado, item "Plus"): *integração com IA para gerar insights sobre os
resultados exibidos nos dashboards, fornecendo análises mais detalhadas e sugestões de melhorias para a liderança da
empresa.*

## Por que essa funcionalidade

O dashboard (`GET /reports/summary`) já entrega funil, KPIs, sparkline e impacto por orientação — mas são números crus.
Quem decide é o líder, e o líder não tem tempo de interpretar quinze indicadores toda semana. A IA lê o mesmo resumo que
o dashboard mostra e devolve, em português, o que um analista júnior escreveria numa manhã: o que está bem, o que está
mal e o que fazer a respeito — com a fonte (os mesmos números) sempre visível ao lado. É a diferença entre "ROI
consolidado: 36,73%" e "o ROI está positivo, puxado por um projeto específico, e duas orientações estão no vermelho —
revise-as".

## Modelo

**`gemini-3.1-flash-lite`** (Google Gemini API, `generateContent`), configurável via `Gemini:Model` — nenhum nome de
modelo está fixo no código, porque os modelos gratuitos mudam de nome e de disponibilidade com frequência (em setembro
de 2026, por exemplo, `gemini-2.5-flash` e `gemini-2.5-flash-lite` já não aceitavam contas novas). O Flash-Lite foi
escolhido por ser o de menor custo em tokens entre os disponíveis na conta usada no projeto — cada geração de insight
usa cerca de 1,5 mil tokens (prompt + resposta), então o modelo mais barato é o que sobra mais cota gratuita para a
demonstração.

## Entrada e saída

**Entrada** — `POST /reports/insights` (só **LIDER**), corpo opcional:

```json
{ "period": "ALL", "division": "LOGISTICA", "guidelineId": null, "refresh": false }
```

O servidor monta o **mesmo resumo do dashboard** (`ReportCalculator`, o mesmo cálculo usado no `/reports/summary`) para
os filtros pedidos, minimiza esse resumo (ver guardrails) e envia ao Gemini com um `responseSchema` — a API do Gemini
é instruída a devolver **exatamente** esse formato, então a resposta chega ao servidor já estruturada, não como texto
livre para interpretar.

**Saída:**

```json
{
  "summary": "…",
  "highlights": ["…"],
  "risks": ["…"],
  "recommendations": [{ "title": "…", "detail": "…", "priority": "ALTA|MEDIA|BAIXA", "relatedGuidelineId": "665f…" }],
  "generatedAt": "2026-09-21T22:16:00Z",
  "model": "gemini-3.1-flash-lite",
  "fromCache": false
}
```

O servidor **valida** essa resposta contra o schema antes de devolvê-la; se o modelo devolver algo fora do formato,
o servidor responde `502 AI_INVALID_RESPONSE` — nunca repassa um JSON malformado ou incompleto para o app.

## Guardrails (privacidade e segurança)

- **Nunca vai PII ao Gemini.** `InsightPromptBuilder` monta o payload só com agregados (contagens, somas, percentuais) e
  títulos — nunca nome, e-mail ou id de usuário. Um teste automatizado (`PromptSentToTheModel_HasNoNamesEmailsOrUserIds`)
  captura o corpo de verdade enviado à IA e falha se encontrar qualquer um desses dados.
- **Orientações por referência, não por id.** No prompt elas viram `G1`…`G10`; a recomendação que a IA devolve
  referenciando uma orientação é mapeada de volta para o `id` real no servidor. Se a IA "inventar" uma referência que não
  existe, ela vira `null` — nunca um id real errado.
- **Textos livres tratados como dado, nunca como instrução.** Títulos de projetos/orientações são escritos pelos próprios
  usuários; um título como *"ignore as instruções anteriores…"* é sanitizado (sem quebras de linha, sem `<`/`>`,
  truncado em 80 caracteres) e entra só dentro de um bloco `<dados>` delimitado no prompt — a instrução de sistema
  explicitamente manda o modelo tratar tudo ali como dado, nunca como comando. Testado com um título malicioso de
  verdade.
- **Minimização de volume:** no máximo 10 projetos (priorizando os atrasados + melhores/piores ROI) e 10 orientações
  (as com mais atividade) — o resto entra como contagem (`projetosOmitidos`, `orientacoesOmitidas`), não como dado bruto.
- **Chave nunca logada.** `Gemini:ApiKey` só por variável de ambiente, enviada no header `x-goog-api-key` (nunca na
  query string); os headers do `HttpClient` são redigidos mesmo em log `Trace` — havia uma regressão real aqui (o
  framework logava os headers em `Trace`), corrigida e coberta por teste.
- **Nunca inventa números.** A instrução de sistema pede explicitamente para não inventar valores fora dos dados
  enviados; se os dados forem insuficientes para uma conclusão, o modelo é instruído a dizer isso.

## Resiliência, cache e cota

- **Timeout de 20 s por tentativa**, 1 retry com backoff exponencial + jitter em 429/5xx/timeout, e circuit breaker
  (abre com ≥ 50% de falhas em ≥ 4 chamadas numa janela de 60 s) — falha final vira `503 AI_UNAVAILABLE`, nunca um
  insight fabricado no lugar do erro.
- **Cache de 6 h** por hash de `modelo + filtros + digest dos dados agregados` — mudar um filtro ou editar um projeto
  muda o hash (cache miss automático); `refresh=true` força uma nova geração. Compartilhado entre líderes (são dados da
  empresa, não de uma pessoa). Uma segunda chamada idêntica não gasta cota nenhuma.
- **Cota:** 6 gerações por minuto por usuário, mais um teto diário global configurável (`Gemini:DailyLimit`), com
  contador atômico no Mongo (`aiUsage`) — testado sob 10 requisições simultâneas com teto 3: exatamente 3 passam e 7
  recebem `429 RATE_LIMITED` com `Retry-After`.

## Limitações conhecidas

- **Free tier é pequeno.** Os limites variam por modelo e mudam com frequência (a conta usada aqui chegou a ficar sem
  créditos pré-pagos durante os testes, retornando `402` da própria Google) — o cache e a cota diária existem
  justamente para esticar essa cota.
- **Só português, só executivo.** O prompt fixa idioma e tom; não há opção de outro idioma ou de um resumo mais técnico.
- **A IA pode errar mesmo dentro do schema.** Por isso o app sempre mostra o selo "Gerado por IA — valide antes de
  decidir" — o insight é uma leitura, não uma decisão automática.
- **Sem contexto histórico entre gerações.** Cada chamada é independente; a IA não "lembra" de recomendações
  anteriores nem do que o líder já decidiu fazer a respeito.
- **Nome do modelo é frágil.** Modelos gratuitos são descontinuados ou fechados para novas contas com pouco aviso;
  por isso `Gemini:Model` é configuração, não constante no código.

## Exemplo real de resposta

Capturado numa chamada de verdade à API do Gemini (ambiente de desenvolvimento, dados do seed), em ~2,7 s:

```json
{
  "summary": "O portfólio de inovação apresenta um ROI consolidado de 36,73%, com um lucro líquido de 90 mil reais gerado a partir de um investimento total de 245 mil reais. Atualmente, contamos com três projetos aprovados, sendo que um já foi concluído com sucesso e dois seguem em execução ou planejamento. A eficiência operacional na logística destaca-se como o principal motor de retorno financeiro para o grupo.",
  "highlights": [
    "ROI consolidado de 36,73% com retorno total de 335 mil reais.",
    "Redução de custos operacionais atingiu 53 mil reais.",
    "Ganho médio de produtividade de 8,25% nos projetos ativos.",
    "Projeto de indicadores de pontualidade (G1) concluído com ROI de 158,33%.",
    "Nenhum projeto apresenta atraso no cronograma atual."
  ],
  "risks": [
    "ROI negativo de 68,75% no projeto de sustentabilidade (G2).",
    "Projeto de experiência do passageiro (G3) ainda não apresenta retorno financeiro.",
    "Uma orientação estratégica permanece sem qualquer atividade registrada.",
    "Alta concentração de valor em um único projeto de alta performance."
  ],
  "recommendations": [
    {
      "title": "Revisão do projeto de sustentabilidade",
      "detail": "Avaliar a viabilidade e os custos do projeto de coleta seletiva para reverter o ROI negativo atual.",
      "priority": "ALTA",
      "relatedGuidelineId": "6ab198fd9d8506d25408ff61"
    },
    {
      "title": "Aceleração da experiência do passageiro",
      "detail": "Monitorar o planejamento do check-in por QR Code para garantir que o investimento de 45 mil reais gere retorno.",
      "priority": "MEDIA",
      "relatedGuidelineId": "6ab198fd9d8506d25408ff60"
    },
    {
      "title": "Expansão da eficiência logística",
      "detail": "Utilizar o sucesso do projeto de indicadores de pontualidade como modelo para novas iniciativas de eficiência.",
      "priority": "MEDIA",
      "relatedGuidelineId": "6ab198fd9d8506d25408ff5f"
    }
  ],
  "generatedAt": "2026-09-21T22:15:08.47Z",
  "model": "gemini-3.1-flash-lite",
  "fromCache": false
}
```

Note que nenhum nome, e-mail ou id de usuário aparece em nenhum campo — só orientações e projetos (por título e id de
orientação/projeto, nunca id de pessoa).

---

Ver também: `.specs/features/sprint2/spec.md` (R2-07), `.specs/features/sprint2/design.md` (§9) e
[`docs/api/ENDPOINTS.md`](../api/ENDPOINTS.md#post-apiv1reportsinsights) para o contrato completo.
