# Design — Sprint 1 INOVAGAB

Arquitetura, módulos, componentes e fluxos. Complementa `spec.md` (requisitos) com **como** vamos implementar.

## 1. Visão geral

App Android nativo Kotlin/Compose, single-module, arquitetura MVVM + Repository com DI via **Hilt**, persistência Firebase (Auth + Firestore + Crashlytics + Analytics), comunicação reativa via `StateFlow`. Navegação type-safe via Kotlin Serialization. Sem backend próprio no Sprint 1.

```
┌─────────────────────────────────────────────────────────┐
│                    UI (Compose)                          │
│  Screens · Components · Theme · Navigation (type-safe)   │
│  CompositionLocal: LocalSession                          │
└──────────────────────────────┬──────────────────────────┘
                               │ StateFlow / Events
┌──────────────────────────────▼──────────────────────────┐
│                    ViewModels (Hilt)                     │
│  SavedStateHandle · UiState<T> sealed                    │
└──────────────────────────────┬──────────────────────────┘
                               │ Result<T, DomainError>
┌──────────────────────────────▼──────────────────────────┐
│                  UseCases (quando justifica)             │
│  ApproveIdeaUseCase · CompleteProjectUseCase ...         │
└──────────────────────────────┬──────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────┐
│              Repositories (interface)                    │
│           ┌──────────────────┴──────────────┐            │
│           ▼                                 ▼            │
│   FirestoreRepository                 (Sprint 2: REST)   │
│   Mapper: Dto ⇄ Domain                                   │
└──────────────────────────────┬──────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────┐
│        Firebase Auth + Firestore + Crashlytics           │
└─────────────────────────────────────────────────────────┘
```

## 2. Princípios arquiteturais

| Princípio | Aplicação |
|---|---|
| **Single source of truth** | Firestore é a fonte; ViewModel mantém cache reativo via `StateFlow` |
| **Unidirecional** | UI emite eventos → ViewModel processa → atualiza state → UI observa |
| **Stateless screens** | Composables recebem `state` + `onEvent` por parâmetro; sem `remember` para dados de domínio |
| **Imutabilidade** | Models são `data class` com `val`, copy via `.copy()` |
| **Reatividade** | Firestore snapshot listeners → `callbackFlow` → `StateFlow` |
| **Fail fast no UI** | `UiState` é sealed; toda tela trata `Loading/Success/Error` explicitamente |
| **Erros tipados** | `DomainError` sealed → mapeado para mensagem pt-BR na UI |
| **DTO ≠ Domain** | Camada `data` tem `*Dto` espelhando Firestore; camada `domain` tem models puros; mapper explícito (`Dto.toDomain()`) |
| **Process death safe** | Forms guardam input em `SavedStateHandle`; sobrevivem à recriação do processo |

## 3. Estrutura de pacotes

Organização **feature-based**, single-module:

```
com.inovagab.app/
├── core/
│   ├── auth/                       # AuthRepository, SessionManager
│   ├── data/
│   │   ├── firestore/              # FirestoreSource (listeners, queries genéricas)
│   │   ├── dto/                    # *Dto data classes (mapeiam doc Firestore 1:1)
│   │   └── mapper/                 # IdeaDto.toDomain(), Project.toDto(), etc.
│   ├── di/                         # Hilt modules
│   │   ├── AppModule.kt
│   │   ├── FirebaseModule.kt
│   │   └── RepositoryModule.kt
│   ├── domain/
│   │   ├── model/                  # Idea, Project, User, Guideline (puros, sem Firebase)
│   │   ├── error/                  # sealed DomainError
│   │   └── usecase/                # Use cases não-triviais
│   ├── ui/
│   │   ├── theme/                  # InovaGabTheme, Colors, Typography
│   │   ├── components/             # Composables reutilizáveis
│   │   ├── state/                  # UiState<T> sealed
│   │   └── local/                  # CompositionLocals (LocalSession, etc.)
│   └── util/                       # Extensions, formatters (R$, %, datas)
│
├── feature/
│   ├── auth/                       # LoginScreen, LoginViewModel
│   ├── guidelines/                 # CRUD orientações
│   ├── ideas/                      # Ideias (operador + gestor)
│   ├── projects/                   # Projetos (gestor + leitura líder)
│   ├── dashboard/                  # Dashboard do líder
│   └── profile/                    # Perfil + badges + pontos
│
├── navigation/                     # Routes (sealed @Serializable), NavGraph
├── InovaGabApplication.kt          # @HiltAndroidApp + init Crashlytics
└── MainActivity.kt                 # @AndroidEntryPoint + enableEdgeToEdge + Splash
```

Cada `feature/X/` contém:
```
X/
├── data/
│   ├── XRepositoryImpl.kt          # impl da interface declarada em domain
│   └── XDto.kt                     # se houver DTO específico além dos de core/data/dto
├── ui/
│   ├── XScreen.kt                  # Composable
│   ├── XViewModel.kt               # @HiltViewModel
│   └── XState.kt                   # data class do state
└── domain/
    └── XRepository.kt              # interface
```

## 4. Camada de dados

### 4.1 Models — separação DTO vs Domain

```kotlin
// core/data/dto/IdeaDto.kt
data class IdeaDto(
    @get:PropertyName("title") val title: String = "",
    @get:PropertyName("description") val description: String = "",
    @get:PropertyName("category") val category: String = "",
    @get:PropertyName("division") val division: String = "",
    @get:PropertyName("guidelineId") val guidelineId: String? = null,
    @get:PropertyName("authorId") val authorId: String = "",
    @get:PropertyName("authorName") val authorName: String = "",
    @get:PropertyName("status") val status: String = "SUBMETIDA",
    @get:PropertyName("ice") val ice: Map<String, Any>? = null,
    @get:PropertyName("reviewerId") val reviewerId: String? = null,
    @get:PropertyName("reviewComment") val reviewComment: String? = null,
    @get:PropertyName("createdAt") val createdAt: Timestamp? = null,
    @get:PropertyName("reviewedAt") val reviewedAt: Timestamp? = null,
)

// core/domain/model/Idea.kt
data class Idea(
    val id: String,
    val title: String,
    val description: String,
    val category: String,
    val division: Division,
    val guidelineId: String?,
    val authorId: String,
    val authorName: String,
    val status: IdeaStatus,
    val ice: Ice?,
    val reviewerId: String?,
    val reviewComment: String?,
    val createdAt: Long,
    val reviewedAt: Long?,
)

// core/data/mapper/IdeaMapper.kt
fun IdeaDto.toDomain(id: String): Idea = Idea(
    id = id,
    title = title,
    description = description,
    category = category,
    division = Division.valueOf(division),
    guidelineId = guidelineId,
    authorId = authorId,
    authorName = authorName,
    status = IdeaStatus.valueOf(status),
    ice = ice?.let { Ice.fromMap(it) },
    reviewerId = reviewerId,
    reviewComment = reviewComment,
    createdAt = createdAt?.toDate()?.time ?: 0L,
    reviewedAt = reviewedAt?.toDate()?.time,
)

fun Idea.toDto(): IdeaDto = /* ... */
```

**Por quê:** isola o Firestore da camada domain. Sprint 2 troca apenas DTOs/Mappers para atender a API REST Java/C# — domain e UI ficam intactos.

### 4.2 DomainError sealed

```kotlin
// core/domain/error/DomainError.kt
sealed interface DomainError {
    data object NotAuthenticated : DomainError
    data object NetworkUnavailable : DomainError
    data class Unauthorized(val role: Role, val needed: Role) : DomainError
    data class NotFound(val entity: String, val id: String) : DomainError
    data class ValidationFailed(val field: String, val reason: String) : DomainError
    data class FirestoreFailure(val code: String, val message: String) : DomainError
    data class Unknown(val cause: Throwable) : DomainError
}

fun DomainError.toPtBr(): String = when (this) {
    is NotAuthenticated -> "Faça login para continuar"
    is NetworkUnavailable -> "Sem conexão. Verifique sua internet"
    is Unauthorized -> "Seu perfil não tem permissão para essa ação"
    is NotFound -> "Não encontramos o que você procura"
    is ValidationFailed -> "Campo $field: $reason"
    is FirestoreFailure -> "Erro ao salvar. Tente novamente"
    is Unknown -> "Erro inesperado. Tente novamente"
}
```

Repos retornam `Result<T, DomainError>` (via wrapper próprio `Outcome` ou `kotlin.Result` com `getOrElse`). ViewModel mapeia `Failure(error)` → `UiState.Error(error.toPtBr())`.

### 4.3 Repositories (interfaces em domain, impls em data)

```kotlin
// core/domain/IdeasRepository.kt (interface)
interface IdeasRepository {
    fun observeMyIdeas(userId: String): Flow<List<Idea>>
    fun observeForCuration(): Flow<List<Idea>>
    fun observeByGuideline(guidelineId: String): Flow<List<Idea>>
    suspend fun createIdea(input: CreateIdeaInput): Outcome<String>
    suspend fun updateIdea(id: String, input: UpdateIdeaInput): Outcome<Unit>
    suspend fun deleteIdea(id: String): Outcome<Unit>
    suspend fun saveIce(id: String, ice: Ice): Outcome<Unit>
    suspend fun approve(id: String, reviewerId: String): Outcome<String>     // retorna projectId
    suspend fun reject(id: String, reviewerId: String, comment: String): Outcome<Unit>
}

// feature/ideas/data/FirestoreIdeasRepository.kt
@Singleton
class FirestoreIdeasRepository @Inject constructor(
    private val firestore: FirebaseFirestore,
    private val usersRepo: UsersRepository,
) : IdeasRepository { /* impl */ }
```

**Padrão de leitura reativa** (Firestore → Flow):
```kotlin
override fun observeMyIdeas(userId: String): Flow<List<Idea>> = callbackFlow {
    val reg = firestore.collection("ideas")
        .whereEqualTo("authorId", userId)
        .orderBy("createdAt", Query.Direction.DESCENDING)
        .addSnapshotListener { snap, err ->
            if (err != null) { close(err); return@addSnapshotListener }
            trySend(snap?.documents.orEmpty().map { d -> d.toObject<IdeaDto>()!!.toDomain(d.id) })
        }
    awaitClose { reg.remove() }
}
```

**Operações transacionais críticas** (usam `firestore.runTransaction`):
- Criar ideia (R-03.1): cria documento + credita +10 (ou +15 se `guidelineId` não-nulo) ao autor + avalia badges
- Aprovar ideia (R-03.8): update ideia + create projeto (herdando `guidelineId`) + write update entry + credita +50 ao autor + avalia badges
- Salvar ICE (R-03.12): update ICE + transição SUBMETIDA→EM_ANALISE
- Excluir ideia (R-03.11): delete + reverter -10 (ou -15 se tinha conexão estratégica), com clamp em 0
- Projeto → CONCLUIDO (R-03.13): update projeto + update ideia origem para IMPLEMENTADA + credita +200 ao autor + avalia "Impacto Real"
- Atualizar projeto (R-04.5): update + criar entry em `updates/` com diff

## 5. Camada de UI

### 5.1 UiState sealed

```kotlin
// core/ui/state/UiState.kt
sealed interface UiState<out T> {
    data object Idle : UiState<Nothing>
    data object Loading : UiState<Nothing>
    data class Success<T>(val data: T) : UiState<T>
    data class Error(val message: String) : UiState<Nothing>
}
```

### 5.2 ViewModels com Hilt + SavedStateHandle

```kotlin
@HiltViewModel
class NewIdeaViewModel @Inject constructor(
    private val ideasRepo: IdeasRepository,
    private val guidelinesRepo: GuidelinesRepository,
    private val session: SessionManager,
    private val savedState: SavedStateHandle,
) : ViewModel() {

    // Inputs persistem em SavedStateHandle (sobrevivem a process death)
    val title: StateFlow<String> = savedState.getStateFlow("title", "")
    val description: StateFlow<String> = savedState.getStateFlow("description", "")
    val selectedGuidelineId: StateFlow<String?> = savedState.getStateFlow("guidelineId", null)

    fun onTitleChange(value: String) { savedState["title"] = value }
    fun onDescriptionChange(value: String) { savedState["description"] = value }

    fun submit() = viewModelScope.launch {
        val user = session.requireCurrent() ?: return@launch
        val input = CreateIdeaInput(
            title = title.value, description = description.value,
            guidelineId = selectedGuidelineId.value, /*...*/
        )
        when (val r = ideasRepo.createIdea(input)) {
            is Outcome.Success -> _events.emit(NewIdeaEvent.Created(r.value))
            is Outcome.Failure -> _events.emit(NewIdeaEvent.Failed(r.error.toPtBr()))
        }
    }
}
```

### 5.3 Combine reativo no Dashboard

```kotlin
@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val ideasRepo: IdeasRepository,
    private val projectsRepo: ProjectsRepository,
    private val guidelinesRepo: GuidelinesRepository,
) : ViewModel() {
    private val filters = MutableStateFlow(DashboardFilters.Default)
    fun setPeriod(p: Period) { filters.update { it.copy(period = p) } }
    fun setDivision(d: Division?) { filters.update { it.copy(division = d) } }

    val state: StateFlow<UiState<DashboardState>> = combine(
        ideasRepo.observeAll(),
        projectsRepo.observeAll(),
        guidelinesRepo.observeAll(),
        filters,
    ) { ideas, projects, guidelines, f ->
        UiState.Success(DashboardComputer.compute(ideas, projects, guidelines, f))
    }.catch { emit(UiState.Error("Erro ao montar dashboard")) }
     .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), UiState.Loading)
}
```

`DashboardComputer` é um objeto puro (sem deps) — fácil de testar com unit tests.

### 5.4 CompositionLocal para sessão

```kotlin
// core/ui/local/Session.kt
val LocalSession = compositionLocalOf<AuthSession?> { null }

// MainActivity
setContent {
    val session by sessionManager.currentUser.collectAsStateWithLifecycle()
    CompositionLocalProvider(LocalSession provides session) {
        InovaGabTheme { AppNavHost() }
    }
}

// Qualquer Composable
@Composable
fun ProfileScreen() {
    val user = LocalSession.current?.profile ?: return
    Text("Olá, ${user.name}")  // sem prop drilling
}
```

### 5.5 Componentes reutilizáveis (`core/ui/components`)

| Componente | Onde usa |
|---|---|
| `KpiCard` | Dashboard R-05.2 |
| `KpiCardAnimated` | Modo apresentação R-05.11 (count-up) |
| `FunnelChart` (Canvas) | Dashboard R-05.1 |
| `SparklineChart` (Canvas) | Dashboard R-05.3 |
| `GuidelineImpactCard` | Dashboard "Impacto por Orientação" R-05.10 |
| `JourneyStepper` (vertical) | Ideia detalhe R-03.9 |
| `IceMatrix` (3 sliders) | Curadoria gestor R-03.5 |
| `BadgeChip` | Perfil R-06.4 / R-06.5 |
| `RoleScaffold` | Layout base por perfil com bottom-nav adequada |
| `StatusBadge` | Lista de ideias (SUBMETIDA, APROVADA, etc.) |
| `EmptyState` | Listas vazias |
| `TimelineEntry` | Histórico de projeto R-04.5 |
| `CategoryAutoCompleteField` | Cadastro de ideia R-03.10 |
| `GuidelinePicker` (dropdown) | Cadastro de ideia/projeto R-03.14 / R-04.8 |
| `GuidelineBadge` | Cabeçalho do detalhe de ideia/projeto |

## 6. Navegação type-safe

Compose Navigation 2.8+ com Kotlin Serialization. Rotas são `@Serializable` data classes/objects — args tipados, refactor-safe.

```kotlin
// navigation/Routes.kt
@Serializable sealed interface Route

@Serializable data object Login : Route

// Operador
@Serializable data object MyIdeas : Route
@Serializable data object NewIdea : Route
@Serializable data class IdeaDetail(val id: String) : Route
@Serializable data object Guidelines : Route
@Serializable data object Profile : Route

// Gestor
@Serializable data object Curation : Route
@Serializable data object Projects : Route
@Serializable data class ProjectDetail(val id: String) : Route
@Serializable data object NewProject : Route

// Líder
@Serializable data object Dashboard : Route
@Serializable data object GuidelinesAdmin : Route
@Serializable data class GuidelineDrillDown(val id: String) : Route
```

Uso:
```kotlin
NavHost(navController, startDestination = Login) {
    composable<Login> { LoginScreen(onSuccess = { role -> navController.navigateToHome(role) }) }
    composable<IdeaDetail> { entry ->
        val args = entry.toRoute<IdeaDetail>()
        IdeaDetailScreen(ideaId = args.id)
    }
    /* ... */
}

navController.navigate(IdeaDetail(id = "abc"))    // type-safe
```

Estrutura visual por perfil (bottom navigation):

```
OPERADOR              GESTOR               LIDER
─────────────         ─────────────        ─────────────
🏠 Minhas ideias      🏠 Curadoria         🏠 Dashboard
🎯 Orientações        📋 Projetos          🎯 Orientações
👤 Perfil             🎯 Orientações       📋 Projetos
                      👤 Perfil            👤 Perfil
```

**Auth guard:** raiz observa `LocalSession`. Se `null` → força navegação para `Login`.

## 7. Tema

Material 3, single light theme, baseado na identidade Águia Branca.

### 7.1 Paleta (proposta — DS-1 aberto)

| Token | Cor | Uso |
|---|---|---|
| `primary` | #0B2A5B | Botões primários, app bar |
| `onPrimary` | #FFFFFF | Texto sobre primary |
| `secondary` | #C99A4A | Highlights, badges premium |
| `tertiary` | #4D8FB8 | Acentos secundários |
| `surface` | #FFFFFF | Cards, dialogs |
| `background` | #F5F6FA | Fundo de tela |
| `error` | #C73E3E | Erros, status REJEITADA |
| `success` | #2E8B57 | Status APROVADA, ROI positivo |
| `warning` | #E6A23C | Status EM_ANALISE |

Cores semânticas por status: SUBMETIDA neutral, EM_ANALISE warning, APROVADA/IMPLEMENTADA/CONCLUIDO success, REJEITADA/CANCELADO error, PLANEJAMENTO tertiary, EM_EXECUCAO warning.

### 7.2 Tipografia

Material 3 default (Roboto), com overrides:
- `displayLarge`: 36sp bold — KPIs do dashboard
- `headlineMedium`: 22sp semibold — Títulos de tela
- `titleMedium`: 16sp medium — Itens de lista
- `bodyMedium`: 14sp — Texto corrido
- `labelSmall`: 11sp medium — Badges, chips

## 8. Integração Firebase

### 8.1 Auth

```kotlin
class SessionManager @Inject constructor(
    private val auth: FirebaseAuth,
    private val usersRepo: UsersRepository,
) {
    val currentUser: StateFlow<AuthSession?> = /* callbackFlow + flatMapLatest */
}
```

### 8.2 Firestore

- Coleções top-level: `users`, `strategicGuidelines`, `ideas`, `projects`
- Subcoleção: `projects/{id}/updates`
- **Cache offline** ativo por padrão (Firestore SDK)
- Listeners gerenciados em `viewModelScope`; cancelados em `onCleared()`

### 8.3 Crashlytics + Analytics

```kotlin
@HiltAndroidApp
class InovaGabApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        Firebase.crashlytics.isCrashlyticsCollectionEnabled = !BuildConfig.DEBUG
        Firebase.analytics.setAnalyticsCollectionEnabled(!BuildConfig.DEBUG)
    }
}
```

Eventos custom rastreados via Analytics:
- `idea_created` (com `has_guideline` boolean)
- `idea_approved`
- `project_completed` (com `roi_positive` boolean)
- `dashboard_presentation_mode`

Crashlytics captura crashes automaticamente; usar `FirebaseCrashlytics.recordException()` em `Outcome.Failure(Unknown)` para visibilidade adicional.

### 8.4 Índices compostos necessários

| Coleção | Campos |
|---|---|
| `ideas` | `authorId ASC, createdAt DESC` |
| `ideas` | `status ASC, ice.score DESC` |
| `ideas` | `guidelineId ASC, createdAt DESC` |
| `projects` | `division ASC, updatedAt DESC` |
| `projects` | `guidelineId ASC, updatedAt DESC` |
| `projects` | `stage ASC, updatedAt DESC` |

### 8.5 Seed inicial

Script `bootstrap/SeedData.kt` executado **on demand** (long-press no logo da tela de login). Cria 3 usuários demo, 4 orientações, 6 ideias, 3 projetos com diferentes stages, atualizações nos projetos.

## 9. Tratamento de erros

| Camada | Estratégia |
|---|---|
| Repository | Retorna `Outcome<T>` ; converte exceções Firestore em `DomainError` específicos |
| UseCase | Propaga `Outcome` |
| ViewModel | Converte `Failure(error)` em `UiState.Error(error.toPtBr())` |
| UI | `when (state) { is Error -> ErrorScreen(message, onRetry) }` |
| Crashlytics | Erros `Unknown` registrados via `recordException()` |
| Sem conexão | Firestore listener emite cache; UI mostra banner sutil "Modo offline" |

## 10. Build system

### 10.1 Version Catalog (`gradle/libs.versions.toml`)

Estrutura central:
```toml
[versions]
kotlin = "2.3.20"
agp = "9.2.0"
compose-bom = "2026.05.00"
firebase-bom = "34.13.0"
hilt = "2.55"
nav = "2.10.0"
lifecycle = "2.9.0"
splashscreen = "1.0.1"
activity-compose = "1.10.0"
kotlinx-serialization = "1.8.0"

[libraries]
androidx-core-ktx = { module = "androidx.core:core-ktx", version = "1.15.0" }
androidx-activity-compose = { module = "androidx.activity:activity-compose", version.ref = "activity-compose" }
androidx-lifecycle-viewmodel = { module = "androidx.lifecycle:lifecycle-viewmodel-compose", version.ref = "lifecycle" }
androidx-navigation-compose = { module = "androidx.navigation:navigation-compose", version.ref = "nav" }
androidx-splashscreen = { module = "androidx.core:core-splashscreen", version.ref = "splashscreen" }
compose-bom = { module = "androidx.compose:compose-bom", version.ref = "compose-bom" }
compose-ui = { module = "androidx.compose.ui:ui" }
compose-material3 = { module = "androidx.compose.material3:material3" }
compose-material-icons-extended = { module = "androidx.compose.material:material-icons-extended" }
compose-ui-tooling-preview = { module = "androidx.compose.ui:ui-tooling-preview" }
firebase-bom = { module = "com.google.firebase:firebase-bom", version.ref = "firebase-bom" }
firebase-auth = { module = "com.google.firebase:firebase-auth-ktx" }
firebase-firestore = { module = "com.google.firebase:firebase-firestore-ktx" }
firebase-crashlytics = { module = "com.google.firebase:firebase-crashlytics-ktx" }
firebase-analytics = { module = "com.google.firebase:firebase-analytics-ktx" }
hilt-android = { module = "com.google.dagger:hilt-android", version.ref = "hilt" }
hilt-compiler = { module = "com.google.dagger:hilt-compiler", version.ref = "hilt" }
hilt-navigation-compose = { module = "androidx.hilt:hilt-navigation-compose", version = "1.3.0" }
kotlinx-serialization-json = { module = "org.jetbrains.kotlinx:kotlinx-serialization-json", version.ref = "kotlinx-serialization" }
# tests
junit = { module = "junit:junit", version = "4.13.2" }
mockk = { module = "io.mockk:mockk", version = "1.13.13" }
turbine = { module = "app.cash.turbine:turbine", version = "1.2.0" }
kotlinx-coroutines-test = { module = "org.jetbrains.kotlinx:kotlinx-coroutines-test", version = "1.10.1" }

[plugins]
android-application = { id = "com.android.application", version.ref = "agp" }
kotlin-android = { id = "org.jetbrains.kotlin.android", version.ref = "kotlin" }
kotlin-compose = { id = "org.jetbrains.kotlin.plugin.compose", version.ref = "kotlin" }
kotlin-serialization = { id = "org.jetbrains.kotlin.plugin.serialization", version.ref = "kotlin" }
hilt = { id = "com.google.dagger.hilt.android", version.ref = "hilt" }
ksp = { id = "com.google.devtools.ksp", version = "2.3.20-2.0.0" }
google-services = { id = "com.google.gms.google-services", version = "4.4.3" }
firebase-crashlytics = { id = "com.google.firebase.crashlytics", version = "3.0.3" }
```

### 10.2 MainActivity (edge-to-edge + Splash)

```kotlin
@AndroidEntryPoint
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()                    // androidx.core:core-splashscreen
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()                       // SDK 35 default
        setContent { InovaGabApp() }
    }
}
```

`AndroidManifest.xml` raiz da activity:
```xml
<activity
    android:name=".MainActivity"
    android:exported="true"
    android:enableOnBackInvokedCallback="true"
    android:theme="@style/Theme.InovaGab.Splash">
```

## 11. Estratégia de testes

Cobrir os pontos de maior risco (lógica de negócio):

### 11.1 Unit tests obrigatórios

| Suite | O que valida |
|---|---|
| `DashboardComputerTest` | Cálculo de funil, ROI consolidado, "Impacto por Orientação" sob diferentes filtros; edge case investimento = 0 |
| `IceTest` | `Ice.isComplete`, `score = i*c*e`, validação de ranges |
| `IdeasViewModelTest` | Cadastro credita pontos certos (+10 ou +15); deleção reverte com clamp em 0 |
| `ApproveIdeaUseCaseTest` | Aprovar cria projeto rascunho com `originatingIdeaId`, herda `guidelineId`, credita +50pts ao autor |
| `CompleteProjectUseCaseTest` | `stage = CONCLUIDO` → ideia origem vira IMPLEMENTADA + +200pts; idempotência |
| `BadgeEvaluatorTest` | Cada badge único, "Inovador do Mês" não repete, "Visionário" exige 3 orientações distintas |
| `MapperTest` | `IdeaDto.toDomain()` e volta — round-trip preserva dados |

### 11.2 Ferramentas
- **JUnit 4** — runner padrão
- **MockK** — fakes/mocks para repositories
- **Turbine** — coleta de Flow em testes
- **kotlinx-coroutines-test** — `runTest`, `TestDispatcher`

### 11.3 Estrutura
```
app/src/test/java/com/inovagab/app/
├── domain/
│   ├── DashboardComputerTest.kt
│   ├── IceTest.kt
│   └── usecase/
│       ├── ApproveIdeaUseCaseTest.kt
│       └── CompleteProjectUseCaseTest.kt
├── feature/
│   ├── ideas/IdeasViewModelTest.kt
│   └── dashboard/DashboardViewModelTest.kt
└── data/MapperTest.kt
```

Meta: ≥ 70% de cobertura em `core/domain/` e nos ViewModels listados.

## 12. Pontos abertos de design (precisam decisão antes do código)

| # | Ponto | Default que assumi |
|---|---|---|
| **DS-1** | Paleta exata de cores | Azul #0B2A5B + dourado — pode ajustar |
| **DS-2** | Logo do app | Texto "INOVAGAB" + ícone Material `Lightbulb` |
| **DS-3** | Fonte custom (Inter? Manrope?) | Roboto default (sem asset extra) |
| **DS-4** | Bottom-nav vs Drawer | Bottom-nav (mobile-first) |
| **DS-5** | Splash com animação custom | Padrão Android 12+ via `core-splashscreen` |
| **DS-7** | Material You (dynamic color) | Desabilitar (cores fixas da marca) |

## 13. Riscos e mitigações

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Avaliador não configurar Firebase → app crasha | Alta | Detectar `FirebaseApp.getApps()` vazio no `Application.onCreate` e logar erro amigável |
| Índices compostos não criados → query falha em runtime | Média | Pré-criar via `firestore.indexes.json`; documentar no `DOCUMENTACAO_TECNICA.md` |
| Transação distribuída falha em rede ruim | Baixa | `runTransaction` tem retry built-in; toast amigável se falhar |
| Pontuação inconsistente em writes concorrentes | Baixa | `FieldValue.increment(n)` em vez de read-modify-write |
| Listener vazando → memory leak | Média | `awaitClose { reg.remove() }` em todos os `callbackFlow` |
| Hilt + KSP build lento na primeira vez | Baixa | Aceitar; clean build CI ~1min |
| Process death em meio a cadastro | Média | `SavedStateHandle` preserva inputs do form |
