# STATE — INOVAGAB

## Decisões registradas

- **D1 (2026-05-26):** Android nativo (Kotlin + Compose). iOS descartado por inviabilidade de ambiente macOS.
- **D2 (2026-05-26):** Firebase Auth + Cloud Firestore como backend Sprint 1. Atende "conectividade externa obrigatória" e desacopla da decisão de backend Sprint 2 (Java/C#).
- **D3 (2026-05-26):** Spec unificada em vez de uma por feature, dada a fortíssima interdependência entre os módulos e o prazo.
- **D4 (2026-05-26):** Inovação (peso 10%) consiste em: matriz ICE para priorização + gamificação (pontos/badges/ranking) + automação idea→projeto + dashboard rico.
- **D5 (2026-05-26):** Gamificação extra inclui curtidas + comentários entre operadores (R-07). Pontos: autor recebe +2pts por curtida; quem curte não ganha pontos diretos (badge "Apoiador" reconhece padrão de comportamento). Curtidas são informação auxiliar ao gestor, não influenciam o score ICE.
- **D6 (2026-05-26):** Dashboard do líder usa Funil de Inovação como hero visual + KPIs em cards + sparkline de tendência ROI. Proibido apresentação tabular (planilha).
- **D7 (2026-05-26):** Jornada da ideia exibida como timeline/stepper vertical na página de detalhe da ideia (operador), com 5 estágios. Conecta narrativa individual ao funil agregado do dashboard.
- **D8 (2026-05-26):** Categoria da ideia é texto livre com autocomplete (sugestões baseadas em categorias já usadas), não lista fixa. Normalização: trim + primeira letra maiúscula. Sem deduplicação agressiva.
- **D9 (2026-05-26):** Operador pode editar e deletar sua própria ideia **apenas enquanto status = SUBMETIDA**. Transição automática para `EM_ANALISE` (que congela edição) ocorre quando gestor salva primeiro valor ICE. Exclusão reverte os +10 pontos de R-06.1.
- **D10 (2026-05-26):** Dashboard tem dois filtros simultâneos: período (mês/trimestre/ano/tudo) + divisão (Passageiros/Comércio/Logística/Corporativo/Tudo). Filtros persistem na sessão, resetam no logout.
- **D11 (2026-05-26):** Projetos têm histórico de atualizações em subcoleção `projects/{id}/updates`. Cada save do gestor cria entrada com timestamp, autor, nota opcional e diff dos campos alterados. Exibido como timeline vertical no detalhe do projeto.
- **D12 (2026-05-26):** Qualquer gestor pode editar/deletar qualquer projeto (sem owner exclusivo no Sprint 1). Líder nunca edita projetos (consistência com a rubrica). Hard-delete para ideias e projetos (sem soft-delete no Sprint 1). Quando Σ investimento = 0, ROI é exibido como "—".
- **D13 (2026-05-26):** Divisão `CORPORATIVO` é válida em **todos** os contextos (cadastro de ideia, projeto, filtro de dashboard). Operadores corporativos (RH, TI, financeiro) não ficam de fora.
- **D14 (2026-05-26):** Funil de inovação e stepper da ideia distinguem "Aprovada" (rascunho em PLANEJAMENTO existe) de "Em execução" (projeto com `stage ∈ {EM_EXECUCAO, CONCLUIDO}`). Isso resolve a duplicação que existia entre "Aprovadas" e "Viraram projeto".
- **D15 (2026-05-26):** Transição automática `ideia.status = IMPLEMENTADA` ocorre quando projeto vinculado vira `stage = CONCLUIDO`. A automação dispara +200pts (R-06.3) ao autor da ideia, idempotente.
- **D16 (2026-05-26):** Campo renomeado de `ownerManagerId` para `creatorManagerId` em `projects/{id}` para refletir que é apenas auditoria — qualquer gestor edita qualquer projeto.
- **D17 (2026-05-26):** Sparkline de tendência ROI é janela fixa de 6 meses (`projects.updatedAt`), independente do filtro de período. Reagiu ao filtro de divisão. Resolve ambiguidade de comportamento sob filtros.
- **D18 (2026-05-26):** Pontuação tem clamp em 0 (`max(0, points + delta)`). Descurtir reverte os +2pts do autor. Badge "Inovador do Mês" é única por usuário (uma vez na vida). ICE só pode ser salvo com os três campos preenchidos entre 1 e 10.
- **D19 (2026-05-26):** Notificação de atividade (curtida/comentário) rastreada via `users.lastSeenIdeaActivityAt: timestamp`, atualizado ao operador entrar em "Minhas ideias". Operador autor pode comentar/responder na própria ideia via página de detalhe (R-07.4).

### Reset de inovação (2026-05-26, sessão de revisão)

A direção de "engajamento social via curtidas + comentários" estava solta — pontos por curtida sem ancoragem em marco, badge "Apoiador" arbitrária, complexidade transacional alta para pouco retorno. Substituída por **Conexão Estratégica** como eixo central do produto. Decisões abaixo invalidam parcialmente D5, D9 (parte de reverter pontos de curtidas), D18 (clamp ainda vale; descurtir não existe mais) e D19 (notificação removida).

- **D20 (2026-05-26):** **R-07 (Curtidas e Comentários Sociais) removida inteiramente da spec.** Cortado por: (a) ausência de propósito real além de gratificação social que a contagem visível já dá, (b) risco em ambiente corporativo sem moderação, (c) custo de implementação desproporcional ao retorno, (d) dilui o foco do app em "integrar estratégia, execução e mensuração". Modelo de dados perde `ideas.likeCount/likedBy/commentCount/lastActivityAt`, subcoleção `ideas/{id}/comments` e `users.lastSeenIdeaActivityAt`.
- **D21 (2026-05-26):** **Conexão Estratégica é o eixo central.** Toda ideia tem campo `guidelineId` (opcional apenas se ainda não houver orientações cadastradas). Projeto herda `guidelineId` da ideia origem ou recebe via cadastro direto. Operador ganha +5 pts extras ao conectar (R-06.1.1).
- **D22 (2026-05-26):** Badges reorganizadas para refletir conexão estratégica: "Estrategista" (1ª ideia conectada aprovada) e "Visionário" (3 ideias aprovadas em orientações distintas) substituem "Apoiador". "Primeira Ideia", "Inovador do Mês" e "Impacto Real" permanecem.
- **D23 (2026-05-26):** Adicionada seção **"Impacto por Orientação"** no dashboard do líder (R-05.10) — mostra quais direcionamentos estratégicos estão virando resultado real (ideias, projetos, ROI por orientação).
- **D24 (2026-05-26):** Adicionado **modo apresentação** no dashboard (R-05.11) — botão que entra em fullscreen com KPIs animados (count-up). Cobre o critério "visibilidade prática e diferenciada dos resultados" do PDF e fica forte no vídeo demo.
- **D25 (2026-05-26):** Excluir ideia em SUBMETIDA reverte +10 base e +5 conexão (quando aplicável), totalizando -15 com clamp em 0 (R-03.11 + R-06.7).
- **D26 (2026-05-26):** Excluir orientação estratégica que tem ideias/projetos vinculados é **permitido**; entidades órfãs mostram "🎯 Orientação removida" no detalhe. Hard-delete simples.
- **D27 (2026-05-26):** Gestor pode cadastrar projeto **direto** sem partir de ideia (R-04.8); nesse caso escolhe orientação estratégica no cadastro (mesma lógica de R-03.14, opcional se não houver orientações).
- **D28 (2026-05-26):** Versões fixadas: Kotlin 2.3.20, AGP 9.2.0, Gradle 9.5.1, targetSdk 35, Compose BOM 2026.05.00, Firebase BOM 34.13.0, JDK 17, minSdk 24. Atualiza decisão original (D1/D2) que previa Kotlin 1.9 + targetSdk 34. **Why:** verificação em mai/2026 mostrou que Kotlin 1.9 está 3 versões majoras atrás do estável e targetSdk 34 não atende mais ao requisito do Google Play. **How to apply:** usar exatamente essas versões em `libs.versions.toml`. Kotlin 2.x usa o Compose Compiler Plugin oficial (`org.jetbrains.kotlin.plugin.compose`) — não setar `kotlinCompilerExtensionVersion` antigo.

### Adições de arquitetura (2026-05-26, sessão de polimento técnico)

- **D29 (2026-05-26):** Adotar **Hilt** (`com.google.dagger:hilt-android:2.55`) em vez de DI manual. **Why:** padrão da indústria Android, mais reconhecível na avaliação, gerenciamento automático de scopes (Singleton, ViewModelScoped). **How to apply:** `@HiltAndroidApp` na Application, `@AndroidEntryPoint` na MainActivity, `@HiltViewModel` nos ViewModels, `@Inject` constructor nos Repos. Substitui `AppContainer` manual.
- **D30 (2026-05-26):** Navegação **type-safe** via Navigation Compose 2.10 + kotlinx.serialization 1.8. Rotas como `@Serializable data object/data class` em vez de strings de path. **Why:** evita typos em runtime, args tipados, refactor-friendly. **How to apply:** sealed `Route` interface com objects/classes serializáveis; `composable<IdeaDetail> { entry -> entry.toRoute<IdeaDetail>() }`.
- **D31 (2026-05-26):** Configuração via **Gradle Version Catalog** (`gradle/libs.versions.toml`). Centraliza todas as versões num único TOML referenciado por `libs.xxx` nos `build.gradle.kts`. **Why:** padrão AGP 9+, organização clara, facilita atualizações em massa.
- **D32 (2026-05-26):** **SavedStateHandle** em todos os ViewModels de formulário (cadastrar ideia, cadastrar/editar projeto, editar orientação). Inputs persistem em `savedState["fieldName"]` via `StateFlow`. **Why:** sobrevive a process death e rotação de tela. Custo pequeno, experiência polida.
- **D33 (2026-05-26):** **Edge-to-edge** habilitado via `enableEdgeToEdge()` no `MainActivity.onCreate()` (alinhamento com targetSdk 35). **Predictive Back** habilitado via `android:enableOnBackInvokedCallback="true"` no Manifest. Compose Navigation lida com gesture automaticamente.
- **D34 (2026-05-26):** **Splash Screen API** (`androidx.core:core-splashscreen:1.0.1`). Chamada `installSplashScreen()` antes de `super.onCreate()`. Tema `Theme.InovaGab.Splash` com logo INOVAGAB. **Why:** acabamento profissional no primeiro contato; ótimo no vídeo demo.
- **D35 (2026-05-26):** **Firebase Crashlytics + Analytics** habilitados (apenas em release builds — desabilitados em debug via flag `BuildConfig.DEBUG`). Eventos custom: `idea_created` (com `has_guideline`), `idea_approved`, `project_completed` (com `roi_positive`), `dashboard_presentation_mode`. **Why:** mentalidade de produto, quase zero custo (vem no Firebase BOM).
- **D36 (2026-05-26):** **CompositionLocal `LocalSession`** expõe a sessão autenticada para qualquer Composable sem prop drilling. Provider no root da `MainActivity`. **Why:** código de UI mais limpo; reduz parâmetros em screens internos.
- **D37 (2026-05-26):** **`Flow.combine`** no `DashboardViewModel` para combinar reativamente `ideasFlow + projectsFlow + guidelinesFlow + filtersFlow`. Lógica de agregação extraída para objeto puro `DashboardComputer` (sem deps), facilmente testável.
- **D38 (2026-05-26):** **Mapper explícito Dto ↔ Domain.** `core/data/dto/*Dto.kt` espelham documentos Firestore com `@PropertyName`; `core/domain/model/*.kt` são models limpos sem dependência Firebase; `core/data/mapper/*.kt` converte. **Why:** isola Firestore da camada domain — Sprint 2 troca apenas DTOs/Mappers ao migrar para REST Java/C#. UI e ViewModels ficam intactos.
- **D39 (2026-05-26):** **`sealed interface DomainError`** com casos tipados (NotAuthenticated, NetworkUnavailable, Unauthorized, NotFound, ValidationFailed, FirestoreFailure, Unknown). Repos retornam `Outcome<T>` (wrapper próprio com `Success<T>`/`Failure(DomainError)`). UI faz `when` exaustivo. **Why:** elimina exceções genéricas, força tratamento explícito, mensagens pt-BR centralizadas em `DomainError.toPtBr()`.
- **D40 (2026-05-26):** **Estratégia de testes:** JUnit 4 + MockK + Turbine + kotlinx-coroutines-test. Cobertura obrigatória em `core/domain/` (≥ 70%) e nos ViewModels críticos: `DashboardComputerTest`, `IceTest`, `IdeasViewModelTest`, `ApproveIdeaUseCaseTest`, `CompleteProjectUseCaseTest`, `BadgeEvaluatorTest`, `MapperTest`. **Why:** peso 25% no critério qualidade de código exige testes; sem testes esse peso fica frágil.

## Blockers

- (nenhum no momento)

## Preferências

- Comunicação em português
- Decisões firmes em vez de menus de opções
- Foco em executar, não em discutir meta
