# Spec — Sprint 1 INOVAGAB

Aplicativo móvel Android que integra **estratégia → execução → mensuração** da inovação corporativa do Grupo Águia Branca.

Requisitos com IDs rastreáveis (`R-XX`) e critérios de aceitação testáveis.

---

## Eixo central: Conexão Estratégica

Toda ideia é vinculada a uma **orientação estratégica** do líder. Isso amarra os três níveis da organização e é a narrativa principal do app: o líder direciona, o operador captura na linguagem do direcionamento, o gestor curadoria, e o líder vê **qual orientação dele virou resultado real**.

---

## R-01 — Autenticação (todos os perfis)

**História:** Como usuário do Grupo Águia Branca, quero fazer login com email e senha do meu perfil para acessar funcionalidades correspondentes ao meu nível.

**Requisitos:**
- R-01.1 — App apresenta tela de login com email + senha
- R-01.2 — Firebase Authentication autentica o usuário
- R-01.3 — App busca `users/{uid}` em Firestore para descobrir `role` (`OPERADOR | GESTOR | LIDER`) e `division`
- R-01.4 — Sessão persiste entre execuções (Firebase Auth mantém token)
- R-01.5 — Logout disponível em qualquer tela autenticada
- R-01.6 — Telas e ações inacessíveis se não autenticado

**Aceitação:**
- Login com credenciais demo válidas leva à home do perfil correto
- Login com credenciais inválidas mostra erro
- Fechar e reabrir o app mantém o usuário logado
- Operador autenticado não consegue acessar rotas de gestor/líder

---

## R-02 — Orientações Estratégicas

**Histórias:**
- Como líder, quero criar/editar/remover orientações estratégicas para que todo o time veja o direcionamento.
- Como gestor ou operador, quero consultar as orientações estratégicas vigentes.

**Requisitos:**
- R-02.1 — Tela "Orientações" lista todas em ordem de mais recente
- R-02.2 — Líder vê botão "Nova orientação" e ações editar/remover em cada item
- R-02.3 — Gestor e operador veem apenas a leitura
- R-02.4 — Cada orientação tem: título, descrição, pilar (`DIRECIONAMENTO | IDEIAS | PROJETOS | MENSURACAO`), autor (líder), `createdAt`, `updatedAt`
- R-02.5 — Excluir orientação é hard-delete. Ideias e projetos com `guidelineId` apontando para uma orientação removida mostram "Orientação removida" no detalhe (referência órfã preservada para histórico)

**Aceitação:**
- Líder cria orientação e aparece no topo da lista imediatamente
- Operador e gestor nunca veem botão de criar/editar/remover
- Editar uma orientação atualiza `updatedAt` e reposiciona o item para o topo
- Excluir uma orientação que tinha ideias vinculadas mantém as ideias intactas; ideias mostram "Orientação removida"

---

## R-03 — Ideias (Operador + Gestor)

**Histórias:**
- Como operador, quero cadastrar problemas/ideias do dia a dia conectados a uma orientação estratégica.
- Como operador, quero acompanhar o status das ideias que cadastrei.
- Como gestor, quero priorizar e aprovar as melhores ideias usando uma matriz objetiva.

**Requisitos:**

### Cadastro e listagem
- R-03.1 — Operador cadastra ideia com: título, descrição, categoria (R-03.10), divisão (`PASSAGEIROS | COMERCIO | LOGISTICA | CORPORATIVO`, pré-selecionada com a divisão do usuário), **orientação estratégica vinculada** (R-03.14)
- R-03.2 — Operador vê apenas as próprias ideias na aba "Minhas ideias"
- R-03.3 — Status da ideia: `SUBMETIDA | EM_ANALISE | APROVADA | REJEITADA | IMPLEMENTADA`
- R-03.4 — Gestor vê todas as ideias com status `SUBMETIDA` ou `EM_ANALISE` em sua lista de curadoria

### Priorização e decisão (gestor)
- R-03.5 — Gestor preenche matriz **ICE** (Impacto 1-10, Confiança 1-10, Facilidade 1-10) → score = Impacto × Confiança × Facilidade. Botão "Salvar avaliação" só habilita quando os **três** campos têm valores inteiros entre 1 e 10
- R-03.6 — Lista do gestor ordena por ICE score descendente; ideias sem ICE aparecem agrupadas no fim como "Aguardando avaliação" (corresponde a `status = SUBMETIDA`)
- R-03.7 — Gestor pode aprovar (status APROVADA) ou rejeitar (status REJEITADA) com comentário obrigatório
- R-03.8 — **Automação 1 (aprovação → projeto rascunho):** ao aprovar, sistema cria automaticamente projeto em `projects/{id}` com `originatingIdeaId = ideaId`, `stage = PLANEJAMENTO`, `title = "PROJ: " + ideia.title`, `creatorManagerId = gestor que aprovou`, `division = ideia.division`, `guidelineId = ideia.guidelineId`

### Jornada visível para o autor
- R-03.9 — **Timeline visual da jornada** na página de detalhe da ideia (visível para o autor):
  - Stepper vertical com 5 estágios: `Submetida → Em análise → Aprovada → Em execução → Resultado obtido`
  - Estágios alcançados em verde (✅) com data; pendentes em cinza (○) sem data
  - Estágio "Rejeitada" substitui o stepper por mensagem específica quando aplicável
  - Mapeamento:
    - `Submetida` ↔ `status = SUBMETIDA`
    - `Em análise` ↔ `status = EM_ANALISE`
    - `Aprovada` ↔ `status = APROVADA` (projeto rascunho existe em PLANEJAMENTO)
    - `Em execução` ↔ projeto vinculado com `stage IN (EM_EXECUCAO, CONCLUIDO)`
    - `Resultado obtido` ↔ `status = IMPLEMENTADA`

### Categoria
- R-03.10 — **Categoria** é texto livre com autocomplete:
  - Input livre (mín 2 chars, máx 40 chars)
  - Ao digitar, app sugere categorias já existentes em outras ideias (busca por prefixo, case-insensitive)
  - Operador pode aceitar sugestão ou criar nova
  - Normalização leve: trim, primeira letra maiúscula

### Edição pelo autor
- R-03.11 — **Edição/exclusão pelo operador autor** permitida **somente enquanto status = SUBMETIDA**:
  - A partir de `EM_ANALISE`, botões "Editar" e "Excluir" desaparecem do detalhe
  - Exclusão é hard-delete
  - Pontos de R-06.1 (+10) e R-06.2 (+5 conexão estratégica, quando aplicável) são revertidos no `users.points` (com clamp em 0 — ver R-06.7)

### Transições automáticas
- R-03.12 — **Transição SUBMETIDA → EM_ANALISE:** ocorre quando o gestor salva a primeira avaliação ICE completa (R-03.5). Sem botão "Iniciar análise" explícito
- R-03.13 — **Automação 2 (projeto concluído → ideia IMPLEMENTADA + pontos):** quando projeto vinculado tem `stage` alterado para `CONCLUIDO`:
  - `ideia.status = IMPLEMENTADA` (em transação Firestore)
  - +200 pontos ao autor original (R-06.3)
  - Idempotente (não credita se já era IMPLEMENTADA)

### Conexão estratégica (eixo central)
- R-03.14 — **Orientação estratégica vinculada à ideia:**
  - No cadastro, operador escolhe (campo `guidelineId`) qual orientação a ideia endereça
  - Dropdown alimentado pelas orientações existentes em `strategicGuidelines`, ordenadas por mais recente
  - **Comportamento quando não há orientações cadastradas:** dropdown mostra "Nenhuma orientação cadastrada ainda" e ideia pode ser submetida sem vínculo (`guidelineId = null`)
  - Quando vinculada: operador ganha **+5 pontos extras** (R-06.1.1) e ideia se qualifica para badge "Estrategista"
  - Detalhe da ideia exibe no topo: "🎯 Conectada com: [título da orientação]". Se orientação foi deletada, mostra "🎯 Orientação removida"
  - Projeto criado via R-03.8 herda o `guidelineId` da ideia origem

**Aceitação:**
- Operador cadastra ideia em ≤ 4 toques após login (selecionar orientação adiciona um toque)
- Gestor consegue priorizar lista de 20+ ideias em ordem ICE clara
- Aprovar uma ideia gera projeto rascunho prefixado "PROJ:" + título da ideia, herdando `guidelineId`
- Operador vê stepper com estágios alcançados em verde e datas reais
- Operador não consegue editar ideia a partir do momento em que gestor salva ICE
- Excluir ideia em SUBMETIDA reduz `users.points` em 10 (ou 15 se tinha conexão estratégica), sem ficar negativo
- Botão "Salvar avaliação" desabilitado se qualquer campo ICE estiver vazio ou fora de [1,10]
- Rejeitar exige preencher campo de comentário
- Gestor altera `stage` do projeto vinculado para `CONCLUIDO` → autor da ideia recebe +200 pontos e status da ideia vira `IMPLEMENTADA`
- Cadastrar ideia com `guidelineId` preenchido credita +15 pts (10 base + 5 conexão); sem `guidelineId` credita +10 pts
- Detalhe da ideia mostra "🎯 Conectada com: [título]" sempre que `guidelineId` não-nulo

---

## R-04 — Projetos e Iniciativas

**Histórias:**
- Como gestor, quero cadastrar projetos e atualizar seus dados conforme avançam.
- Como líder, quero consultar o andamento dos projetos e ver o histórico de evolução de cada um.

**Requisitos:**
- R-04.1 — Projeto tem: título, descrição, etapa (`PLANEJAMENTO | EM_EXECUCAO | CONCLUIDO | CANCELADO`), status textual livre, investimento (R$), prazo (data alvo), retorno financeiro (R$ obtido), ganho de produtividade (%), reducaoCusto (R$), divisão (`PASSAGEIROS | COMERCIO | LOGISTICA | CORPORATIVO`), `creatorManagerId` (uid do gestor que criou — auditoria apenas), `guidelineId` (orientação estratégica — herdada da ideia ou escolhida pelo gestor em criação direta), `originatingIdeaId` (null se criado direto pelo gestor)
- R-04.2 — Qualquer gestor pode criar, editar e deletar qualquer projeto (sem "dono exclusivo" no Sprint 1)
- R-04.3 — Líder lê todos os projetos; **não pode** editar
- R-04.4 — Operadores não acessam essa tela
- R-04.5 — **Histórico de atualizações** (timeline visual):
  - Cada save do gestor cria uma entrada em `projects/{id}/updates` contendo: timestamp, gestor (uid + nome), nota opcional, diff dos campos alterados
  - Página de detalhe do projeto exibe timeline vertical (mais recentes no topo) com data, autor, nota, lista de campos modificados ("Investimento: R$ 100k → R$ 120k")
  - Líder e gestor veem a timeline
- R-04.6 — Criação inicial gera primeira entrada no histórico (nota "Projeto criado")
- R-04.7 — Quando projeto é criado via automação de aprovação (R-03.8), primeira entrada do histórico cita "Criado automaticamente a partir da ideia: {título}"
- R-04.8 — Gestor pode cadastrar projeto **direto** (sem partir de ideia). Nesse caso, escolhe orientação estratégica no cadastro, mesma lógica de R-03.14 (opcional se não houver orientações)

**Aceitação:**
- Projeto criado aparece imediatamente na lista de líderes com 1 entrada no histórico
- Editar um projeto persiste no Firestore e adiciona nova entrada no histórico
- Líder abre projeto e vê timeline com pelo menos 2 entradas (criação + 1 atualização)
- Diff exibe campos com valor anterior e novo
- Líder não vê botão "Editar" nem "Excluir" em nenhuma tela de projeto
- Operador é redirecionado/bloqueado ao tentar abrir tela de projetos
- Projeto criado por automação herda `guidelineId` da ideia origem

---

## R-05 — Dashboard de Resultados (Líder)

**História:** Como líder, quero ver o impacto consolidado da inovação, qual orientação estratégica está virando resultado, e poder consultar projetos individuais.

**Layout:**

1. **Topo — Funil de Inovação** (Compose Canvas, barras horizontais proporcionais ao total). Cada estágio é subset do anterior:
   - **Submetidas** — todas as ideias cadastradas (qualquer status)
   - **Avaliadas** — `status IN (EM_ANALISE, APROVADA, REJEITADA, IMPLEMENTADA)`
   - **Aprovadas** — `status IN (APROVADA, IMPLEMENTADA)`
   - **Em execução** — projetos vinculados com `stage IN (EM_EXECUCAO, CONCLUIDO)`
   - **ROI positivo** — projects com `financialReturn > investment` E `stage = CONCLUIDO`
2. **Meio — Cards de KPI** (grid 2 colunas):
   - ROI consolidado (%)
   - Lucro líquido total (R$)
   - Investimento total (R$)
   - Projetos ativos (contagem)
   - Ganho médio de produtividade (%)
   - Redução de custo total (R$)
3. **Sparkline de tendência ROI** — mini-gráfico de linha mostrando ROI dos últimos 6 meses (mensal, agrupado por `projects.updatedAt`)
4. **Impacto por Orientação** (R-05.10) — seção destacada agregando resultados por orientação estratégica
5. **Lista de projetos por ROI** — descendente, cada item: título, ROI %, lucro, prazo, badge de status

**Requisitos:**
- R-05.1 — Funil renderizado como hero, com 5 estágios proporcionais ao total
- R-05.2 — Cards de KPI em grid 2 colunas
- R-05.3 — Sparkline em janela fixa de 6 meses (independente do filtro de período). Sem padding artificial quando há menos de 6 meses de dados
- R-05.4 — Lista de projetos ordenada por ROI descendente, com badge de status colorida
- R-05.5 — ROI consolidado = (Σ retornoFinanceiro − Σ investimento) / Σ investimento × 100
- R-05.6 — Edge case ROI: quando Σ investimento = 0, KPI exibe "—" (sem divisão por zero); sparkline pula esse mês
- R-05.7 — Tela nunca apresenta dados em formato tabular — sempre cards, barras ou gráficos
- R-05.8 — Atualização em tempo real (Firestore snapshot listener)
- R-05.9 — **Filtros no topo da tela:**
  - **Período:** `Este mês | Último trimestre | Este ano | Tudo` (padrão: `Tudo`)
  - **Divisão:** `Tudo | Passageiros | Comércio | Logística | Corporativo` (padrão: `Tudo`)
  - Filtros aplicam ao funil, KPIs e lista de projetos
  - Sparkline imune ao filtro de período (janela fixa) mas sensível ao filtro de divisão
  - Seção "Impacto por Orientação" (R-05.10) também respeita ambos os filtros
  - Filtros persistem na sessão, resetam no logout
- R-05.10 — **Impacto por Orientação** (eixo central do produto):
  - Seção destacada com card por orientação estratégica
  - Cada card mostra: título da orientação, contagem de ideias vinculadas, contagem de projetos vinculados, ROI consolidado das ideias dessa orientação
  - Ordenado por ROI descendente; orientações sem projeto vão para o fim
  - Toque no card faz drill-down para lista das ideias/projetos daquela orientação
  - Líder enxerga **quais direcionamentos dele estão virando resultado real**
- R-05.11 — **Modo apresentação** (botão "▶ Apresentar" no topo da tela):
  - Entra em modo fullscreen, oculta system bars
  - KPIs ficam grandes, com animação **count-up** (números crescem de 0 ao valor real em ~1.5s)
  - Funil também anima entrada (barras crescem da esquerda)
  - Toque sai do modo
  - Pensado para reuniões de C-level e para o vídeo demo do FIAP

**Aceitação:**
- Editar retorno de um projeto atualiza KPIs do dashboard em <2s
- ROI exibido em % com 1 casa decimal
- Funil sempre proporcional (estágio com mais itens é o mais largo)
- Nenhuma view usa `Row(...)` simulando tabela com colunas alinhadas
- Selecionar "Logística" no filtro reduz o funil, KPIs, sparkline, "Impacto por Orientação" e lista
- Quando todos os projetos da divisão filtrada têm investimento = 0, ROI mostra "—"
- "Impacto por Orientação" mostra ao menos uma linha por orientação com ideia vinculada
- Tocar em "▶ Apresentar" entra em fullscreen e dispara animação count-up dos KPIs

---

## R-06 — Gamificação e Reconhecimento

**História:** Como Águia Branca, quero reconhecer operadores que conectam suas ideias à estratégia e geram impacto real, para fomentar cultura de inovação alinhada.

**Requisitos:**

### Pontuação (apenas por marcos da jornada)
- R-06.1 — **+10 pontos** ao cadastrar ideia
- R-06.1.1 — **+5 pontos extras** quando a ideia é cadastrada com `guidelineId` não-nulo (conexão estratégica)
- R-06.2 — **+50 pontos** quando ideia é aprovada (R-03.7)
- R-06.3 — **+200 pontos** quando ideia vira projeto implementado (R-03.13)

### Badges (cada uma desbloqueável uma vez por usuário)
- R-06.4 — Badges:
  - **"Primeira Ideia"** — 1ª submissão
  - **"Estrategista"** — 1ª ideia conectada a uma orientação ativa que virou aprovada (mostra alinhamento operacional ↔ estratégico)
  - **"Inovador do Mês"** — primeira vez que cadastra ≥ 5 ideias dentro de um mesmo mês calendário (única na vida)
  - **"Impacto Real"** — 1ª ideia que virou projeto implementado (R-03.13)
  - **"Visionário"** — 3 ideias aprovadas vinculadas a **orientações diferentes** (mostra amplitude de pensamento estratégico)

### Visualização
- R-06.5 — Tela de perfil mostra pontos totais, badges conquistadas, e contagem de ideias por status do usuário
- R-06.6 — Home do operador mostra ranking top 5 do mês (operadores ordenados por pontos conquistados no mês corrente)

### Clamp
- R-06.7 — `users.points` nunca fica negativo. Toda subtração (R-03.11) usa `max(0, points + delta)`

**Aceitação:**
- Pontos visíveis na tela de perfil
- Badges aparecem como ícones coloridos quando desbloqueadas
- Cada badge só desbloqueia uma vez (re-cumprir o critério não duplica)
- Ranking atualiza ao recarregar a home
- Subtração de pontos abaixo de 0 trava em 0
- Cadastrar 5 ideias num mês desbloqueia "Inovador do Mês"; cadastrar mais ideias no mesmo mês ou em outro mês não desbloqueia de novo
- Aprovar a 3ª ideia do operador vinculada a orientações distintas desbloqueia "Visionário"

---

## Modelo de dados Firestore

```
users/{uid}
  name: string
  email: string
  role: "OPERADOR" | "GESTOR" | "LIDER"
  division: "PASSAGEIROS" | "COMERCIO" | "LOGISTICA" | "CORPORATIVO"
  points: number                       // sempre ≥ 0 (R-06.7 clamp)
  badges: string[]                     // cada string única; sem repetição
  createdAt: timestamp

strategicGuidelines/{id}
  title: string
  description: string
  pillar: "DIRECIONAMENTO" | "IDEIAS" | "PROJETOS" | "MENSURACAO"
  authorId: string                     // uid do líder criador
  authorName: string
  createdAt: timestamp
  updatedAt: timestamp

ideas/{id}
  title: string
  description: string
  category: string                              // texto livre normalizado (R-03.10)
  division: "PASSAGEIROS" | "COMERCIO" | "LOGISTICA" | "CORPORATIVO"
  guidelineId: string | null                    // R-03.14 — conexão estratégica
  authorId: string
  authorName: string
  status: "SUBMETIDA" | "EM_ANALISE" | "APROVADA" | "REJEITADA" | "IMPLEMENTADA"
  ice: { impact: number, confidence: number, ease: number, score: number } | null
    // todos 1-10 inteiros (R-03.5); null até gestor preencher
  reviewerId: string | null
  reviewComment: string | null                  // obrigatório quando rejeita (R-03.7)
  createdAt: timestamp
  reviewedAt: timestamp | null

projects/{id}
  title: string
  description: string
  stage: "PLANEJAMENTO" | "EM_EXECUCAO" | "CONCLUIDO" | "CANCELADO"
  statusText: string
  investment: number
  targetDate: timestamp
  financialReturn: number
  productivityGain: number              // percentual (0-100+)
  costReduction: number
  division: "PASSAGEIROS" | "COMERCIO" | "LOGISTICA" | "CORPORATIVO"
  guidelineId: string | null            // herdado da ideia origem ou escolhido no cadastro direto
  creatorManagerId: string              // uid do gestor que criou; auditoria apenas
  originatingIdeaId: string | null      // null se cadastrado direto pelo gestor
  createdAt: timestamp
  updatedAt: timestamp                  // usado pelo sparkline mensal (R-05.3)

projects/{id}/updates/{updateId}        // subcoleção (R-04.5)
  authorId: string                      // uid do gestor que fez o save
  authorName: string
  note: string                          // nota opcional (pode ser vazia)
  changes: array<{ field: string, from: any, to: any }>
  createdAt: timestamp
```

---

## Credenciais de demonstração (seed)

| Email | Senha | Perfil | Divisão |
|---|---|---|---|
| lider@inovagab.com | inovagab123 | LIDER | CORPORATIVO |
| gestor@inovagab.com | inovagab123 | GESTOR | LOGISTICA |
| operador@inovagab.com | inovagab123 | OPERADOR | LOGISTICA |
