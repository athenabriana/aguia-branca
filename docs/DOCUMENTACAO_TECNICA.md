# ÁGUIA BRANCA — Documentação Técnica (Sprint 1)

**Cliente:** Grupo Águia Branca · **Contexto:** FIAP Challenge 2026 · **Sprint:** 1 (26/05/2026)

---

## 1. Visão

Aplicativo móvel Android nativo que integra a gestão da inovação corporativa do Grupo Águia Branca, conectando três níveis organizacionais (operacional, tático, estratégico) em um único ambiente para capturar, estruturar e acompanhar iniciativas de inovação que gerem **impacto mensurável**.

**Eixo central — Conexão Estratégica:** toda ideia pode ser vinculada a uma orientação estratégica do líder. Isso amarra os três níveis e é a narrativa principal: o líder direciona, o operador captura, o gestor cura, o líder vê **qual direcionamento dele virou resultado real**.

---

## 2. Stack

| Camada | Tecnologia | Versão |
|---|---|---|
| Plataforma | Android nativo | minSdk **24**, targetSdk **35** |
| Linguagem | Kotlin | **2.1.0** |
| UI | Jetpack Compose + Material 3 | Compose BOM 2024.12.01 |
| Arquitetura | MVVM + Repository + UseCases + DTO↔Domain | — |
| Estado | ViewModel + StateFlow + SavedStateHandle | lifecycle 2.8.7 |
| Navegação | Navigation Compose type-safe (kotlinx.serialization) | 2.8.5 |
| DI | Hilt | 2.55 |
| Auth | Firebase Authentication (email/senha) | Firebase BOM 33.7.0 |
| Banco | Cloud Firestore (transações + listeners) | Firebase BOM 33.7.0 |
| Observabilidade | Firebase Crashlytics + Analytics | Firebase BOM 33.7.0 |
| Splash | androidx.core:core-splashscreen | 1.0.1 |
| Edge-to-edge | activity-compose enableEdgeToEdge() | 1.9.3 |
| Build | Gradle Kotlin DSL + Version Catalog | Gradle 8.11.1 / AGP 8.7.3 |
| JDK | 17 (compatível 21) | — |
| Testes | JUnit 4 + MockK + Turbine + kotlinx-coroutines-test | — |

---

## 3. Arquitetura

```
┌───────────────────────────────────────────────────────────┐
│                       Compose UI                           │
│  Screens · ViewModels (Hilt) · Components · LocalSession  │
└───────────▲──────────────────────────────────▲────────────┘
            │                                  │
            │ StateFlow                        │ UseCases (transações)
            │                                  │
┌───────────┴──────────────────────────────────┴────────────┐
│                  Domain (puro Kotlin)                      │
│   Models  ·  Repository interfaces  ·  DomainError         │
│   Outcome wrapper  ·  BadgeEvaluator (regra puro)          │
└───────────▲──────────────────────────────────▲────────────┘
            │                                  │
            │ DTO ↔ Domain mappers             │ Hilt @Binds
            │                                  │
┌───────────┴──────────────────────────────────┴────────────┐
│              Data (Firestore impl)                         │
│   FirestoreUsersRepository · GuidelinesRepository          │
│   IdeasRepository · ProjectsRepository                     │
│   (transações + snapshot listeners)                        │
└────────────────────────────────────────────────────────────┘
```

**Princípios:**
- **Camadas puras:** `core/domain/` não tem dependência Android nem Firebase. Substituir backend no Sprint 2 troca apenas `core/data/` e `feature/**/data/`.
- **Type-safe nav:** rotas como `data object`/`data class` `@Serializable`, com `navController.navigate(Route.IdeaDetail(id))`.
- **Atomic transactions** para operações multi-doc: criar ideia + creditar pontos; aprovar ideia + criar projeto + credita +50 + entry inicial no histórico do projeto; completar projeto + ideia IMPLEMENTADA + creditar +200 — todas dentro de `firestore.runTransaction`.
- **UiState sealed** padroniza Loading/Success/Error em todas as telas; `DomainError sealed` traduz exceções para PT-BR.
- **CompositionLocal LocalSession** evita prop drilling do usuário autenticado.
- **SavedStateHandle** em todos os ViewModels de formulário para sobreviver a process death.

---

## 4. Modelo de Dados (Firestore)

```
users/{uid}
  name, email, role (OPERADOR|GESTOR|LIDER),
  division (PASSAGEIROS|COMERCIO|LOGISTICA|CORPORATIVO),
  points: number ≥ 0,
  badges: string[],
  createdAt: timestamp

strategicGuidelines/{id}
  title, description, pillar, authorId, authorName,
  createdAt, updatedAt

ideas/{id}
  title, description, category (texto livre normalizado),
  division, guidelineId | null,
  authorId, authorName,
  status (SUBMETIDA|EM_ANALISE|APROVADA|REJEITADA|IMPLEMENTADA),
  ice: {impact, confidence, ease, score} | null,
  reviewerId | null, reviewComment | null,
  createdAt, reviewedAt | null

projects/{id}
  title, description, stage (PLANEJAMENTO|EM_EXECUCAO|CONCLUIDO|CANCELADO),
  statusText, investment, targetDate,
  financialReturn, productivityGain, costReduction,
  division, guidelineId | null,
  creatorManagerId, originatingIdeaId | null,
  createdAt, updatedAt

projects/{id}/updates/{updateId}   # subcoleção (histórico)
  authorId, authorName,
  note: string,
  changes: [{ field, from, to }],
  createdAt
```

### Índices recomendados (`firestore.indexes.json`)

| Coleção | Campos | Uso |
|---|---|---|
| `ideas` | `authorId ASC, createdAt DESC` | Minhas ideias |
| `ideas` | `status ARRAY, createdAt DESC` | Curadoria |
| `ideas` | `guidelineId ASC` | Drill-down por orientação |
| `projects` | `guidelineId ASC` | Drill-down por orientação |
| `projects` | `updatedAt DESC` | Dashboard + sparkline |
| `users` | `role ASC, points DESC` | Ranking |

---

## 5. Fluxos Críticos

### 5.1 Cadastro de Ideia (R-03.1 + R-06.1)

```
Operador preenche form
    ↓
NewIdeaViewModel.submit()
    ↓
IdeasRepository.createIdea()
    ↓ Transação:
    ├─→ cria doc em ideas/
    ├─→ users/{uid}.points += 10 (ou +15 se com guidelineId)
    └─→ commit
    ↓
Analytics.logIdeaCreated(hasGuideline)
    ↓
Toast: "+10 pts" ou "+15 pts (10 + 5 conexão estratégica)"
```

### 5.2 Aprovação de Ideia → Projeto Rascunho (R-03.8)

```
Gestor preenche ICE + tap Aprovar
    ↓
ApproveIdeaUseCase.invoke(ideaId, reviewerId)
    ↓ Transação ÚNICA:
    ├─→ ideia.status = APROVADA, reviewerId, reviewedAt
    ├─→ cria projects/{newId} com title = "PROJ: " + ideia.title,
    │   stage = PLANEJAMENTO, guidelineId = ideia.guidelineId,
    │   originatingIdeaId = ideaId, creatorManagerId = reviewerId
    ├─→ cria projects/{newId}/updates/{auto} com note
    │   "Criado automaticamente a partir da ideia: …"
    └─→ users/{authorId}.points += 50
    ↓
Analytics.logIdeaApproved(projectId)
```

### 5.3 Conclusão do Projeto → Ideia IMPLEMENTADA (R-03.13)

```
Gestor edita projeto e seleciona stage = CONCLUIDO
    ↓
ProjectFormViewModel.submit() (update + change diff)
    ↓
CompleteProjectUseCase.invoke(projectId)
    ↓ Transação:
    ├─→ se originatingIdeaId != null E ideia.status != IMPLEMENTADA:
    │     ideia.status = IMPLEMENTADA
    │     users/{authorId}.points += 200
    │     (Idempotência: re-run não credita de novo)
    └─→ commit
    ↓
Analytics.logProjectCompleted(projectId, hasIdea)
```

### 5.4 Dashboard — Impacto por Orientação (R-05.10)

```
DashboardViewModel.state = combine(
    ideasRepo.observeAll(),
    projectsRepo.observeAll(),
    guidelinesRepo.observeAll(),
    filters
) → DashboardComputer.compute(...) → DashboardState
```

`DashboardComputer` (puro, testável):
- Aplica filtros (período/divisão) a ideias e projetos
- **Funil** de 5 estágios: Submetidas → Avaliadas → Aprovadas → Em execução → ROI positivo
- **KPIs:** ROI consolidado (= (ΣretornoFinanceiro − Σinvestimento) / Σinvestimento × 100, null se invest=0), lucro, investimento, projetos ativos, ganho produtividade médio, redução de custo
- **Sparkline ROI:** janela fixa de 6 buckets mensais (ignora filtro de período; aplica divisão); pula meses com investimento 0
- **Impacto por Orientação:** card por orientação com contagem ideias, contagem projetos, ROI consolidado; ordenado descendente; orientações sem projeto ao fim
- **Modo apresentação:** entra fullscreen, KPIs animam count-up via `animateFloatAsState` por 1.5s, funil anima entrada (barras crescem da esquerda)

---

## 6. Gamificação (R-06)

### Pontos (apenas por marcos)
- **+10** ao cadastrar ideia
- **+5** extras se ideia tem `guidelineId` (conexão estratégica)
- **+50** quando ideia é aprovada
- **+200** quando vira projeto implementado

`points` nunca negativo — toda subtração usa `max(0, points + delta)` via transação.

### Badges (cada uma desbloqueável uma vez)
- **Primeira Ideia** — 1ª submissão
- **Estrategista** — 1ª ideia conectada a uma orientação que virou aprovada
- **Inovador do Mês** — primeira vez que cadastra ≥ 5 ideias num mesmo mês calendário
- **Impacto Real** — 1ª ideia que virou projeto implementado
- **Visionário** — 3 ideias aprovadas vinculadas a orientações **distintas**

Lógica em `core/domain/badge/BadgeEvaluator.kt` (puro Kotlin, testado).

---

## 7. Testes

| Suíte | Cobertura |
|---|---|
| `IceTest` | `Ice.score`, `Ice.isComplete` em bordas e casos inválidos |
| `MapperTest` | Round-trip DTO↔Domain para User, Guideline, Idea (com e sem ICE), Project |
| `BadgeEvaluatorTest` | Cada badge: caso positivo, casos negativos, idempotência (não re-desbloqueia) |

Composables / Screens / Repositories Firestore: validação manual no Sprint 1 (sem instrumentação).

---

## 8. Build & Distribuição

### Debug local
```bash
./gradlew :app:assembleDebug
```

### Release assinado
1. `keytool -genkey -keystore keystore/release.jks -alias aguiabranca ...`
2. Criar `keystore.properties`:
   ```
   storeFile=keystore/release.jks
   storePassword=…
   keyAlias=aguiabranca
   keyPassword=…
   ```
3. `./gradlew :app:assembleRelease`
4. APK em `app/build/outputs/apk/release/app-release.apk`

### Seed de dados
Long-press no logo da tela de login (R-T20) → cria 3 usuários, 4 orientações, 6 ideias, 3 projetos.

---

## 9. Limitações conhecidas (Sprint 1)

- **Inovação Aberta** (pilar 3) ainda não implementada — está prevista para Sprint 2.
- Apenas autenticação email/senha — não há SSO corporativo.
- Sem testes instrumentados (UI) — validação manual no Sprint 1.
- `google-services.json` precisa ser substituído pelo do projeto real do cliente antes de rodar.
- Sem regras de Firestore granulares — Sprint 1 usa regra mínima (`request.auth != null`). Sprint 2 introduzirá regras por role/propriedade.
- Modo apresentação não oculta system bars (Compose `WindowInsetsController` requereria adaptação adicional do MainActivity); funciona como fullscreen visual via `Surface.fillMaxSize`.

---

## 10. Sprint 2 (próximo)

- Substituir camada Firestore por API Java/C# REST mantendo `core/domain/`.
- Adicionar suporte a Inovação Aberta (pilar 3).
- SSO corporativo (OAuth provider Águia Branca).
- Regras Firestore por papel + auditoria.
- Testes de integração (Firestore Emulator) + UI tests.
