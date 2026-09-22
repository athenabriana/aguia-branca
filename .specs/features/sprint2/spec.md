# Spec — Sprint 2 INOVAGAB (Backend + Integração + IA)

Backend real (C# / .NET 8 Web API + MongoDB) que substitui o Firebase (Auth + Firestore) do app da Sprint 1, com autenticação JWT por perfil, APIs REST para todos os recursos do app, relatórios para o dashboard e **insights gerados por IA (Google Gemini)** sobre os resultados.

Requisitos com IDs rastreáveis (`R2-XX`) e critérios de aceitação testáveis. Continuação de `.specs/features/sprint1/spec.md` (`R-XX`): as regras de negócio da Sprint 1 **permanecem**; o que muda é *onde* executam (do cliente/Firestore para o servidor).

**Fonte do enunciado:** `.specs/features/sprint2/atividade-descricao-fiap.md`
**Design:** `.specs/features/sprint2/design.md`
**Tasks:** `.specs/features/sprint2/tasks.md`
**Deadline:** a confirmar (não consta no enunciado da Sprint 2)

---

## Escopo e decisões de partida

| Item | Decisão |
|---|---|
| Backend | C# · .NET 8 · ASP.NET Core Web API · ASP.NET Identity + JWT · EF Core |
| Banco | MongoDB (via provider `MongoDB.EntityFrameworkCore`), replica set para transações |
| IA (Plus) | Google Gemini API — **insights sobre os resultados do dashboard** para a liderança |
| App | Mesmo app Android da Sprint 1; camada `Firestore*Repository` substituída por `Remote*Repository` (REST). Domínio e UI preservados |
| Migração | Firestore → MongoDB (schema + dados) com ferramenta própria idempotente (R2-09) |
| Servidor é a fonte da verdade | Pontos, badges, transições de status, automações e cálculos do dashboard passam a rodar **no backend** |

### Cobertura do enunciado

| Exigência do enunciado | Requisito |
|---|---|
| Login de 3 perfis (operador, gestor, líder); JWT, criptografia; roles por operação | R2-01 |
| Orientações: líder CRUD, demais consultam; **registro histórico (id, data, categoria, campanha)** | R2-02 |
| Ideias: operador CRUD próprias; gestor consulta/prioriza/aprova; vínculo com estratégia | R2-03 |
| Projetos: gestor CRUD + progresso + resultados; líder consulta; vínculo com estratégia | R2-04 |
| Dashboard: resumo geral + retorno por estratégia ou projeto (ROI, lucro, prazo, investimento, produtividade) | R2-06 |
| IA: insights sobre dashboards com análises e sugestões para a liderança | R2-07 |
| App integrado às APIs, sem mocks | R2-08 |
| Banco NoSQL (migração do Firebase) | R2-09 |
| Camadas/módulos, README, apresentação, endpoints, explicação do modelo de IA | R2-10, R2-11 |

### Achados da análise do app (Sprint 1 *as-built*) que afetam a Sprint 2

| # | Achado | Consequência |
|---|---|---|
| A1 | `BadgeEvaluator` é importado em `FirestoreIdeasRepository` mas **nunca é invocado**; `users.badges` nunca é gravado | R-06.4 não estava efetivamente entregue. Backend passa a avaliar/persistir badges (R2-05.3) |
| A2 | `topByPointsThisMonth` ordena por **pontos totais**, não pelos do mês (variável `startOfMonth` não é usada) | Backend mantém razão de pontos (`pointEvents`) e calcula ranking mensal real (R2-05.4) |
| A3 | Projeto tem campos além da spec S1: `priorityScore`, `reporterId/Name`, `responsibleId/Name` | Entram no contrato de projeto (R2-04.1) |
| A4 | Aprovação bloqueia auto-aprovação (`PermissionDenied`) | Regra vira 403 `SELF_APPROVAL_FORBIDDEN` no servidor (R2-03.8) |
| A5 | `ApproveIdeaUseCase`/`CompleteProjectUseCase` escrevem direto no Firestore | Viram regras do servidor; no app viram chamadas REST finas (R2-08) |
| A6 | Credenciais reais do app: `*@aguiabranca.com` / `aguiabranca123` (a spec S1 cita `inovagab.com`) | Seed da Sprint 2 usa as credenciais *as-built* (R2-01.8) |
| A7 | Operador hoje lê todos os projetos via Firestore (`allow read: if isAuth()`); o stepper da ideia precisa do estágio do projeto | Servidor restringe operador (R-04.4); resposta da ideia embute `linkedProject` resumido (R2-03.9) |
| A8 | Dashboard é calculado no cliente (`DashboardComputer.kt`) a partir de listas completas | Cálculo migra para o servidor (R2-06); app só renderiza |

---

## R2-01 — Autenticação e autorização

**História:** Como usuário (operador, gestor ou líder), quero entrar com e-mail e senha e ter acesso apenas ao que meu perfil permite, com segurança robusta.

**Requisitos:**
- R2-01.1 — `POST /api/v1/auth/login` valida credenciais via ASP.NET Identity e devolve `accessToken` (JWT), `refreshToken`, `expiresIn` e o perfil do usuário
- R2-01.2 — JWT assinado (HS256, chave ≥ 256 bits vinda de configuração/segredo), com `iss`, `aud`, `exp`, `jti` e claims `sub`, `email`, `name`, `role`, `division`. Validação estrita de issuer, audience, assinatura e expiração (skew ≤ 30 s)
- R2-01.3 — Access token de **30 min**; refresh token opaco de **7 dias**, armazenado **somente como hash** (SHA-256), com **rotação** a cada uso e **detecção de reuso** (reuso de token já rotacionado revoga toda a família)
- R2-01.4 — `POST /auth/refresh`, `POST /auth/logout` (revoga refresh), `GET /auth/me`
- R2-01.5 — Roles: `OPERADOR`, `GESTOR`, `LIDER`. Toda rota (exceto login/refresh/health) exige autenticação; autorização por **policy** conforme matriz de permissões (abaixo)
- R2-01.6 — Senhas com hash PBKDF2 do Identity (nunca reversível, nunca logadas/retornadas); política mínima: 8+ caracteres; **lockout** após 5 falhas (15 min)
- R2-01.7 — Mensagem de credencial inválida idêntica para "usuário inexistente" e "senha errada" (não vaza existência de conta)
- R2-01.8 — **Seed** idempotente (Development/`Seed:Enabled`): `lider@aguiabranca.com` (LIDER, CORPORATIVO), `gestor@aguiabranca.com` (GESTOR, LOGISTICA), `operador@aguiabranca.com` (OPERADOR, LOGISTICA), senha `aguiabranca123`, mais dados de exemplo (orientações, ideias, projetos)
- R2-01.9 — Rate limiting em `/auth/*` (10 req/min por IP)

**Matriz de permissões:**

| Recurso / ação | OPERADOR | GESTOR | LIDER |
|---|:-:|:-:|:-:|
| Orientações — listar/consultar/histórico | ✅ | ✅ | ✅ |
| Orientações — criar/editar/excluir | ❌ | ❌ | ✅ |
| Ideias — criar | ✅ | ✅ ¹ | ❌ |
| Ideias — ler | só as próprias | todas | todas |
| Ideias — editar/excluir | só própria + `SUBMETIDA` | ❌ | ❌ |
| Ideias — ICE / aprovar / rejeitar | ❌ | ✅ (não a própria) | ❌ |
| Projetos — ler (+ histórico) | ❌ | ✅ | ✅ |
| Projetos — criar/editar/excluir | ❌ | ✅ | ❌ |
| Ranking mensal / perfil próprio | ✅ | ✅ | ✅ |
| Relatórios / dashboard | ❌ | ❌ | ✅ |
| Insights de IA | ❌ | ❌ | ✅ |

¹ *Compatível com o as-built (regras Firestore permitem qualquer autenticado criar ideia; a UI já bloqueia auto-aprovação). Ver ponto aberto OP-3.*

**Aceitação:**
- Login com credenciais demo devolve JWT válido; credenciais inválidas → `401 INVALID_CREDENTIALS`
- Requisição sem token → `401`; token de outro perfil sem permissão → `403`; token expirado → `401`
- Refresh devolve novo par de tokens e invalida o anterior; reapresentar o antigo → `401` e família revogada
- 5 logins errados seguidos → `429` com `Retry-After`
- Nenhum endpoint devolve `passwordHash`/`securityStamp`

---

## R2-02 — Orientações estratégicas (+ histórico)

**Histórias:**
- Como líder, quero criar/editar/remover orientações e consultar o **histórico** das estratégias.
- Como gestor/operador, quero consultar as orientações vigentes.

**Requisitos:**
- R2-02.1 — CRUD em `/api/v1/guidelines`; escrita só `LIDER`; leitura todos os perfis autenticados
- R2-02.2 — Campos: `title` (3–120), `description` (até 2000), `pillar` (`DIRECIONAMENTO | IDEIAS | PROJETOS | MENSURACAO`), **`campaign`** (opcional, até 80), `authorId/authorName` (do token), `createdAt`, `updatedAt`
- R2-02.3 — Listagem ordenada por `updatedAt` desc (comportamento S1: editar reposiciona no topo)
- R2-02.4 — **Registro histórico:** toda criação, edição e exclusão grava entrada imutável em `guidelineHistory` com `id`, **data** (`occurredAt`), **categoria** (= `pillar`), **campanha**, ação (`CREATED | UPDATED | DELETED`), snapshot dos campos e autor da mudança
- R2-02.5 — `GET /guidelines/history` com filtros `guidelineId`, `category`, `campaign`, `from`, `to` (paginado, mais recente primeiro). O histórico **sobrevive à exclusão** da orientação
- R2-02.6 — Exclusão é hard-delete da orientação (R-02.5); ideias/projetos vinculados mantêm o `guidelineId` órfão; respostas de ideia/projeto trazem `guidelineTitle = null` quando órfã (app mostra "Orientação removida")

**Aceitação:**
- Operador/gestor recebem `403` em POST/PUT/DELETE de orientação
- Editar orientação gera 1 entrada `UPDATED` com a data e a campanha vigentes
- Excluir orientação com ideias vinculadas → `204`; ideias continuam listáveis; histórico mostra `DELETED`
- `GET /guidelines/history?campaign=X` só retorna entradas da campanha X

---

## R2-03 — Ideias

**Histórias:** as de R-03 (Sprint 1), agora sobre API.

**Requisitos:**
- R2-03.1 — `POST /ideas`: `title` (3–120), `description` (até 2000), `category` (2–40, normalizada trim + 1ª maiúscula), `division`, `guidelineId` (opcional). `authorId/authorName/status` **definidos pelo servidor** (status inicial `SUBMETIDA`); nunca aceitos do corpo
- R2-03.2 — `guidelineId` informado deve existir → senão `422 GUIDELINE_NOT_FOUND`
- R2-03.3 — Leitura: operador só vê as próprias (`GET /ideas?scope=mine`; detalhe alheio → `404`); gestor/líder veem todas. Filtros: `scope` (`mine | curation | all`), `status`, `guidelineId`, `division`, paginação
- R2-03.4 — `scope=curation` devolve `SUBMETIDA` + `EM_ANALISE`, ordenadas por ICE score desc; sem ICE ao fim (R-03.6)
- R2-03.5 — `PUT /ideas/{id}` e `DELETE /ideas/{id}`: somente o autor e somente enquanto `SUBMETIDA`; caso contrário `409 IDEA_NOT_EDITABLE`
- R2-03.6 — `PUT /ideas/{id}/ice` (GESTOR): `impact`, `confidence`, `ease` inteiros 1–10; `score` calculado no servidor; transição automática `SUBMETIDA → EM_ANALISE` (R-03.12). Permitido enquanto `SUBMETIDA`/`EM_ANALISE`
- R2-03.7 — `POST /ideas/{id}/reject` (GESTOR): `comment` obrigatório (1–1000) → `REJEITADA`, `reviewerId`, `reviewedAt`. Sem pontos
- R2-03.8 — `POST /ideas/{id}/approve` (GESTOR): **automação 1 (R-03.8)** em uma transação: ideia → `APROVADA` + projeto rascunho (`PLANEJAMENTO`, `title = "PROJ: " + título`, herda `division`/`guidelineId`, `originatingIdeaId`, `creatorManagerId`, `priorityScore = ice.score`, `reporter*` = autor da ideia, `responsible*` = gestor) + 1ª entrada de histórico "Criado automaticamente a partir da ideia: {título}" + **+50 pts** ao autor + avaliação de badges. Autor não pode aprovar a própria ideia (`403 SELF_APPROVAL_FORBIDDEN`). **Idempotente:** reaprovar devolve o mesmo `projectId` sem duplicar projeto nem pontos (índice único parcial em `projects.originatingIdeaId`)
- R2-03.9 — Resposta de ideia inclui `guidelineTitle` (null se órfã) e `linkedProject { id, stage, updatedAt }` (null se não há) — permite ao operador montar o stepper (R-03.9) sem acessar `/projects`
- R2-03.10 — Criação credita **+10 pts** (+5 se `guidelineId`); exclusão em `SUBMETIDA` reverte −10/−15 com clamp em 0 (R2-05)
- R2-03.11 — Ideias em `APROVADA`/`IMPLEMENTADA`/`REJEITADA` não aceitam novo ICE, aprovação ou rejeição → `409 IDEA_INVALID_STATE` (exceto reaprovar `APROVADA`, idempotente)

**Aceitação:**
- Operador A não consegue ler/editar ideia do operador B (`404`)
- Editar ideia após ICE salvo → `409 IDEA_NOT_EDITABLE`
- ICE com valor 0, 11 ou não inteiro → `400 VALIDATION_ERROR`
- Aprovar 2× a mesma ideia → 1 projeto, +50 pts uma única vez
- Gestor autor da ideia tenta aprovar → `403 SELF_APPROVAL_FORBIDDEN`
- Rejeitar sem comentário → `400`
- Corpo de POST com `status: "APROVADA"` ou `authorId` alheio é ignorado/rejeitado (sem mass assignment)

---

## R2-04 — Projetos e iniciativas

**Requisitos:**
- R2-04.1 — Campos: `title`, `description`, `stage` (`PLANEJAMENTO | EM_EXECUCAO | CONCLUIDO | CANCELADO`), `statusText`, `investment` (≥ 0), `targetDate` (prazo), `financialReturn` (≥ 0), `productivityGain` (% ≥ 0), `costReduction` (≥ 0), `division`, `guidelineId`, `originatingIdeaId`, `priorityScore`, `reporterId/Name`, `responsibleId/Name`, `creatorManagerId`, `createdAt`, `updatedAt`
- R2-04.2 — `GET /projects`, `GET /projects/{id}`, `GET /projects/{id}/updates`: GESTOR e LIDER. Filtros: `stage`, `division`, `guidelineId`, paginação. Operador → `403`
- R2-04.3 — `POST /projects`, `PUT /projects/{id}`, `DELETE /projects/{id}`: apenas GESTOR (qualquer gestor edita qualquer projeto, D12). `guidelineId` validado; vínculo com a estratégia vigente
- R2-04.4 — **Histórico de atualizações:** cada criação/edição grava `projectUpdates` com autor, nota opcional e **diff calculado no servidor** (`field`, `from`, `to`), mais recente primeiro. Criação → nota "Projeto criado" (ou a da automação R2-03.8)
- R2-04.5 — **Automação 2 (R-03.13)**: quando o `stage` passa para `CONCLUIDO`, na mesma transação da edição: ideia de origem → `IMPLEMENTADA`, **+200 pts** ao autor, badges avaliadas. Idempotente (não recredita)
- R2-04.6 — Edição concorrente: `PUT` aceita `version` opcional; se enviado e divergente → `409 CONCURRENCY_CONFLICT`
- R2-04.7 — Excluir projeto remove seu histórico; a ideia de origem permanece no status atual

**Aceitação:**
- Líder recebe `403` em POST/PUT/DELETE; operador recebe `403` em qualquer rota de projetos
- Editar `investment` 100 → 120 gera entrada com `changes = [{field:"investment", from:100, to:120}]`
- `PUT` com `stage=CONCLUIDO` em projeto originado de ideia → ideia `IMPLEMENTADA`, autor +200; repetir `PUT` não recredita
- `PUT` sem mudança real de campos ainda registra nota, com `changes` vazio (comportamento S1)

---

## R2-05 — Gamificação no servidor

**Requisitos:**
- R2-05.1 — Regras de pontos de R-06 executadas **somente no servidor**: +10 ideia; +5 vínculo estratégico; +50 aprovação; +200 implementação; −10/−15 exclusão. Toda mudança grava evento imutável em `pointEvents` (`userId`, `delta`, `reason`, `refId`, `createdAt`) na mesma transação da regra
- R2-05.2 — `users.points` nunca negativo (clamp em 0, R-06.7); o `delta` gravado é o **efetivo** após o clamp
- R2-05.3 — **Badges persistidas:** após cada evento relevante o servidor executa a avaliação (`Primeira Ideia`, `Estrategista`, `Inovador do Mês`, `Impacto Real`, `Visionário` — mesmas regras de R-06.4) e grava as novas em `users.badges`, sem duplicar
- R2-05.4 — `GET /users/ranking?limit=5`: top **do mês corrente** (fuso `America/Sao_Paulo`), somente `OPERADOR`, calculado pela soma dos `pointEvents` do mês; desempate por pontos totais e nome. Retorna `id`, `name`, `monthPoints`
- R2-05.5 — `GET /users?role=` (GESTOR/LIDER) para seleção de responsável; `GET /auth/me` traz `points` e `badges` atualizados

**Aceitação:**
- Cadastrar ideia com orientação → +15; sem → +10; excluir em `SUBMETIDA` → −15/−10 sem ficar < 0
- 5 ideias no mesmo mês → badge "Inovador do Mês" uma única vez (re-cumprir não duplica)
- 3 ideias aprovadas em 3 orientações distintas → "Visionário"
- Ranking do mês ignora pontos de meses anteriores

---

## R2-06 — Relatórios / Dashboard (Líder)

**História:** Como líder, quero um resumo estruturado com resultados e retornos por estratégia ou projeto, para exibir em gráficos.

**Requisitos:** (semântica idêntica a R-05 / `DashboardComputer.kt` — porte para C# com testes de paridade)
- R2-06.1 — `GET /reports/summary?period=&division=` (LIDER): `funnel` (5 estágios), `kpis` (ROI consolidado %, lucro líquido, investimento total, projetos ativos, ganho médio de produtividade, redução de custo total, **projetos atrasados**), `sparkline` (6 meses fixos, sensível a `division`, imune a `period`), `guidelineImpacts[]`, `projects[]` ordenados por ROI desc
- R2-06.2 — `period`: `THIS_MONTH | LAST_QUARTER | THIS_YEAR | ALL` (padrão `ALL`); `division` opcional. Filtros afetam funil, KPIs, impacto por orientação e lista
- R2-06.3 — ROI = (Σ retorno − Σ investimento) / Σ investimento × 100; Σ investimento = 0 → `null` (app mostra "—"); mês com investimento 0 → ponto `null` na sparkline
- R2-06.4 — **Retorno por estratégia:** `GET /reports/guidelines` (lista) e `GET /reports/guidelines/{id}` (ideias, projetos, investimento, retorno, lucro, ROI da orientação)
- R2-06.5 — **Retorno por projeto:** `GET /reports/projects/{id}` (investimento, retorno, lucro, ROI, produtividade, redução de custo, `targetDate`, `daysToDeadline`, `overdue`, estágio)
- R2-06.6 — Respostas contêm apenas dados agregados/resumidos prontos para gráfico; valores monetários em número (BRL, sem arredondar), **percentuais com 2 casas** (arredondamento comercial; a ordenação usa o valor exato). A formatação visual é do app
- R2-06.8 — **Decisões de paridade com o app** (herdadas do `DashboardComputer.kt` de propósito): período filtra ideias por `createdAt` e projetos por `updatedAt`, janela `[início, agora]`; `LAST_QUARTER` é uma **janela móvel de 3 meses** (não o trimestre-calendário); ganho médio de produtividade só considera projetos com ganho > 0; "ativos" = só `EM_EXECUCAO`. **Diferenças intencionais:** meses/dias seguem o fuso do relatório (`Reports:TimeZone`), não o do aparelho; "atrasado" = projeto aberto (não `CONCLUIDO`/`CANCELADO`) cujo **dia** do prazo já passou (no próprio dia do prazo ainda não está atrasado); `daysToDeadline` é `null` para projeto concluído/cancelado ou sem prazo; empates de ordenação resolvidos por título e id (resposta determinística)
- R2-06.7 — O app deixa de calcular o dashboard localmente (`DashboardComputer.kt` removido); mantém UI, filtros, modo apresentação e drill-down

**Aceitação:**
- Filtro `division=LOGISTICA` reduz funil, KPIs, impacto e lista; sparkline reage à divisão mas não ao período
- Σ investimento = 0 → `roiPercent = null`
- Conjuntos de dados de referência (**calculados à mão** a partir das regras do `DashboardComputer.kt` — o app nunca teve teste unitário do dashboard) produzem **exatamente** os números esperados no servidor
- Operador/gestor → `403` em `/reports/**`
- Editar retorno de um projeto e recarregar o dashboard atualiza os KPIs em < 2 s (rede local)

---

## R2-07 — IA: insights sobre os dashboards (Plus)

**Funcionalidade escolhida:** *integração com IA para gerar insights sobre os resultados exibidos nos dashboards, fornecendo análises mais detalhadas e sugestões de melhorias para a liderança.*

**Requisitos:**
- R2-07.1 — `POST /reports/insights` (LIDER) recebe `{ period, division?, guidelineId? }`, monta o **mesmo resumo do R2-06** e o envia à **Google Gemini API** (`generateContent`), com modelo configurável (`Gemini:Model`; API gratuita)
- R2-07.2 — Saída **estruturada** (JSON schema): `summary` (parágrafo), `highlights[]`, `risks[]`, `recommendations[]` (`title`, `detail`, `priority ALTA|MEDIA|BAIXA`, `relatedGuidelineId?`), mais `generatedAt`, `model`, `fromCache`. Resposta do modelo é validada no servidor; inválida → `502 AI_INVALID_RESPONSE`
- R2-07.3 — Idioma pt-BR; tom executivo; análises **baseadas apenas nos dados enviados** (instrução explícita para não inventar números); aviso "Gerado por IA — valide antes de decidir" exibido no app
- R2-07.4 — **Privacidade:** somente dados agregados + títulos truncados (80 chars, máx. 10 projetos e 10 orientações). Nunca nomes, e-mails ou IDs de usuário. Textos livres tratados como dados (delimitados no prompt) — mitigação de prompt injection
- R2-07.5 — **Resiliência:** timeout de 20 s **por tentativa** (`Gemini:TimeoutSeconds`), 1 retry com backoff em 429/5xx/timeout e circuit breaker; falha → `503 AI_UNAVAILABLE` (o app mostra erro e "Tentar novamente"; **nunca** exibe insight falso)
- R2-07.6 — **Cache + cota:** resultado cacheado 6 h por hash (`modelo`+`period`+`division`+`guidelineId`+digest dos dados), compartilhado entre líderes (são dados da empresa), com opção `refresh=true`; rate limit por usuário (6/min, 429 `RATE_LIMITED` + `Retry-After`) e **teto diário atômico** `Gemini:DailyLimit` (coleção `aiUsage`, 1 documento por dia no fuso do relatório; cache hit não consome cota; tentativas que falham no Gemini consomem, pois também gastam a cota do provedor)
- R2-07.9 — **Prompt:** orientações vão como referência curta (`G1`…`G10`), nunca por id; a recomendação devolve `relatedGuidelineRef` que o servidor mapeia para o `relatedGuidelineId` real (referência inventada pelo modelo vira `null`). Com `guidelineId` no pedido, o resumo cobre só aquela orientação. Projetos enviados: os atrasados (até 4) + melhores/piores ROI até 10; orientações: as 10 com mais atividade (`orientacoesOmitidas`/`projetosOmitidos` informam o corte). O log registra só contagem de tokens, modelo, status e latência (nunca conteúdo nem chave; os headers do `HttpClient` são redigidos mesmo em `Trace`)
- R2-07.7 — Chave `Gemini:ApiKey` só por variável de ambiente/secret; enviada em header `x-goog-api-key`; nunca logada
- R2-07.8 — App: card "✨ Insights da IA" no dashboard (botão gerar, loading, seções Destaques/Riscos/Recomendações, erro com retry, selo de IA e data de geração); respeita os filtros ativos
- R2-07.10 — **[Adicionado após a entrega da M09, a pedido do usuário — não fazia parte do levantamento inicial]** O card de insights é o **primeiro item** do dashboard. O modo apresentação ganha uma visão de **stories** (Instagram-like) para os insights: a tela inicial da apresentação tem o mesmo botão de gerar (mesma rota `POST /reports/insights`, sem alteração de contrato); com o resultado disponível — recém-gerado ou **o último salvo no servidor** (`refresh=false` já entrega o cache válido, sem custo de IA) —, o líder entra num reel com uma página por seção (resumo, destaques, riscos, cada recomendação), barra de progresso segmentada, avanço automático por tempo, navegação manual (toque nas laterais) e opção de gerar um novo sem sair da apresentação

**Aceitação:**
- Com chave válida, insights são retornados em ≤ 20 s e passam na validação de schema
- Segunda chamada com mesmos filtros/dados → `fromCache = true` sem nova chamada ao Gemini
- Gemini indisponível/cota excedida → `503 AI_UNAVAILABLE` com mensagem amigável; app não quebra
- Payload enviado ao Gemini (capturado em teste) não contém nome/e-mail/id de usuário
- Operador/gestor → `403`
- Reabrir a apresentação depois de já ter gerado um insight mostra "Ver insights"/"Gerar novo" com a data do último gerado, sem chamar o Gemini de novo

---

## R2-08 — Integração do app com o backend (sem mocks)

**Requisitos:**
- R2-08.1 — Interfaces `core/domain/*Repository` **mantidas**; implementações `Firestore*Repository` substituídas por `Remote*Repository` (Retrofit/OkHttp + kotlinx.serialization). UI e ViewModels só mudam onde necessário
- R2-08.2 — Auth: `SessionManager` usa `/auth/login|refresh|logout|me`; tokens em armazenamento protegido (DataStore + chave do Android Keystore); interceptor injeta `Authorization: Bearer`; `Authenticator` faz refresh transparente em `401`; falha de refresh → limpa sessão e volta ao Login (auth guard existente)
- R2-08.3 — Reatividade: `snapshotsAsFlow` substituído por **polling enquanto coletado** (15 s) + invalidação imediata após cada escrita (a tela vê a própria mudança sem esperar o ciclo). R-05.8 ("tempo real") passa a "atualização automática ≤ 15 s"
- R2-08.4 — `ProblemDetails` → `DomainError` (`401→NotAuthenticated`, `403→PermissionDenied`, `404→NotFound`, `400/422→ValidationFailed`, `409→ConflictingState`, `429`/timeout/IO→`NetworkUnavailable`/mensagem específica)
- R2-08.5 — `ApproveIdeaUseCase` e `CompleteProjectUseCase` deixam de escrever no banco: aprovação vira `POST /ideas/{id}/approve`; conclusão é efeito do `PUT /projects/{id}` (servidor). Analytics do app mantidos
- R2-08.6 — Dashboard usa `ReportsRepository` (`/reports/**`); ranking usa `/users/ranking`; perfil exibe badges reais do servidor
- R2-08.7 — URL da API por build: debug → `http://10.0.2.2:<porta>` (cleartext só em debug via `network_security_config`); release → HTTPS configurável (`local.properties`/CI)
- R2-08.8 — Remoção de `firebase-auth` e `firebase-firestore`, `firestore.rules`, `firebase.json`, índices e `FirestoreHelpers`. **Mantidos** Analytics e Crashlytics (fora do escopo da migração — ver OP-4)
- R2-08.9 — DTOs de rede `@Serializable` com mappers `Dto ↔ Domain` (datas ISO-8601 UTC ↔ `Long`); `MapperTest` atualizado

**Aceitação:**
- Nenhuma referência a `FirebaseFirestore`/`FirebaseAuth` no código do app
- Fluxos completos executados no emulador contra o backend real: login (3 perfis) → orientação → ideia → ICE → aprovação → projeto → conclusão → dashboard → insights
- Token expirado durante uso → refresh silencioso; refresh inválido → tela de Login
- App sem rede exibe erro tratado (sem crash) e recupera ao reconectar

---

## R2-09 — Migração Firebase → MongoDB

**Requisitos:**
- R2-09.1 — **Modelo:** coleções Mongo equivalentes às do Firestore (mapeamento abaixo); subcoleção `projects/{id}/updates` vira coleção `projectUpdates`
- R2-09.2 — **Ferramenta** `FirestoreMigrator` (console .NET): lê do Firestore (service account), transforma e grava no Mongo. Suporta `--dry-run`, é **idempotente** (upsert por `legacyId`) e emite **relatório de conciliação** (contagem por coleção origem × destino, órfãos, erros)
- R2-09.3 — IDs: `legacyId` guarda o ID Firestore; novos `_id` são `ObjectId`; **todas as referências** (`authorId`, `guidelineId`, `originatingIdeaId`, `creatorManagerId`, `reviewerId`, `reporterId`, `responsibleId`, `updates.authorId`) são remapeadas via tabela de IDs
- R2-09.4 — `Timestamp` → `DateTime` UTC; enums validados (valor desconhecido → relatório, não aborta)
- R2-09.5 — **Usuários/senhas:** hashes do Firebase Auth (scrypt modificado) **não são migrados**; usuários são recriados no Identity com senha temporária de seed e e-mail preservado. Documentado como limitação intencional
- R2-09.6 — **Pontos:** saldo migrado como `pointEvents` de abertura (`reason=MIGRATION`); badges recalculadas pelo avaliador do servidor
- R2-09.7 — Índices e restrições únicas criados por *initializer* no startup (provider EF Mongo não gerencia índices/migrations)

**Mapeamento Firestore → Mongo:**

| Firestore | MongoDB | Observação |
|---|---|---|
| `users/{uid}` (+ Firebase Auth) | `users` (Identity) | senha recriada; `points`/`badges` preservados |
| `strategicGuidelines/{id}` | `guidelines` (+ `guidelineHistory` inicial `CREATED`) | `campaign` = null |
| `ideas/{id}` | `ideas` | `ice` embutido |
| `projects/{id}` | `projects` | + `version` |
| `projects/{id}/updates/{uid}` | `projectUpdates` | `projectId` remapeado |
| — | `pointEvents`, `refreshTokens`, `aiInsights`, `guidelineHistory` | novas |

**Aceitação:**
- Dry-run não escreve; execução real seguida de segunda execução não duplica nada
- Conciliação: contagens origem = destino (± itens reportados como inválidos)
- Nenhuma referência aponta para ID inexistente após migração (ou é listada no relatório)

---

## R2-10 — Qualidade, segurança, observabilidade e documentação

- R2-10.1 — **Arquitetura em camadas** (Api / Application / Domain / Infrastructure) com organização por feature (design §3); domínio sem dependência de ASP.NET/EF/Mongo
- R2-10.2 — **Erros padronizados** em `ProblemDetails` com `code`, `errors[]`, `traceId`; middleware global; sem stack trace no cliente
- R2-10.3 — **Validação** de entrada (FluentValidation) e DTOs de request explícitos (sem mass assignment)
- R2-10.4 — **Logs estruturados** (Serilog) com `CorrelationId`, `UserId`, rota; nunca senha/JWT/refresh/API key
- R2-10.5 — **Swagger/OpenAPI** com esquema Bearer; `/health`, `/health/ready`, `/health/live` (checa Mongo)
- R2-10.6 — **Segurança de API:** HTTPS/HSTS em produção, CORS restrito, limite de payload, rate limiting, headers de segurança, segredos fora do repositório, validação de configuração no startup
- R2-10.7 — **Testes:** unitários (domínio, handlers, validators, cálculo de relatórios, avaliador de badges), **matriz de autorização** (401/403/2xx por endpoint × perfil), integração com `WebApplicationFactory` + MongoDB real (Testcontainers). Meta: Domain + Application ≥ 80%; regras críticas (pontos, badges, aprovação, conclusão, ROI, autorização) 100%
- R2-10.8 — **Execução:** `docker compose up` sobe Mongo (replica set) + API; `dotnet run` funciona com Mongo local/Atlas; README com passo a passo, variáveis de ambiente e usuários demo
- R2-10.9 — Estrutura versionada em `/backend` (solution) e `/mobile` (app), migrações e seed reproduzíveis

---

## R2-11 — Entregáveis

1. `.zip` do **backend** (camadas claras + `README.md` de execução)
2. `.zip` do **app** (projeto completo integrado + **APK** release)
3. **Apresentação** (PDF/PPT): nomes e RMs, **diagrama de arquitetura do backend**, **especificação dos endpoints** (rota, método, payload, resposta), **modelo de IA e funcionalidade escolhida**
4. Documento de endpoints gerado do OpenAPI (`docs/api/ENDPOINTS.md`) para apoiar a apresentação

---

## Contrato de API (`/api/v1`)

Erros: `application/problem+json` — `{ type, title, status, detail, instance, code, errors:[{code,message,field?}], traceId }`.
Datas ISO-8601 UTC. IDs `string` (ObjectId). Listas paginadas: `?page=1&pageSize=50` (máx. 200) → `{ items, page, pageSize, totalItems, totalPages }`.

| Método | Rota | Perfis | Descrição | Sucesso |
|---|---|---|---|---|
| POST | `/auth/login` | público | Login | 200 |
| POST | `/auth/refresh` | público | Rotaciona tokens | 200 |
| POST | `/auth/logout` | autenticado | Revoga refresh | 204 |
| GET | `/auth/me` | autenticado | Perfil, pontos, badges | 200 |
| GET | `/users` | GESTOR, LIDER | Lista por `role` | 200 |
| GET | `/users/ranking` | autenticado | Top do mês (operadores) | 200 |
| GET | `/guidelines` | autenticado | Lista | 200 |
| GET | `/guidelines/{id}` | autenticado | Detalhe | 200 |
| POST | `/guidelines` | LIDER | Cria | 201 |
| PUT | `/guidelines/{id}` | LIDER | Edita | 200 |
| DELETE | `/guidelines/{id}` | LIDER | Remove | 204 |
| GET | `/guidelines/history` | autenticado | Histórico (id, data, categoria, campanha) | 200 |
| GET | `/ideas` | autenticado* | Lista (`scope`, filtros) | 200 |
| GET | `/ideas/{id}` | autenticado* | Detalhe (+`linkedProject`) | 200 |
| POST | `/ideas` | OPERADOR, GESTOR | Cria (+pontos) | 201 |
| PUT | `/ideas/{id}` | autor | Edita (`SUBMETIDA`) | 200 |
| DELETE | `/ideas/{id}` | autor | Exclui (`SUBMETIDA`, −pontos) | 204 |
| PUT | `/ideas/{id}/ice` | GESTOR | Salva ICE | 200 |
| POST | `/ideas/{id}/approve` | GESTOR | Aprova → projeto rascunho | 200 |
| POST | `/ideas/{id}/reject` | GESTOR | Rejeita (comentário) | 200 |
| GET | `/projects` | GESTOR, LIDER | Lista | 200 |
| GET | `/projects/{id}` | GESTOR, LIDER | Detalhe | 200 |
| GET | `/projects/{id}/updates` | GESTOR, LIDER | Histórico | 200 |
| POST | `/projects` | GESTOR | Cria | 201 |
| PUT | `/projects/{id}` | GESTOR | Edita (+diff, +conclusão) | 200 |
| DELETE | `/projects/{id}` | GESTOR | Exclui | 204 |
| GET | `/reports/summary` | LIDER | Resumo geral | 200 |
| GET | `/reports/guidelines` | LIDER | Retorno por estratégia (lista) | 200 |
| GET | `/reports/guidelines/{id}` | LIDER | Retorno de uma estratégia | 200 |
| GET | `/reports/projects/{id}` | LIDER | Retorno de um projeto | 200 |
| POST | `/reports/insights` | LIDER | Insights de IA (Gemini) | 200 |
| GET | `/health`, `/health/ready`, `/health/live` | público | Saúde | 200 |

\* operador restrito às próprias ideias (R2-03.3).

**Exemplos de payload (principais):**

```jsonc
// POST /auth/login
{ "email": "lider@aguiabranca.com", "password": "aguiabranca123" }
// 200
{ "accessToken": "eyJ...", "refreshToken": "q8...", "expiresIn": 1800,
  "user": { "id": "665f...", "name": "Líder", "email": "lider@aguiabranca.com",
            "role": "LIDER", "division": "CORPORATIVO", "points": 0, "badges": [] } }

// POST /ideas
{ "title": "Roteirização com IA", "description": "…", "category": "Tecnologia",
  "division": "LOGISTICA", "guidelineId": "665f..." }
// 201
{ "id": "6660...", "status": "SUBMETIDA", "guidelineTitle": "Eficiência operacional",
  "ice": null, "linkedProject": null, "authorId": "…", "authorName": "Operador",
  "createdAt": "2026-09-21T14:03:11Z", "pointsAwarded": 15 }

// PUT /ideas/{id}/ice
{ "impact": 8, "confidence": 7, "ease": 6 }   // → score 336, status EM_ANALISE

// POST /ideas/{id}/approve  → 200
{ "ideaId": "6660...", "projectId": "6661...", "alreadyApproved": false }

// PUT /projects/{id}
{ "title": "PROJ: Roteirização", "description": "…", "stage": "CONCLUIDO",
  "statusText": "Entregue", "investment": 120000, "financialReturn": 310000,
  "productivityGain": 12.5, "costReduction": 45000, "targetDate": "2026-12-01T00:00:00Z",
  "division": "LOGISTICA", "guidelineId": "665f...", "responsibleId": "…",
  "note": "Meta atingida", "version": 3 }

// GET /reports/summary?period=ALL&division=LOGISTICA   (percentuais com 2 casas; valores em BRL)
{ "period": "ALL", "division": "LOGISTICA", "generatedAt": "2026-09-21T22:15:00Z",
  "funnel": { "submitted": 6, "evaluated": 5, "approved": 3, "inExecution": 2, "roiPositive": 1 },
  "kpis": { "roiConsolidated": 36.73, "netProfit": 90000, "totalInvestment": 245000, "totalReturn": 335000,
            "activeProjects": 1, "avgProductivityGain": 8.25, "totalCostReduction": 53000, "overdueProjects": 0 },
  "sparkline": [ { "month": "2026-04", "roiPercent": null }, …, { "month": "2026-09", "roiPercent": 36.73 } ],
  "guidelineImpacts": [ { "guidelineId": "665f…", "title": "Eficiência operacional", "ideasCount": 3, "projectsCount": 2,
                          "investment": 120000, "financialReturn": 310000, "netProfit": 190000, "roiPercent": 158.33 } ],
  "projects": [ { "id": "6661…", "title": "PROJ: Roteirização", "stage": "CONCLUIDO", "division": "LOGISTICA",
                  "guidelineId": "665f…", "guidelineTitle": "Eficiência operacional",
                  "investment": 120000, "financialReturn": 310000, "netProfit": 190000, "roiPercent": 158.33,
                  "productivityGain": 12.5, "costReduction": 45000, "targetDate": "2026-12-01T00:00:00Z",
                  "daysToDeadline": null, "overdue": false, "statusText": "Entregue", "updatedAt": "…" } ] }
// daysToDeadline é null para projeto CONCLUIDO/CANCELADO ou sem prazo.

// POST /reports/insights   { "period": "ALL", "division": "LOGISTICA", "guidelineId": null, "refresh": false }   (corpo opcional: {} vale)
{ "summary": "…", "highlights": [ "…" ], "risks": [ "…" ],
  "recommendations": [ { "title": "…", "detail": "…", "priority": "ALTA", "relatedGuidelineId": "665f…" } ],
  "generatedAt": "2026-09-21T14:10:00Z", "model": "gemini-3.1-flash-lite", "fromCache": false }
// Erros: 503 AI_UNAVAILABLE · 502 AI_INVALID_RESPONSE · 429 RATE_LIMITED (+ Retry-After) · 404 (guidelineId inexistente) · 403 (não é líder)
```

---

## Modelo de dados MongoDB

Documentos em `camelCase`; `_id` ObjectId exposto como `id: string`.

```
users                           (ASP.NET Identity via stores customizados)
  _id, email, normalizedEmail, userName, normalizedUserName, passwordHash,
  securityStamp, accessFailedCount, lockoutEnd,
  name, role: "OPERADOR"|"GESTOR"|"LIDER", division,
  points: int ≥ 0, badges: string[], legacyId?, createdAt

refreshTokens
  _id, userId, familyId, tokenHash, expiresAt, createdAt, revokedAt?, replacedByHash?

guidelines
  _id, title, description, pillar, campaign?, authorId, authorName, legacyId?, createdAt, updatedAt

guidelineHistory                 (imutável; sobrevive à exclusão)
  _id, guidelineId, action: "CREATED"|"UPDATED"|"DELETED", occurredAt,
  category (=pillar), campaign?, title, snapshot{title,description,pillar,campaign},
  changedById, changedByName

ideas
  _id, title, description, category, division, guidelineId?, authorId, authorName,
  status: "SUBMETIDA"|"EM_ANALISE"|"APROVADA"|"REJEITADA"|"IMPLEMENTADA",
  ice?: {impact,confidence,ease,score}, reviewerId?, reviewComment?,
  legacyId?, createdAt, updatedAt, reviewedAt?

projects
  _id, title, description, stage, statusText, investment, targetDate?, financialReturn,
  productivityGain, costReduction, division, guidelineId?, creatorManagerId,
  originatingIdeaId?, priorityScore?, reporterId?, reporterName?, responsibleId?, responsibleName?,
  version: int, legacyId?, createdAt, updatedAt

projectUpdates
  _id, projectId, authorId, authorName, note, changes:[{field,from,to}], createdAt

pointEvents
  _id, userId, delta, reason: IDEA_CREATED|IDEA_DELETED|IDEA_APPROVED|IDEA_IMPLEMENTED|MIGRATION,
  refId?, createdAt

aiInsights                        (cache)
aiUsage                           (contador diário de gerações; _id = yyyy-MM-dd, count, expiresAt)
  _id, cacheKey (unique), userId, filters, model, payload, createdAt, expiresAt (TTL)
```

**Índices:** `users.normalizedEmail` (unique) · `refreshTokens.tokenHash` (unique) + TTL · `guidelines.updatedAt` desc · `guidelineHistory (guidelineId, occurredAt desc)`, `(category, occurredAt)`, `(campaign, occurredAt)` · `ideas (authorId, createdAt desc)`, `(status, ice.score desc)`, `(guidelineId, createdAt desc)` · `projects (division, updatedAt desc)`, `(guidelineId, updatedAt desc)`, `(stage, updatedAt desc)`, `originatingIdeaId` (unique parcial) · `projectUpdates (projectId, createdAt desc)` · `pointEvents (userId, createdAt)`, `(createdAt)` · `aiInsights.cacheKey` (unique) + TTL `expiresAt` · `aiUsage` (`_id` = dia `yyyy-MM-dd`, `count`) + TTL `expiresAt` (2 dias).

---

## Códigos de erro

`INVALID_CREDENTIALS` (401) · `TOKEN_INVALID` (401) · `FORBIDDEN` (403) · `SELF_APPROVAL_FORBIDDEN` (403) · `RESOURCE_NOT_FOUND` (404) · `VALIDATION_ERROR` (400) · `GUIDELINE_NOT_FOUND` (422) · `IDEA_NOT_EDITABLE` (409) · `IDEA_INVALID_STATE` (409) · `CONCURRENCY_CONFLICT` (409) · `RATE_LIMITED` / conta bloqueada (429) · `AI_INVALID_RESPONSE` (502) · `AI_UNAVAILABLE` (503) · `INTERNAL_ERROR` (500)

---

## Pontos abertos (precisam de confirmação; default assumido)

| # | Ponto | Default assumido |
|---|---|---|
| **OP-1** | "Categoria" e "campanha" do histórico de estratégias: `categoria` = `pillar` existente; `campanha` = campo novo opcional na orientação | Como descrito em R2-02 |
| **OP-2** | Existe conceito de estratégia "vigente" vs. encerrada? | Não: toda orientação existente é vigente; excluir = deixa de ser vigente (histórico preserva) |
| **OP-3** | Gestor pode cadastrar ideias? | Sim (as-built), bloqueando auto-aprovação |
| **OP-4** | Manter Firebase Analytics/Crashlytics no app? | Sim; só Auth e Firestore saem |
| **OP-5** | Onde hospedar o backend para o APK funcionar na avaliação? | Deploy gratuito (API) + MongoDB Atlas M0; fallback: `docker compose` local + IP da LAN |
| **OP-6** | Modelo Gemini exato do free tier | ✅ **Resolvida:** `gemini-3.1-flash-lite` (escolha do usuário, máxima economia de tokens; existe na conta e respondeu no smoke real em ~2,7 s, ~1,5 mil tokens/geração). Continua configurável (`Gemini:Model`): nomes de modelos gratuitos mudam com frequência (2.5 Flash/Flash-Lite já não aceitam contas novas) |
| **OP-7** | Paginação no app | App pede `pageSize=200` e não pagina (volume de demo); limitação registrada |

---

## Fora de escopo (Sprint 2)

Push notifications · inovação aberta · iOS · upload de anexos · e-mail/recuperação de senha · chat com IA e pontuação de ideias por IA (opções não escolhidas do enunciado — candidatas a evolução) · cache distribuído/mensageria.
