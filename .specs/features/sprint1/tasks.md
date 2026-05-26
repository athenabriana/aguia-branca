# Sprint 1 — Tasks

**Design**: `.specs/features/sprint1/design.md`
**Spec**: `.specs/features/sprint1/spec.md`
**Status**: Draft

Matriz de testes (greenfield — extraída da seção 11 do design):

| Camada | Tipo de teste | Comando |
|---|---|---|
| `core/domain/model/` (Ice) | unit | `./gradlew :app:testDebugUnitTest` |
| `core/domain/usecase/` | unit | `./gradlew :app:testDebugUnitTest` |
| `core/data/mapper/` | unit | `./gradlew :app:testDebugUnitTest` |
| Pure logic (`DashboardComputer`, `BadgeEvaluator`) | unit | `./gradlew :app:testDebugUnitTest` |
| ViewModels críticos (Ideas, Dashboard) | unit | `./gradlew :app:testDebugUnitTest` |
| Repositories Firestore | none (Sprint 1) | — |
| Composables / Screens | none (Sprint 1, validação manual) | — |

Gates:
- **quick**: `./gradlew :app:compileDebugKotlin` (compila)
- **full**: `./gradlew :app:testDebugUnitTest` (compila + unit tests)
- **build**: `./gradlew :app:assembleRelease` (APK release)

Paralelismo: todas as suítes unit são parallel-safe (sem I/O compartilhado, sem Android runtime).

---

## Execution Plan

### Fase 1 — Foundation (sequencial)

```
T01 → T02 → T03 → T04 → T05
                          ↓
         ┌────────────────┼────────────────┐
         ▼                ▼                ▼
        T06              T08             T11
         ↓                ↓                ↓
        T07              T09             T12
         ↓                ↓                ↓
       T10 (test)       T13              T14
                          ↓
                         T15
```

### Fase 2 — Auth (sequencial)

```
T15 → T16 → T17 → T18 → T19 → T20 → T21
```

### Fase 3 — Orientações (paralelo após auth)

```
T21 ─┬─→ T22 (repo)
     └─→ T23 (list screen) ──→ T24 (admin CRUD)
```

### Fase 4 — Ideias (sequencial com paralelismo interno)

```
T24 → T25 (IdeasRepository + createIdea tx)
       ↓
       ├─→ T26 [P] CategoryAutoComplete
       ├─→ T27 [P] GuidelinePicker
       ├─→ T28 [P] JourneyStepper
       ├─→ T29 [P] IceMatrix
       └─→ T30 [P] BadgeEvaluator (test)
              ↓
              T31 (NewIdeaScreen + VM + test) → T32 (MyIdeasScreen) → T33 (IdeaDetail)
              ↓
              T34 (CurationScreen + VM + ICE save tx)
              ↓
              T35 (ApproveIdeaUseCase + test) → T36 (RejectIdeaUseCase)
```

### Fase 5 — Projetos (sequencial após ideias)

```
T36 → T37 (ProjectsRepository + diff/history) → T38 [P] TimelineEntry
              ↓
              T39 (ProjectsListScreen) → T40 (ProjectDetailScreen + timeline)
              ↓
              T41 (NewProjectScreen) → T42 (ProjectEditScreen)
              ↓
              T43 (CompleteProjectUseCase + test)
```

### Fase 6 — Dashboard (sequencial)

```
T43 → T44 (DashboardComputer + test)
       ↓
       ├─→ T45 [P] FunnelChart
       ├─→ T46 [P] SparklineChart
       ├─→ T47 [P] KpiCard + KpiCardAnimated
       └─→ T48 [P] GuidelineImpactCard
              ↓
              T49 (DashboardViewModel + test) → T50 (DashboardScreen + filters + presentation mode)
```

### Fase 7 — Perfil + Ranking (paralelo após dashboard)

```
T50 ─┬─→ T51 [P] ProfileScreen
     └─→ T52 [P] RankingTop5 (home operador)
```

### Fase 8 — Entregáveis (sequencial)

```
T52 → T53 (Firestore indexes file) → T54 (Analytics events wiring) → T55 (Release APK) → T56 (Docs)
```

---

## Task Breakdown

### T01: Bootstrap Gradle + Version Catalog

**What**: Criar projeto Android base com Gradle Kotlin DSL e `libs.versions.toml` completo conforme design §10.1.
**Where**: `settings.gradle.kts`, `build.gradle.kts` (root), `app/build.gradle.kts`, `gradle/libs.versions.toml`, `gradle.properties`
**Depends on**: None
**Reuses**: design §10.1 (catálogo completo)
**Requirement**: D28, D31

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `libs.versions.toml` contém todas as versões da seção §10.1
- [ ] `app/build.gradle.kts` aplica plugins: android-application, kotlin-android, kotlin-compose, kotlin-serialization, hilt, ksp, google-services, firebase-crashlytics
- [ ] `compileSdk=35`, `minSdk=24`, `targetSdk=35`, JDK 17
- [ ] Dependências do Compose BOM e Firebase BOM sem versão individual
- [ ] Gate: `./gradlew :app:tasks` lista tasks sem erro

**Tests**: none
**Gate**: build

---

### T02: Firebase project setup

**What**: Adicionar `google-services.json` placeholder e configurar `firebase.indexes.json` vazio inicial. Documentar setup no README.
**Where**: `app/google-services.json` (placeholder com instrução), `firebase/firestore.indexes.json` (vazio inicial), `README.md` (passo de setup)
**Depends on**: T01
**Reuses**: —
**Requirement**: D2

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `google-services.json` presente (placeholder ou real); build não falha por falta dele
- [ ] `firestore.indexes.json` criado com `{ "indexes": [], "fieldOverrides": [] }`
- [ ] README documenta criação do projeto Firebase + download do json
- [ ] Gate: `./gradlew :app:assembleDebug` compila

**Tests**: none
**Gate**: build

---

### T03: InovaGabApplication (Hilt + Crashlytics)

**What**: Application class anotada com `@HiltAndroidApp`, inicializa Crashlytics e Analytics conforme design §8.3.
**Where**: `app/src/main/java/com/inovagab/app/InovaGabApplication.kt`, `AndroidManifest.xml` (registra `android:name`)
**Depends on**: T01, T02
**Reuses**: design §8.3
**Requirement**: D29, D35

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `@HiltAndroidApp class InovaGabApplication : Application()`
- [ ] `Firebase.crashlytics.isCrashlyticsCollectionEnabled = !BuildConfig.DEBUG`
- [ ] `Firebase.analytics.setAnalyticsCollectionEnabled(!BuildConfig.DEBUG)`
- [ ] Manifest aponta `android:name=".InovaGabApplication"`
- [ ] Gate: `./gradlew :app:compileDebugKotlin` compila

**Tests**: none
**Gate**: quick

---

### T04: MainActivity (Splash + edge-to-edge + Predictive Back)

**What**: Activity única `@AndroidEntryPoint` chamando `installSplashScreen()` antes de `super.onCreate()` e `enableEdgeToEdge()`. Manifest com `enableOnBackInvokedCallback="true"`.
**Where**: `app/src/main/java/com/inovagab/app/MainActivity.kt`, `AndroidManifest.xml`, `app/src/main/res/values/themes.xml` (`Theme.InovaGab.Splash`)
**Depends on**: T03
**Reuses**: design §10.2
**Requirement**: D33, D34

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `installSplashScreen()` chamado antes de `super.onCreate()`
- [ ] `enableEdgeToEdge()` no `onCreate`
- [ ] `Theme.InovaGab.Splash` configurado com windowSplashScreenAnimatedIcon
- [ ] Manifest: `android:theme="@style/Theme.InovaGab.Splash"`, `android:enableOnBackInvokedCallback="true"`
- [ ] Activity exibe placeholder `setContent { Text("InovaGab") }` para validar ciclo
- [ ] Verify: app abre, vê splash, depois tela placeholder com gestos predictive back funcionando

**Tests**: none
**Gate**: quick

---

### T05: Theme (cores + tipografia)

**What**: `InovaGabTheme` com paleta da §7.1 e tipografia da §7.2. Material You **desabilitado** (cores fixas).
**Where**: `core/ui/theme/InovaGabTheme.kt`, `Color.kt`, `Type.kt`
**Depends on**: T04
**Reuses**: design §7
**Requirement**: DS-1, DS-7

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `InovaGabTheme(content)` aplica `MaterialTheme` com `lightColorScheme` (sem dynamicColor)
- [ ] Tokens semânticos extras (success, warning) expostos via extension `MaterialTheme.semantic`
- [ ] Tipografia override conforme §7.2
- [ ] MainActivity envolve `setContent { InovaGabTheme { ... } }`
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T06: Domain enums

**What**: Enums puros do domínio.
**Where**: `core/domain/model/Enums.kt` (Role, Division, IdeaStatus, ProjectStage, Pillar, Period)
**Depends on**: T01
**Reuses**: spec — modelo de dados
**Requirement**: R-01.3, R-02.4, R-03.3, R-04.1, R-05.9

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `Role { OPERADOR, GESTOR, LIDER }`
- [ ] `Division { PASSAGEIROS, COMERCIO, LOGISTICA, CORPORATIVO }`
- [ ] `IdeaStatus { SUBMETIDA, EM_ANALISE, APROVADA, REJEITADA, IMPLEMENTADA }`
- [ ] `ProjectStage { PLANEJAMENTO, EM_EXECUCAO, CONCLUIDO, CANCELADO }`
- [ ] `Pillar { DIRECIONAMENTO, IDEIAS, PROJETOS, MENSURACAO }`
- [ ] `Period { THIS_MONTH, LAST_QUARTER, THIS_YEAR, ALL }`
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T07: Domain models + IceTest

**What**: Data classes do domínio (`User`, `Guideline`, `Idea`, `Project`, `ProjectUpdate`, `Ice`, `AuthSession`). `Ice` inclui `isComplete`, `score`, validação. Acompanha `IceTest`.
**Where**: `core/domain/model/User.kt`, `Guideline.kt`, `Idea.kt`, `Project.kt`, `Ice.kt`, `AuthSession.kt`; `app/src/test/.../domain/IceTest.kt`
**Depends on**: T06
**Reuses**: spec — modelo de dados
**Requirement**: R-03.5

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Todos modelos como `data class` com `val`
- [ ] `Ice(impact, confidence, ease)` com `val score = impact * confidence * ease`
- [ ] `Ice.isComplete: Boolean = impact in 1..10 && confidence in 1..10 && ease in 1..10`
- [ ] `IceTest`: cobre score correto, isComplete false para 0 ou 11, true para [1,10] em todos
- [ ] Gate: `./gradlew :app:testDebugUnitTest --tests "*IceTest*"` passa (≥4 testes)

**Tests**: unit
**Gate**: full

---

### T08: DomainError sealed + Outcome wrapper

**What**: `sealed interface DomainError` com casos + extensão `toPtBr()`; `sealed interface Outcome<out T>` com `Success` / `Failure(DomainError)`.
**Where**: `core/domain/error/DomainError.kt`, `core/domain/error/Outcome.kt`
**Depends on**: T06
**Reuses**: design §4.2
**Requirement**: D39

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `DomainError` com 7 casos do design §4.2
- [ ] `DomainError.toPtBr()` cobre todos os casos via `when` exaustivo
- [ ] `Outcome<T>` com `Success<T>(val value: T)` e `Failure(val error: DomainError)`
- [ ] Helper `Outcome.runCatching { ... }` mapeia `Exception` → `Unknown(cause)`
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T09: DTOs

**What**: `*Dto` classes espelhando documentos Firestore com `@PropertyName` e defaults.
**Where**: `core/data/dto/UserDto.kt`, `GuidelineDto.kt`, `IdeaDto.kt`, `ProjectDto.kt`, `ProjectUpdateDto.kt`
**Depends on**: T06
**Reuses**: design §4.1
**Requirement**: D38

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Cada DTO tem construtor com defaults vazios (exigência do Firestore)
- [ ] Campos com `@PropertyName("camelCase")` quando difere do nome Kotlin
- [ ] `IdeaDto` inclui `ice: Map<String, Any>?` (não `Ice` direto — convertido no mapper)
- [ ] `ProjectUpdateDto.changes: List<Map<String, Any>>`
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T10: Mappers + MapperTest

**What**: Funções `Dto.toDomain(id)` e `Domain.toDto()` para User, Guideline, Idea, Project, ProjectUpdate. Inclui round-trip test.
**Where**: `core/data/mapper/UserMapper.kt`, `GuidelineMapper.kt`, `IdeaMapper.kt`, `ProjectMapper.kt`; `app/src/test/.../data/MapperTest.kt`
**Depends on**: T07, T09
**Reuses**: design §4.1
**Requirement**: D38

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Mapper IdeaDto↔Idea preserva todos os campos (incl. `ice` map ↔ `Ice` object)
- [ ] Mapper trata `Timestamp` nulo como 0L em `createdAt`
- [ ] `MapperTest` cobre round-trip Idea→Dto→Idea com `ice` preenchido e null (≥5 testes — 1 por entidade)
- [ ] Gate: `./gradlew :app:testDebugUnitTest --tests "*MapperTest*"` passa

**Tests**: unit
**Gate**: full

---

### T11: UiState sealed

**What**: `sealed interface UiState<out T>` com Idle/Loading/Success/Error.
**Where**: `core/ui/state/UiState.kt`
**Depends on**: T06
**Reuses**: design §5.1
**Requirement**: D39

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 4 variantes definidas conforme §5.1
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T12: LocalSession CompositionLocal

**What**: `LocalSession = compositionLocalOf<AuthSession?> { null }` provido na root.
**Where**: `core/ui/local/LocalSession.kt`
**Depends on**: T07
**Reuses**: design §5.4
**Requirement**: D36

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `LocalSession` declarado e exportado
- [ ] Helper `@Composable fun requireSession(): AuthSession` faz check + throws se null
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T13: Navigation Routes (type-safe)

**What**: Sealed `Route` interface + todos os objects/data classes `@Serializable` do design §6.
**Where**: `navigation/Routes.kt`
**Depends on**: T01
**Reuses**: design §6
**Requirement**: D30

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `@Serializable sealed interface Route`
- [ ] Todas as 14 rotas do design §6 declaradas (Login, MyIdeas, NewIdea, IdeaDetail(id), Guidelines, Profile, Curation, Projects, ProjectDetail(id), NewProject, Dashboard, GuidelinesAdmin, GuidelineDrillDown(id), e adicionar EditProject(id))
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T14: AppNavHost com auth guard

**What**: `AppNavHost(navController)` configura `NavHost(startDestination = Login)` e adiciona efeito que redireciona para Login quando `LocalSession.current == null`.
**Where**: `navigation/AppNavHost.kt`
**Depends on**: T11, T12, T13
**Reuses**: design §6 (auth guard)
**Requirement**: R-01.6

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `composable<Login>` placeholder retorna `Text("Login")`
- [ ] `LaunchedEffect(session)` observa session e força `navigate(Login) { popUpTo(0) }` se null em rota não-Login
- [ ] MainActivity chama `AppNavHost(rememberNavController())` dentro do `CompositionLocalProvider(LocalSession provides session)`
- [ ] Gate: compila + abrir app vai para Login

**Tests**: none
**Gate**: quick

---

### T15: Hilt modules

**What**: `AppModule` (provides app-level singletons), `FirebaseModule` (provides FirebaseAuth, FirebaseFirestore, Crashlytics, Analytics).
**Where**: `core/di/AppModule.kt`, `core/di/FirebaseModule.kt`
**Depends on**: T03
**Reuses**: design §3, §8
**Requirement**: D29

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `@Module @InstallIn(SingletonComponent::class) object FirebaseModule` com `@Provides @Singleton fun provideAuth/Firestore/Crashlytics/Analytics`
- [ ] `RepositoryModule` declarado vazio (preenchido nas tasks de feature via `@Binds`)
- [ ] Gate: compila e KSP gera código Hilt sem erro

**Tests**: none
**Gate**: quick

---

### T16: SessionManager

**What**: Classe que expõe `currentUser: StateFlow<AuthSession?>` combinando `FirebaseAuth.authStateChanges()` com `usersRepo.observe(uid)`.
**Where**: `core/auth/SessionManager.kt`
**Depends on**: T15, T17 (interface), T12
**Reuses**: design §8.1
**Requirement**: R-01.3, R-01.4

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Implementação com `callbackFlow` para auth state + `flatMapLatest` para usersRepo
- [ ] `currentUser` é `StateFlow<AuthSession?>` (Hot, com `SharingStarted.WhileSubscribed`)
- [ ] `suspend fun signOut()` chama `auth.signOut()`
- [ ] `@Singleton @Inject constructor(auth, usersRepo)`
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T17: UsersRepository

**What**: Interface em `domain/` + impl Firestore em `data/` lendo/escrevendo `users/{uid}`.
**Where**: `core/domain/UsersRepository.kt`, `feature/auth/data/FirestoreUsersRepository.kt`
**Depends on**: T10, T15
**Reuses**: design §4.3
**Requirement**: R-01.3, R-06.5

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Interface: `fun observe(uid: String): Flow<User?>`, `suspend fun ensureProfileExists(uid, email, role, division): Outcome<Unit>`, `suspend fun topByPointsThisMonth(limit: Int): Outcome<List<User>>`
- [ ] Impl Firestore usa `addSnapshotListener` para observe; `FieldValue.increment` para deltas de pontos (helper privado `creditPoints(uid, delta)`)
- [ ] `@Binds` no `RepositoryModule`
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T18: LoginViewModel

**What**: ViewModel Hilt com `email`, `password` em `SavedStateHandle`; `submit()` chama `auth.signInWithEmailAndPassword`. Emite `UiState<Role>` em sucesso.
**Where**: `feature/auth/ui/LoginViewModel.kt`, `feature/auth/ui/LoginState.kt`
**Depends on**: T16, T11
**Reuses**: design §5.2
**Requirement**: R-01.1, R-01.2

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Inputs em `SavedStateHandle`
- [ ] `submit()` converte exceptions do FirebaseAuth em `DomainError` (credenciais inválidas → `ValidationFailed`, rede → `NetworkUnavailable`)
- [ ] Estado expõe `UiState<Role>` (Success carrega Role para redirecionar)
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T19: LoginScreen

**What**: Composable com TextFields email/senha, botão "Entrar", erro inline. Long-press no logo dispara seed (R-T20).
**Where**: `feature/auth/ui/LoginScreen.kt`
**Depends on**: T18, T05
**Reuses**: T05 theme
**Requirement**: R-01.1

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Email TextField com `KeyboardType.Email`; senha com `VisualTransformation.Password`
- [ ] Botão "Entrar" desabilitado se email vazio OU senha < 6 chars
- [ ] Estado `Loading` mostra `CircularProgressIndicator` no botão
- [ ] Estado `Error` exibe `Text(message, color = error)`
- [ ] Logo INOVAGAB com `combinedClickable(onLongClick = { vm.runSeed() })`
- [ ] Verify manual: login com credencial demo válida navega para home do role

**Tests**: none
**Gate**: quick

---

### T20: SeedData script

**What**: `bootstrap/SeedData.kt` cria 3 usuários demo (Auth + users/), 4 orientações, 6 ideias com diversidade de status/guidelineId, 3 projetos com diferentes stages e updates. Idempotente.
**Where**: `bootstrap/SeedData.kt`
**Depends on**: T17, demais repositórios (será chamado pelo LoginViewModel mesmo antes deles existirem — pode usar Firestore direto)
**Reuses**: spec — Credenciais de demonstração
**Requirement**: spec seed table

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Cria emails `lider@inovagab.com`, `gestor@inovagab.com`, `operador@inovagab.com` com senha `inovagab123`
- [ ] Documentos `users/{uid}` com role/division corretos
- [ ] Idempotente: rerun não duplica (checa se user já existe via `fetchSignInMethodsForEmail`)
- [ ] Toast/Snackbar "Seed concluído" ao terminar
- [ ] Verify manual: long-press no logo, login com `lider@` funciona

**Tests**: none
**Gate**: quick

---

### T21: RoleScaffold

**What**: Composable que define bottom navigation por Role conforme design §6. Recebe `currentRoute` + `onNavigate`. Inclui rota inicial pós-login.
**Where**: `core/ui/components/RoleScaffold.kt`, atualiza `AppNavHost.kt` para usá-lo
**Depends on**: T13, T14, T05
**Reuses**: design §6 (estrutura visual por perfil)
**Requirement**: R-01.6

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Layout: `Scaffold(bottomBar = { NavigationBar })` com 3 itens (operador), 4 (gestor), 4 (líder)
- [ ] Cada item de gestor/operador tem ícone Material + label
- [ ] Login bem-sucedido navega para: OPERADOR→MyIdeas, GESTOR→Curation, LIDER→Dashboard (limpando back stack)
- [ ] Tentativa de acessar rota fora do role do usuário redireciona para home do role
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T22: GuidelinesRepository

**What**: Interface + impl Firestore (CRUD em `strategicGuidelines`).
**Where**: `core/domain/GuidelinesRepository.kt`, `feature/guidelines/data/FirestoreGuidelinesRepository.kt`
**Depends on**: T10, T15
**Reuses**: design §4.3
**Requirement**: R-02

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Interface: `observeAll(): Flow<List<Guideline>>`, `observe(id): Flow<Guideline?>`, `create/update/delete: suspend Outcome<...>`
- [ ] Impl com `orderBy("createdAt", DESC)`, snapshot listeners
- [ ] `@Binds` no `RepositoryModule`
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T23: GuidelinesScreen (read-only)

**What**: Tela de lista usada por todos os perfis. Líder vê FAB "Nova" + ações editar/remover por item; demais veem só leitura.
**Where**: `feature/guidelines/ui/GuidelinesScreen.kt`, `GuidelinesViewModel.kt`
**Depends on**: T22, T21
**Reuses**: T21 RoleScaffold
**Requirement**: R-02.1, R-02.2, R-02.3

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `LazyColumn` com cards: título, pillar chip, autor, "atualizada em DD/MM"
- [ ] FAB "Nova orientação" visível só se `LocalSession.role == LIDER`
- [ ] Cada card tem trailing icon "⋮" (apenas líder) abrindo Edit/Delete
- [ ] EmptyState quando lista vazia: "Nenhuma orientação cadastrada"
- [ ] Verify manual: login com cada role mostra/oculta corretamente

**Tests**: none
**Gate**: quick

---

### T24: GuidelinesAdmin (criar/editar/deletar)

**What**: Tela de formulário usada pelo líder para CRUD. ViewModel com `SavedStateHandle`.
**Where**: `feature/guidelines/ui/GuidelinesAdminScreen.kt`, `GuidelinesAdminViewModel.kt`
**Depends on**: T23
**Reuses**: T22
**Requirement**: R-02.2, R-02.4, R-02.5

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Form: título, descrição, dropdown Pillar
- [ ] Inputs em `SavedStateHandle`
- [ ] Botão "Salvar" desabilitado se título vazio
- [ ] Delete pede confirmação (`AlertDialog`) e é hard-delete
- [ ] Após salvar/deletar, navega de volta para lista
- [ ] Verify manual: líder cria → item aparece no topo; líder edita → updatedAt sobe; líder deleta → some

**Tests**: none
**Gate**: quick

---

### T25: IdeasRepository + createIdea transaction

**What**: Interface + impl com **transação** Firestore para `createIdea`: cria doc em `ideas/`, credita +10 ao autor (ou +15 se `guidelineId != null`), avalia badges "Primeira Ideia" e "Inovador do Mês".
**Where**: `core/domain/IdeasRepository.kt`, `feature/ideas/data/FirestoreIdeasRepository.kt`, `feature/ideas/data/IdeasInputs.kt` (CreateIdeaInput, UpdateIdeaInput)
**Depends on**: T22 (precisa GuidelinesRepo para validar guidelineId), T30 (BadgeEvaluator)
**Reuses**: design §4.3
**Requirement**: R-03.1, R-03.14, R-06.1, R-06.1.1, R-06.4

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Interface completa do design §4.3 + helper `observeByGuideline(id): Flow<List<Idea>>`
- [ ] `createIdea` usa `firestore.runTransaction`: cria ideia + `users/{uid}.points += 10` (ou 15) via `FieldValue.increment` + evalúa badges
- [ ] `deleteIdea` (apenas SUBMETIDA): apaga + `points` decrementa com clamp em 0 via transação (lê points, calcula `max(0, p - delta)`, escreve)
- [ ] Idempotência: badge só adiciona se ainda não tem
- [ ] Gate: compila

**Tests**: none (cobertura via T35 ApproveIdeaUseCaseTest e T31 IdeasViewModelTest)
**Gate**: quick

---

### T26: CategoryAutoCompleteField [P]

**What**: Composable que faz autocomplete de categoria buscando prefixos em `ideas` (case-insensitive). Normaliza trim + primeira maiúscula.
**Where**: `core/ui/components/CategoryAutoCompleteField.kt`
**Depends on**: T25
**Reuses**: T05 theme
**Requirement**: R-03.10

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `OutlinedTextField` com dropdown de sugestões ao digitar ≥ 2 chars
- [ ] Busca via Firestore `whereGreaterThanOrEqualTo("category", prefix)` + filtro client-side case-insensitive (limit 20)
- [ ] Dedup de sugestões já idênticas
- [ ] Validação: 2-40 chars, normalizada
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T27: GuidelinePicker [P]

**What**: Dropdown de orientações vigentes, com handling do caso "sem orientações". Mostra "🎯 Conectada com: [título]" quando selecionado.
**Where**: `core/ui/components/GuidelinePicker.kt`
**Depends on**: T22
**Reuses**: T05 theme
**Requirement**: R-03.14, R-04.8

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Recebe `guidelines: List<Guideline>`, `selectedId: String?`, `onSelect: (String?) -> Unit`
- [ ] Lista vazia: mostra texto "Nenhuma orientação cadastrada ainda" e dropdown desabilitado
- [ ] Lista preenchida: dropdown com itens; permite limpar seleção
- [ ] Item selecionado renderiza badge "🎯 Conectada com: [título]" abaixo
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T28: JourneyStepper [P]

**What**: Composable vertical com 5 estágios. Recebe `currentStage` + `dates: Map<Stage, Long?>`. Estágio "Rejeitada" substitui stepper por mensagem.
**Where**: `core/ui/components/JourneyStepper.kt`
**Depends on**: T05
**Reuses**: —
**Requirement**: R-03.9

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 5 estágios: Submetida, Em análise, Aprovada, Em execução, Resultado obtido
- [ ] Alcançado: ícone ✅ verde + data formatada DD/MM/YY
- [ ] Pendente: ícone ○ cinza, sem data
- [ ] Rejeitada: mostra "❌ Ideia rejeitada em DD/MM" + `reviewComment`
- [ ] Linha vertical conectando ícones
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T29: IceMatrix [P]

**What**: Composable com 3 sliders 1-10 + score calculado em tempo real. Callback `onSave(Ice)` só dispara se `isComplete`.
**Where**: `core/ui/components/IceMatrix.kt`
**Depends on**: T07
**Reuses**: T07 Ice model
**Requirement**: R-03.5

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 3 `Slider` com `valueRange = 1f..10f`, steps 9 (inteiros)
- [ ] Score `Impact × Confidence × Ease` exibido grande embaixo
- [ ] Botão "Salvar avaliação" habilitado **apenas** se `Ice.isComplete`
- [ ] Recebe `initial: Ice?` para edição
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T30: BadgeEvaluator + test [P]

**What**: Objeto puro `BadgeEvaluator` com função `evaluate(user: User, ideas: List<Idea>, projects: List<Project>): Set<String>` retornando badges desbloqueáveis. Unit tests.
**Where**: `core/domain/badge/BadgeEvaluator.kt`, `app/src/test/.../domain/BadgeEvaluatorTest.kt`
**Depends on**: T07
**Reuses**: spec — R-06.4
**Requirement**: R-06.4, R-06.7

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Lógica:
  - "Primeira Ideia" se `ideas.any { it.authorId == user.id }`
  - "Estrategista" se existe ideia do user com `guidelineId != null` E `status ∈ {APROVADA, IMPLEMENTADA}`
  - "Inovador do Mês" se existe mês calendário com ≥5 ideias do user E user ainda não tem o badge
  - "Impacto Real" se existe ideia user com `status == IMPLEMENTADA`
  - "Visionário" se ≥3 ideias aprovadas do user em `guidelineId` distintos
- [ ] Retorna apenas badges **novas** (que `user.badges` ainda não tem)
- [ ] `BadgeEvaluatorTest` cobre cada badge (≥7 testes: cada caso positivo + não-repetição "Inovador do Mês" + clamp Visionário em < 3 distintos)
- [ ] Gate: `./gradlew :app:testDebugUnitTest --tests "*BadgeEvaluatorTest*"` passa

**Tests**: unit
**Gate**: full

---

### T31: NewIdeaScreen + ViewModel + IdeasViewModelTest

**What**: Tela de cadastro de ideia (operador) + VM com `SavedStateHandle`. Test cobre pontos creditados +10/+15 e clamp em deleção.
**Where**: `feature/ideas/ui/NewIdeaScreen.kt`, `NewIdeaViewModel.kt`; `app/src/test/.../feature/ideas/IdeasViewModelTest.kt`
**Depends on**: T25, T26, T27
**Reuses**: design §5.2
**Requirement**: R-03.1, R-03.10, R-03.14, R-06.1, R-06.1.1

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Form: título, descrição, CategoryAutoCompleteField, dropdown Division (pré-selecionado com `session.division`), GuidelinePicker
- [ ] Inputs em `SavedStateHandle` (process death safe)
- [ ] `submit()` chama `ideasRepo.createIdea` e navega para `MyIdeas` em sucesso
- [ ] Toast "+15 pts (10 + 5 conexão estratégica)" / "+10 pts" conforme guideline preenchido
- [ ] `IdeasViewModelTest`: cria ideia c/ guideline → credita +15; sem guideline → +10; delete em SUBMETIDA → reverte -15/-10 com clamp em 0 (fake repo)
- [ ] Gate: `./gradlew :app:testDebugUnitTest --tests "*IdeasViewModelTest*"` passa (≥3 testes)

**Tests**: unit
**Gate**: full

---

### T32: MyIdeasScreen

**What**: Lista das ideias do operador (apenas próprias), com status badge e timestamp. Tap navega para detalhe.
**Where**: `feature/ideas/ui/MyIdeasScreen.kt`, `MyIdeasViewModel.kt`
**Depends on**: T25, T21
**Reuses**: T21 RoleScaffold
**Requirement**: R-03.2

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `LazyColumn` ordenado por `createdAt` DESC
- [ ] Cada item: título, StatusBadge, "submetida em DD/MM/YY", GuidelineBadge se aplicável
- [ ] FAB "+ Nova ideia" navega para `NewIdea`
- [ ] EmptyState: "Você ainda não cadastrou ideias"
- [ ] Verify manual: cadastrar ideia → aparece no topo

**Tests**: none
**Gate**: quick

---

### T33: IdeaDetailScreen

**What**: Detalhe da ideia visível ao autor (com JourneyStepper, edit/delete em SUBMETIDA) e ao gestor (com IceMatrix + aprovar/rejeitar).
**Where**: `feature/ideas/ui/IdeaDetailScreen.kt`, `IdeaDetailViewModel.kt`
**Depends on**: T25, T28, T29
**Reuses**: T28, T29
**Requirement**: R-03.7, R-03.9, R-03.11, R-03.14

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Topo: GuidelineBadge ou "🎯 Orientação removida"
- [ ] Body: título, descrição, categoria, divisão
- [ ] Se `currentUser.role == OPERADOR && authorId == currentUser.id`:
  - Mostra JourneyStepper com `dates` derivadas de `createdAt`, `reviewedAt`, etc.
  - Se `status == SUBMETIDA`: botões Editar/Excluir (com AlertDialog de confirmação)
- [ ] Se `currentUser.role == GESTOR && status ∈ {SUBMETIDA, EM_ANALISE}`:
  - Mostra IceMatrix (preenche com `idea.ice` se existir)
  - Botões "Aprovar" e "Rejeitar" (rejeitar abre dialog com TextField obrigatório)
- [ ] Status REJEITADA: stepper substituído por mensagem
- [ ] Verify manual: percorrer fluxos como cada role

**Tests**: none
**Gate**: quick

---

### T34: CurationScreen + ICE save (transição SUBMETIDA→EM_ANALISE)

**What**: Lista do gestor ordenada por ICE score DESC; "Aguardando avaliação" (SUBMETIDA sem ICE) ao fim. `saveIce` em transação atualiza ICE + transiciona status.
**Where**: `feature/ideas/ui/CurationScreen.kt`, `CurationViewModel.kt`; atualiza `FirestoreIdeasRepository.saveIce` para transação
**Depends on**: T33, T25
**Reuses**: T25 repository
**Requirement**: R-03.4, R-03.5, R-03.6, R-03.12

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Lista: ideias com `status ∈ {SUBMETIDA, EM_ANALISE}`; sort por `ice.score` DESC; sem ICE no fim agrupado
- [ ] Tap em item navega para `IdeaDetail(id)`
- [ ] `saveIce` em transação: lê doc, escreve `ice` + se `status == SUBMETIDA` define `status = EM_ANALISE`
- [ ] Verify manual: gestor salva ICE → status vira EM_ANALISE → operador não consegue editar mais

**Tests**: none
**Gate**: quick

---

### T35: ApproveIdeaUseCase + test

**What**: Use case que em transação: atualiza idea para APROVADA, cria projeto rascunho herdando `guidelineId`/`division`, credita +50 ao autor, escreve primeira entry em `projects/{id}/updates`, avalia badge "Estrategista".
**Where**: `core/domain/usecase/ApproveIdeaUseCase.kt`; `app/src/test/.../domain/usecase/ApproveIdeaUseCaseTest.kt`
**Depends on**: T25, T30, T37 (precisa criar entry em updates — coordenar com ProjectsRepo; aqui inline a transação para evitar circular dep)
**Reuses**: design §4.3
**Requirement**: R-03.7, R-03.8

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Use case `suspend operator fun invoke(ideaId, reviewerId): Outcome<String>` (retorna projectId criado)
- [ ] Em **uma** transação: update ideia + create projeto com `originatingIdeaId`, `creatorManagerId`, `guidelineId`, `stage=PLANEJAMENTO`, `title="PROJ: "+idea.title` + create `projects/{id}/updates/{auto}` com note "Criado automaticamente a partir da ideia: {título}" + increment +50 no autor + evalua badge
- [ ] `ApproveIdeaUseCaseTest`: mocks de FirebaseFirestore (MockK) verificam ordem dos writes, herança correta de `guidelineId`, idempotência (re-approve não duplica) — ≥3 testes
- [ ] Gate: `./gradlew :app:testDebugUnitTest --tests "*ApproveIdeaUseCaseTest*"` passa

**Tests**: unit
**Gate**: full

---

### T36: RejectIdeaUseCase

**What**: Use case que atualiza ideia para REJEITADA com `reviewComment` obrigatório.
**Where**: `core/domain/usecase/RejectIdeaUseCase.kt`
**Depends on**: T25
**Reuses**: T25 repo
**Requirement**: R-03.7

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Use case valida `comment.isNotBlank()` retornando `Failure(ValidationFailed("comment", "obrigatório"))` se vazio
- [ ] Update Firestore: `status=REJEITADA, reviewerId, reviewComment, reviewedAt=serverTimestamp`
- [ ] Sem créditos de pontos
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T37: ProjectsRepository + history diff logic

**What**: Interface + impl. Atualização de projeto roda em transação: calcula diff entre snapshot atual e novo input, escreve `projects/{id}` + entry em `projects/{id}/updates` com diff.
**Where**: `core/domain/ProjectsRepository.kt`, `feature/projects/data/FirestoreProjectsRepository.kt`
**Depends on**: T22 (para validação guidelineId), T10
**Reuses**: design §4.3
**Requirement**: R-04.1 a R-04.7

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Interface: `observeAll/observe/create/update/delete/observeUpdates(projectId)`
- [ ] `create`: cria projeto + primeira entry "Projeto criado"
- [ ] `update` em transação: lê doc atual, calcula `changes: List<{field, from, to}>` para campos modificados, escreve novo doc + nova entry com diff
- [ ] `observeUpdates` retorna Flow ordenado por `createdAt` DESC
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T38: TimelineEntry [P]

**What**: Composable de uma entrada de timeline (data/hora, autor, nota opcional, lista de diffs formatada "Investimento: R$ 100k → R$ 120k").
**Where**: `core/ui/components/TimelineEntry.kt`
**Depends on**: T05
**Reuses**: —
**Requirement**: R-04.5

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Renderiza: header (autor + data), nota se não vazia, lista de diffs com formatação amigável por tipo (R$, %, datas, texto)
- [ ] Bullet vertical à esquerda conectando entries
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T39: ProjectsListScreen

**What**: Lista de projetos para gestor (com FAB "Novo") e líder (read-only, sem FAB).
**Where**: `feature/projects/ui/ProjectsListScreen.kt`, `ProjectsListViewModel.kt`
**Depends on**: T37, T21
**Reuses**: T21
**Requirement**: R-04.2, R-04.3, R-04.4

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Lista ordenada por `updatedAt` DESC
- [ ] Cada item: título, StatusBadge (stage), GuidelineBadge se aplicável, "atualizado em DD/MM"
- [ ] FAB "+ Novo projeto" só se `role == GESTOR`
- [ ] Operador acessando → redireciona para sua home
- [ ] Verify manual: 3 roles veem corretamente

**Tests**: none
**Gate**: quick

---

### T40: ProjectDetailScreen

**What**: Detalhe do projeto: cabeçalho com KPIs (investimento, retorno, ROI), GuidelineBadge, link para `originatingIdeaId`, timeline de updates. Botões Editar/Deletar só para gestor.
**Where**: `feature/projects/ui/ProjectDetailScreen.kt`, `ProjectDetailViewModel.kt`
**Depends on**: T39, T38
**Reuses**: T38 TimelineEntry
**Requirement**: R-04.5

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Cabeçalho: título, stage, KPIs (4 cards inline)
- [ ] Se `originatingIdeaId != null`: chip clicável "Originada da ideia: [título]"
- [ ] GuidelineBadge ou "🎯 Orientação removida"
- [ ] Timeline vertical com `TimelineEntry` por update (mais recente primeiro)
- [ ] Botões Editar/Deletar (gestor only); líder vê apenas leitura
- [ ] Verify manual: editar campo gera nova entry no topo

**Tests**: none
**Gate**: quick

---

### T41: NewProjectScreen (direct creation)

**What**: Form de criação direta de projeto pelo gestor (R-04.8). Inclui GuidelinePicker.
**Where**: `feature/projects/ui/NewProjectScreen.kt`, `NewProjectViewModel.kt`
**Depends on**: T37, T27
**Reuses**: T27 GuidelinePicker
**Requirement**: R-04.8

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Form: título, descrição, stage (default PLANEJAMENTO), statusText, investimento, prazo (DatePicker), divisão, GuidelinePicker
- [ ] Inputs em `SavedStateHandle`
- [ ] Submit cria projeto via `projectsRepo.create` e navega para `ProjectDetail(id)`
- [ ] Verify manual: gestor cria → aparece na lista com 1 entry no histórico ("Projeto criado")

**Tests**: none
**Gate**: quick

---

### T42: ProjectEditScreen

**What**: Form de edição do projeto. Cada save gera entry no histórico via T37.
**Where**: `feature/projects/ui/ProjectEditScreen.kt`, `ProjectEditViewModel.kt`
**Depends on**: T41
**Reuses**: T41 form components
**Requirement**: R-04.2, R-04.5

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Mesmo form do New, pré-populado com projeto atual
- [ ] Campo opcional "Nota desta atualização"
- [ ] Save calcula diff via T37 e escreve update entry
- [ ] Stage `CONCLUIDO` dispara T43 use case (chamado pelo VM)
- [ ] Verify manual: editar investimento de 100→120 gera entry "Investimento: R$ 100 → R$ 120"

**Tests**: none
**Gate**: quick

---

### T43: CompleteProjectUseCase + test

**What**: Use case que em transação: ao mudar `stage = CONCLUIDO`, atualiza `ideas/{originatingIdeaId}.status = IMPLEMENTADA` (se existe) + credita +200 ao autor da ideia + avalia badge "Impacto Real". Idempotente.
**Where**: `core/domain/usecase/CompleteProjectUseCase.kt`; `app/src/test/.../domain/usecase/CompleteProjectUseCaseTest.kt`
**Depends on**: T37, T30
**Reuses**: design §4.3
**Requirement**: R-03.13, R-06.3

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Use case `suspend operator fun invoke(projectId): Outcome<Unit>`
- [ ] Lê projeto, se `originatingIdeaId == null` ou ideia já IMPLEMENTADA → no-op (success)
- [ ] Senão transação: update ideia + +200pts + badge
- [ ] `CompleteProjectUseCaseTest`: cobre (a) projeto sem ideia origem → no-op, (b) projeto com ideia → credita, (c) projeto com ideia já IMPLEMENTADA → no-op (idempotência) — ≥3 testes
- [ ] Gate: `./gradlew :app:testDebugUnitTest --tests "*CompleteProjectUseCaseTest*"` passa

**Tests**: unit
**Gate**: full

---

### T44: DashboardComputer + test

**What**: Objeto puro `DashboardComputer.compute(ideas, projects, guidelines, filters): DashboardState`. Calcula funil, KPIs, sparkline (janela fixa 6 meses), "Impacto por Orientação", lista de projetos.
**Where**: `core/domain/dashboard/DashboardComputer.kt`, `DashboardState.kt`, `DashboardFilters.kt`; `app/src/test/.../domain/DashboardComputerTest.kt`
**Depends on**: T07
**Reuses**: spec R-05
**Requirement**: R-05.1 a R-05.10

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Funil de 5 estágios com contagens proporcionais (R-05.1)
- [ ] ROI consolidado = `(Σ retornoFinanceiro − Σ investimento) / Σ investimento * 100`; quando Σ investimento = 0 → `null` (UI mostra "—") (R-05.5, R-05.6)
- [ ] Sparkline 6 buckets mensais por `projects.updatedAt`; ignora meses com investimento 0; aplica filtro de **divisão** mas não de período (R-05.3)
- [ ] "Impacto por Orientação": agrega ideias e projetos por `guidelineId`, calcula ROI por orientação, ordena DESC, orientações sem projeto ao fim (R-05.10)
- [ ] Filtros (período + divisão) aplicados ao funil/KPIs/lista
- [ ] `DashboardComputerTest`: cobre funil sob diferentes datasets, ROI consolidado, edge case investimento=0, sparkline com <6 meses, filtros aplicados — ≥6 testes
- [ ] Gate: `./gradlew :app:testDebugUnitTest --tests "*DashboardComputerTest*"` passa

**Tests**: unit
**Gate**: full

---

### T45: FunnelChart Canvas [P]

**What**: Composable Canvas que desenha 5 barras horizontais proporcionais ao stage com mais itens. Anima entrada (barras crescem da esquerda) controlável via prop.
**Where**: `core/ui/components/FunnelChart.kt`
**Depends on**: T05
**Reuses**: —
**Requirement**: R-05.1, R-05.11

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Recebe `stages: List<Pair<String, Int>>` (5 pares)
- [ ] Cada barra largura = `count / maxCount * widthPx`, com label e valor à direita
- [ ] Cores semânticas por estágio
- [ ] `animateOnAppear: Boolean = false` → quando true, barras crescem via `animateFloatAsState`
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T46: SparklineChart Canvas [P]

**What**: Canvas com 6 pontos conectados por linha. Pula meses com `null` (investimento=0).
**Where**: `core/ui/components/SparklineChart.kt`
**Depends on**: T05
**Reuses**: —
**Requirement**: R-05.3, R-05.6

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Recebe `values: List<Float?>` (6 pontos, null aceitável)
- [ ] Desenha linha + dots; segmentos com null em extremidade são interrompidos
- [ ] Label do mês corrente em destaque
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T47: KpiCard + KpiCardAnimated [P]

**What**: `KpiCard(label, value)` padrão + `KpiCardAnimated(label, targetValue, duration)` com efeito count-up via `animateFloatAsState` de 0 a target em 1.5s.
**Where**: `core/ui/components/KpiCard.kt`
**Depends on**: T05
**Reuses**: —
**Requirement**: R-05.2, R-05.11

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] KpiCard com label small + value grande
- [ ] KpiCardAnimated com `LaunchedEffect` que dispara animation ao montar; suporta valor "—" sem animar
- [ ] Formatadores aceitam R$ (R-05.5), % (1 casa decimal), inteiros
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T48: GuidelineImpactCard [P]

**What**: Card listando título da orientação, contagem de ideias, contagem de projetos, ROI %. Tap dispara drill-down.
**Where**: `core/ui/components/GuidelineImpactCard.kt`
**Depends on**: T05
**Reuses**: —
**Requirement**: R-05.10

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Layout: título no topo, 3 KPIs inline na base
- [ ] `onClick` propagado
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T49: DashboardViewModel + DashboardViewModelTest

**What**: ViewModel com `Flow.combine` de ideas/projects/guidelines/filters → chama `DashboardComputer`. Filtros mutáveis. Test cobre integração ViewModel ↔ Computer com fakes.
**Where**: `feature/dashboard/ui/DashboardViewModel.kt`; `app/src/test/.../feature/dashboard/DashboardViewModelTest.kt`
**Depends on**: T25, T37, T22, T44
**Reuses**: design §5.3
**Requirement**: R-05.8, R-05.9

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `state: StateFlow<UiState<DashboardState>>` usando `combine` + `stateIn`
- [ ] `setPeriod(p)`, `setDivision(d)`, `togglePresentation()` mutáveis no `filtersFlow`
- [ ] Test usa Turbine para validar que ao mudar filtro, novo state emite (≥2 testes)
- [ ] Gate: `./gradlew :app:testDebugUnitTest --tests "*DashboardViewModelTest*"` passa

**Tests**: unit
**Gate**: full

---

### T50: DashboardScreen + filtros + modo apresentação

**What**: Tela do líder com filtros no topo (chips), FunnelChart, KPIs em grid 2 col, sparkline, seção "Impacto por Orientação", lista de projetos. Botão "▶ Apresentar" entra em fullscreen.
**Where**: `feature/dashboard/ui/DashboardScreen.kt`
**Depends on**: T45, T46, T47, T48, T49
**Reuses**: todos os components da fase 6
**Requirement**: R-05.1 a R-05.11

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Filtros: 2 rows de `FilterChip` (período + divisão)
- [ ] Botão "▶ Apresentar" no topo direito
- [ ] Modo apresentação: `LocalView.current.windowInsetsController.hide(systemBars())`, KpiCards trocam para KpiCardAnimated, funil anima entrada
- [ ] Tap em qualquer lugar sai do modo apresentação
- [ ] Nenhuma seção usa `Row(...)` simulando tabela (R-05.7)
- [ ] Verify manual: filtro Logística reduz funil/KPIs/Impacto/lista; sparkline imune ao período mas reage à divisão; modo apresentação anima

**Tests**: none
**Gate**: quick

---

### T51: ProfileScreen [P]

**What**: Tela de perfil com pontos totais, badges desbloqueadas como chips coloridos, contagem de ideias por status do usuário, botão Logout.
**Where**: `feature/profile/ui/ProfileScreen.kt`, `ProfileViewModel.kt`, `core/ui/components/BadgeChip.kt`, `StatusBadge.kt`
**Depends on**: T17, T25, T21
**Reuses**: T21 RoleScaffold
**Requirement**: R-06.5

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Header: avatar placeholder + nome + role
- [ ] KPI grande: pontos totais
- [ ] LazyRow horizontal de BadgeChips (com ícone + nome); desabilitados/grayscale se ainda não conquistado
- [ ] Contagem de ideias por status (5 rows simples)
- [ ] Botão Logout chama `sessionManager.signOut()`
- [ ] Verify manual: cadastrar 5 ideias num mês → badge "Inovador do Mês" aparece colorido

**Tests**: none
**Gate**: quick

---

### T52: RankingTop5 [P]

**What**: Composable mostrando top 5 operadores por pontos do mês corrente, usado na home do operador (`MyIdeasScreen`).
**Where**: `core/ui/components/RankingTop5.kt`; atualiza T32 para incluir o componente
**Depends on**: T17, T32
**Reuses**: T17 UsersRepository.topByPointsThisMonth
**Requirement**: R-06.6

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Card no topo de `MyIdeasScreen` com 5 linhas: posição, nome, pontos
- [ ] Carrega de `usersRepo.topByPointsThisMonth(5)`
- [ ] EmptyState se nenhum operador ainda pontuou
- [ ] Verify manual: ranking aparece na home; operador logado destacado

**Tests**: none
**Gate**: quick

---

### T53: Firestore indexes file

**What**: Preencher `firebase/firestore.indexes.json` com os 6 índices compostos do design §8.4.
**Where**: `firebase/firestore.indexes.json`, README com instrução de deploy
**Depends on**: T02
**Reuses**: design §8.4
**Requirement**: —

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] 6 índices declarados conforme tabela §8.4
- [ ] README documenta `firebase deploy --only firestore:indexes`
- [ ] Verify manual: queries do app não logam warning "needs index"

**Tests**: none
**Gate**: build

---

### T54: Analytics events wiring

**What**: Disparar 4 eventos custom (design §8.3) nos pontos certos: `idea_created` (T25), `idea_approved` (T35), `project_completed` (T43), `dashboard_presentation_mode` (T50).
**Where**: cria `core/util/Analytics.kt` helper; atualiza 4 sites de chamada
**Depends on**: T25, T35, T43, T50
**Reuses**: design §8.3
**Requirement**: D35

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Helper `Analytics.logIdeaCreated(hasGuideline: Boolean)` etc.
- [ ] Cada evento disparado no fluxo correspondente
- [ ] Crashlytics `recordException` chamado em `Outcome.Failure(Unknown)` nos repos
- [ ] Gate: compila

**Tests**: none
**Gate**: quick

---

### T55: Release APK build (signed)

**What**: Configurar keystore release, `signingConfigs.release`, `buildTypes.release { minify=false, signingConfig = release }`. Documentar geração.
**Where**: `app/build.gradle.kts`, `keystore.properties` (gitignored), README
**Depends on**: T54
**Reuses**: —
**Requirement**: entregável #1

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] `keystore.properties` (template no README) com `storeFile`, `storePassword`, `keyAlias`, `keyPassword`
- [ ] `signingConfigs.release` lê do properties
- [ ] `./gradlew :app:assembleRelease` produz `app-release.apk` assinado
- [ ] APK instala em dispositivo/emulador e abre
- [ ] Gate: `./gradlew :app:assembleRelease`

**Tests**: none
**Gate**: build

---

### T56: Documentação técnica

**What**: Markdown técnico cobrindo: arquitetura (com diagrama), stack, modelo de dados, fluxos principais (sequência aprovar ideia, completar projeto), instruções de build/seed/deploy. Convertível para PDF.
**Where**: `docs/DOCUMENTACAO_TECNICA.md`
**Depends on**: T55
**Reuses**: PROJECT.md + design.md como input
**Requirement**: entregável #3

**Tools**: MCP: NONE · Skill: NONE

**Done when**:
- [ ] Seções: Visão, Stack, Arquitetura (com mermaid), Modelo de Dados, Fluxos Críticos (R-03.8, R-03.13, R-05.10), Build, Seed, Testes, Limitações conhecidas
- [ ] Conversível via `pandoc DOCUMENTACAO_TECNICA.md -o doc.pdf`
- [ ] Documentado: como rodar testes, gerar APK, configurar Firebase

**Tests**: none
**Gate**: —

---

## Granularity Check

| Task | Scope | Status |
|---|---|---|
| T01 Bootstrap Gradle | 1 config file set | ✅ |
| T07 Domain models + IceTest | 6 small data classes + Ice logic + test | ✅ (cohesive, mesmo pacote) |
| T10 Mappers + MapperTest | 5 mappers + roundtrip test | ✅ (cohesive) |
| T25 IdeasRepository + createIdea tx | 1 repo file, 1 transação | ✅ (uma interface + uma impl) |
| T31 NewIdeaScreen + VM + test | 1 screen + 1 VM + test | ⚠️ borderline mas cohesive |
| T35 ApproveIdeaUseCase + test | 1 use case + test | ✅ |
| T44 DashboardComputer + test | 1 pure object + test | ✅ |
| T50 DashboardScreen | 1 screen com filtros + modo apresentação | ⚠️ grande mas único arquivo |

Tasks borderline (T31, T50) mantidas por coesão — separar VM de Screen criaria churn.

## Diagram-Definition Cross-Check

| Task | Depends on (body) | Diagram shows | Status |
|---|---|---|---|
| T01 | None | None | ✅ |
| T02 | T01 | T01 | ✅ |
| T03 | T01, T02 | T02 | ✅ |
| T04 | T03 | T03 | ✅ |
| T05 | T04 | T04 | ✅ |
| T06 | T01 | (fanout após T05) | ✅ |
| T07 | T06 | T06→T07 | ✅ |
| T08 | T06 | (paralelo a T07) | ✅ |
| T09 | T06 | (paralelo) | ✅ |
| T10 | T07, T09 | T07→T10, T09→T10 | ✅ |
| T11 | T06 | (paralelo) | ✅ |
| T12 | T07 | T11→T12 | ✅ |
| T13 | T01 | (paralelo) | ✅ |
| T14 | T11, T12, T13 | T13→T14 | ✅ |
| T15 | T03 | T14→T15 | ✅ |
| T16 | T15, T17, T12 | T15→T16 | ✅ |
| T17 | T10, T15 | (paralelo a T16, ambos antes de T18) | ✅ |
| T18 | T16, T11 | T17→T18 | ✅ |
| T19 | T18, T05 | T18→T19 | ✅ |
| T20 | T17 | T19→T20 | ✅ |
| T21 | T13, T14, T05 | T20→T21 | ✅ |
| T22 | T10, T15 | T21→T22 | ✅ |
| T23 | T22, T21 | T22→T23 | ✅ |
| T24 | T23 | T23→T24 | ✅ |
| T25 | T22, T30 | T24→T25, e T30 [P] | ✅ |
| T26-T29 [P] | T25 (T26), T22 (T27), T05 (T28), T07 (T29) | T25→{T26,T27,T28,T29,T30} | ✅ |
| T30 [P] | T07 | (paralelo na fase 4) | ✅ |
| T31 | T25, T26, T27 | T26+T27→T31 | ✅ |
| T32 | T25, T21 | T31→T32 | ✅ |
| T33 | T25, T28, T29 | T32→T33 | ✅ |
| T34 | T33, T25 | T33→T34 | ✅ |
| T35 | T25, T30, T37 | T34→T35 + T37 dep (out-of-phase) | ⚠️ ver nota |
| T36 | T25 | T35→T36 | ✅ |
| T37 | T22, T10 | T36→T37 | ✅ |
| T38-T43 | (ver tasks) | (fase 5 sequencial) | ✅ |
| T44 | T07 | T43→T44 | ✅ |
| T45-T48 [P] | T05 | T44→{T45..T48} | ✅ |
| T49 | T25, T37, T22, T44 | (depois dos componentes) | ✅ |
| T50 | T45-T49 | T49→T50 | ✅ |
| T51-T52 [P] | T17/T25/T32 | T50→{T51,T52} | ✅ |
| T53-T56 | sequencial | T52→T53→T54→T55→T56 | ✅ |

**Nota T35→T37:** ApproveIdeaUseCase escreve em `projects/{id}/updates`. Para evitar dep circular, T35 escreve a primeira entry inline na sua transação (sem chamar ProjectsRepo); T37 já estará criado quando rodarmos T35 porque a fase 5 começa depois de T36. **Resolução:** mover T37 para antes de T35. **Ajuste aplicado:** T35 depende de T37 (entrei na cross-check). Fase 4 termina em T34; T37 vira primeira task da fase 5; depois T35→T36 fecham a fase 4 logicamente, mas a ordem real do diagrama é: T34 → T37 → T35 → T36 → T38… (sequencial entre fases 4 e 5).

A ordem real, corrigida:

```
… T34 → T37 → T35 → T36 → T38 → T39 → T40 → T41 → T42 → T43 → T44 → …
```

## Test Co-location Validation

| Task | Code layer | Matrix requires | Task says | Status |
|---|---|---|---|---|
| T07 Domain models + Ice | domain/model | unit (Ice) | unit | ✅ |
| T08 DomainError + Outcome | domain/error | none | none | ✅ |
| T10 Mappers | data/mapper | unit | unit | ✅ |
| T17 UsersRepository | data | none (Sprint 1) | none | ✅ |
| T22 GuidelinesRepository | data | none | none | ✅ |
| T25 IdeasRepository | data | none | none | ✅ |
| T30 BadgeEvaluator | pure logic | unit | unit | ✅ |
| T31 NewIdeaScreen + VM | VM crítico | unit | unit (IdeasViewModelTest) | ✅ |
| T35 ApproveIdeaUseCase | domain/usecase | unit | unit | ✅ |
| T37 ProjectsRepository | data | none | none | ✅ |
| T43 CompleteProjectUseCase | domain/usecase | unit | unit | ✅ |
| T44 DashboardComputer | pure logic | unit | unit | ✅ |
| T49 DashboardViewModel | VM crítico | unit | unit | ✅ |
| Demais Screens | UI | none | none | ✅ |

Cobertura mínima alcançada: ≥7 suítes obrigatórias do design §11.1 — todas mapeadas para tasks com test co-located.

---

## Resumo

- **56 tasks** distribuídas em **8 fases**
- **7 suítes de unit test** co-localizadas (IceTest, MapperTest, BadgeEvaluatorTest, IdeasViewModelTest, ApproveIdeaUseCaseTest, CompleteProjectUseCaseTest, DashboardComputerTest, DashboardViewModelTest)
- **9 tasks marcadas `[P]`** em pontos de fanout (fase 4 components, fase 6 charts, fase 7 perfil/ranking)
- Cada task tem 1 deliverable atômico, Done When binário, gate verificável
- Toda task rastreável a R-XX (spec) ou D-XX (decisões)
