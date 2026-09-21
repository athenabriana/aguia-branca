# Sprint 2 — Tasks

**Design**: `.specs/features/sprint2/design.md`
**Spec**: `.specs/features/sprint2/spec.md`
**Status**: Draft

Convenção de IDs: **B** = backend (`/backend`) · **M** = mobile (`/mobile`) · **D** = documentação/entregáveis.

## Matriz de testes (greenfield backend + evolução do app)

| Camada | Tipo de teste | Comando |
|---|---|---|
| `Domain` (entidades, VOs, regras, BadgeEvaluator) | unit | `dotnet test --filter "Category!=Integration"` |
| `Application` (handlers, validators, ReportCalculator, prompt builder) | unit | idem |
| `Infrastructure` (repositórios, transações, índices, stores Identity) | **integração** (Testcontainers Mongo rs) | `dotnet test --filter "Category=Integration"` |
| `Infrastructure/Ai` (GeminiClient) | unit com `HttpMessageHandler` fake | `dotnet test --filter "Category!=Integration"` |
| `Api` (matriz de autorização, fluxos, ProblemDetails) | integração `WebApplicationFactory` + Mongo | `dotnet test --filter "Category=Integration"` |
| Mobile: mappers, badges, ice | unit (existentes, atualizados) | `./gradlew :app:testDebugUnitTest` |
| Mobile: `Remote*Repository`, `TokenAuthenticator`, `ProblemDetailsMapper` | unit com **MockWebServer** | `./gradlew :app:testDebugUnitTest` |
| Mobile: Composables/Screens | none (validação manual E2E) | — |

Gates:
- **quick (backend)**: `dotnet build backend/AguiaBranca.sln -warnaserror`
- **unit (backend)**: `dotnet test backend/AguiaBranca.sln --filter "Category!=Integration"`
- **full (backend)**: `dotnet test backend/AguiaBranca.sln` (requer Docker)
- **quick (mobile)**: `cd mobile && ./gradlew :app:compileDebugKotlin`
- **full (mobile)**: `cd mobile && ./gradlew :app:testDebugUnitTest`
- **build (mobile)**: `cd mobile && ./gradlew :app:assembleRelease`

Paralelismo: suítes unit são parallel-safe. Suítes de integração usam contêiner próprio por classe/fixture (não compartilham banco).

**Ambiente:** a máquina tem SDKs .NET 6/7/9 e Docker, **sem SDK 8**. Antes da B01: instalar o SDK 8 *ou* rodar `dotnet` via contêiner `mcr.microsoft.com/dotnet/sdk:8.0`. Sem `mongod` local → Mongo sempre via Docker.

Cobertura-alvo: Domain + Application ≥ 80%; regras críticas (pontos, badges, aprovação, conclusão, ROI, matriz de autorização) 100%.

---

## Execution Plan

### Fase 0 — Fundação e spike (sequencial)

```
B01 → B02 → B03 (spike: go/no-go)
```

### Fase 1 — Núcleo (sequencial)

```
B03 → B04 (Domain) → B05 (Application common) → B06 (Persistência) → B07 (Api base)
```

### Fase 2 — Autenticação (sequencial)

```
B07 → B08 (Identity stores) → B09 (JWT + refresh + endpoints auth) → B10 (policies + seed + harness authz)
```

### Fase 3 — Orientações

```
B10 → B11 (Guidelines CRUD + histórico)
```

### Fase 4 — Ideias e gamificação

```
B11 → B12 (Gamificação: pontos + badges + ranking)
       → B13 (Ideias CRUD + pontos)
       → B14 (ICE + rejeitar + aprovar → projeto; usa entidade/repo de projeto de B04/B06)
```

### Fase 5 — Projetos

```
B14 → B15 (Projetos CRUD + histórico diff) → B16 (Conclusão → ideia IMPLEMENTADA + 200pts)
```

### Fase 6 — Relatórios e IA

```
B16 → B17 (ReportCalculator + golden) → B18 (endpoints /reports)
B10 → B19 [P] (GeminiClient)  ── (independente das features; pode rodar em paralelo desde a Fase 2)
B18 + B19 → B20 (Insights: prompt, cache, cota, endpoint)
```

### Fase 7 — Fechamento do backend

```
B20 → B21 (hardening) → B22 (matriz authz completa + fluxo E2E) → B23 [P] (Migrator) 
B22 → B24 (Docker, README, deploy)
```

### Trilha Mobile (paralela ao backend, ancorada nos contratos do spec + Swagger)

```
B09 ──► M01 → M02 → M03 (auth/sessão)
B11 ──► M04 (orientações)
B14 ──► M05 (ideias)
B16 ──► M06 (projetos)
B18 ──► M07 (dashboard)          B12 ──► M08 (ranking/perfil) [P]
B20 ──► M09 (card Insights IA)
M03..M09 → M10 (remoção Firebase + testes) → M11 (E2E no emulador + APK)
```

### Fase Final — Entregáveis

```
B24 + M11 → D01 (ENDPOINTS.md) → D02 (diagrama + IA) → D03 (apresentação) → D04 (empacotamento)
```

---

## Task Breakdown — Backend

### B01: Scaffold da solution .NET 8  ✅ concluída

**What**: Criar `backend/AguiaBranca.sln` com 4 projetos de `src/` e 4 de `tests/`, referências conforme design §3, Central Package Management, `.editorconfig`, `global.json`, `.gitignore`.
**Where**: `backend/AguiaBranca.sln`, `backend/src/AguiaBranca.{Api,Application,Domain,Infrastructure}`, `backend/tests/AguiaBranca.*.Tests`, `backend/Directory.Build.props`, `backend/Directory.Packages.props`, `backend/global.json`
**Depends on**: —
**Reuses**: design §3
**Requirement**: R2-10.1, R2-10.9

**Done when**:
- [x] `net8.0`, `Nullable=enable`, `ImplicitUsings`, `TreatWarningsAsErrors=true` em `Directory.Build.props`
- [x] Referências: Api→Application+Infrastructure; Infrastructure→Application+Domain; Application→Domain; Domain→∅ (verificado por teste de arquitetura simples que falha se Domain referenciar Application/Infra)
- [x] Versões estáveis mais recentes compatíveis com .NET 8 fixadas em `Directory.Packages.props` (JwtBearer, FluentValidation, Serilog.AspNetCore, Swashbuckle, MongoDB.EntityFrameworkCore, Microsoft.Extensions.Http.Resilience, xUnit, NSubstitute, AwesomeAssertions, Testcontainers.MongoDb, coverlet)
- [x] `global.json` fixa SDK `8.0.x` (`rollForward: latestFeature`)
- [x] `.gitignore` cobre `bin/ obj/ .env *.user secrets`
- [x] Gate: `dotnet build backend/AguiaBranca.sln -warnaserror` sem erro

**Notas de execução**: SDK 8.0.425 selecionado pelo `global.json`. `dotnet new` falha no sandbox (sem escrita em `~/.templateengine`) → `.sln` criado à mão + `dotnet sln add`. NuGet exige `NUGET_HTTP_CACHE_PATH` gravável (sandbox). Versões: EF Core/JwtBearer/Identity 8.0.31, `MongoDB.EntityFrameworkCore` 8.4.4, `MongoDB.Driver` 3.11.2, Resilience 8.10.0, FluentValidation 12.1.1, xUnit 2.9.3 (runner 2.8.2), Testcontainers.MongoDb 4.15.0. `NU1900–NU1904` (auditoria) não quebram o build.
**Tests**: unit (teste de arquitetura de referências — 4 testes)
**Gate**: quick

---

### B02: Docker Compose (Mongo replica set) + configuração tipada  ✅ concluída

**What**: `docker-compose.yml` com Mongo 7 em replica set de 1 nó (init automático) e serviço da API; `appsettings` conforme design §13; Options com `ValidateOnStart()`.
**Where**: `backend/docker-compose.yml`, `backend/.env.example`, `backend/src/AguiaBranca.Api/appsettings*.json`, `Infrastructure/Configuration/*Options.cs`
**Depends on**: B01
**Reuses**: design §13, §15
**Requirement**: R2-10.6, R2-10.8

**Done when**:
- [x] `docker compose up mongo` sobe Mongo com `rs.status().ok == 1` (healthcheck)
- [x] `JwtOptions`, `MongoOptions`, `GeminiOptions`, `SeedOptions`, `CorsOptions` validados no startup (chave JWT < 32 bytes ou ausente → app **falha ao subir** com mensagem clara)
- [x] `.env.example` documenta `Jwt__Key`, `ConnectionStrings__Mongo`, `Gemini__ApiKey`, `Seed__Enabled` sem valores reais; `.env` ignorado no git
- [x] `appsettings*.json` sem segredos
- [x] Gate: `docker compose config` válido + teste unit dos validadores de options

**Notas de execução**: `Dockerfile` e `.dockerignore` já criados aqui (antecipando parte da B24) e o serviço `api` fica no profile `api` (`docker compose --profile api up`). `${JWT_KEY:-}` com default vazio para não exigir a chave só para subir o Mongo (a API falha no startup com mensagem clara). Replica set anuncia `localhost:27017` → strings de conexão usam `directConnection=true`. Verificado: `rs.status()` = PRIMARY. Testes: 14 (validators) + 4 (startup do host).
**Tests**: unit (Options validators + falha de startup)
**Gate**: unit

---

### B03: SPIKE — EF Core MongoDB provider (go/no-go)  ✅ concluída — **GO**

**What**: Prova de conceito descartável que valida os riscos do design §8: (a) CRUD de uma entidade com `ObjectId`→`string` e documento embutido, (b) **transação multi-documento** com commit e rollback, (c) **concurrency token**/`Version`, (d) consulta com filtro + ordenação + paginação, (e) **stores Identity customizados** (`UserManager.CreateAsync` + `CheckPasswordAsync`), (f) criação de índice único/parcial/TTL com o driver. Registrar resultado em ADR.
**Where**: `backend/spikes/EfMongoSpike/` (não entra na solution final), decisão em `design.md` §19 (ADR-002/003 atualizados)
**Depends on**: B02
**Reuses**: design §8, ADR-002, ADR-003
**Requirement**: DS-10 (mitiga risco de R2-09, R2-01, R2-03.8)

**Done when**:
- [x] Cada item (a)–(f) tem resultado ✅/❌ documentado
- [x] **Go**: todos ✅ → segue ADR-002/003 como escritos
- [x] **Fallback**: se (b), (c) ou (e) ❌ → decisão registrada de usar `MongoDB.Driver` direto **dentro dos repositórios/stores** (interfaces de Application inalteradas) e ADR atualizado
- [ ] Pasta `spikes/` removida ou marcada como exemplo antes do empacotamento (D04)

**Resultado (14/15 ✅)** — detalhes no design §8.2 e em `backend/spikes/README.md`:
- ✅ CRUD com `_id` ObjectId nativo (inclusive **sem atributos do driver**, via `HasConversion`), enum como string, `Ice` e lista embutidos
- ✅ Transação multi-doc: commit, rollback explícito, rollback por exceção; `SaveChanges` com várias entidades é atômico
- ✅ Concurrency token (`Version`) → `DbUpdateConcurrencyException`
- ✅ `Where/OrderBy/Skip/Take/Count/StartsWith`; `Select` simples
- ✅ Identity: `UserManager` + store customizado (hash, senha fraca, e-mail único, find, lockout)
- ✅ Índices único / parcial / TTL via driver
- ✅ camelCase: `SetElementName` (propriedades) + `OwnsOne/OwnsMany(...).HasElementName` (embutidos)
- ❌ `GroupBy` no servidor (esperado) → agregar em memória
- ⚠️ Duplicate key sai como `MongoBulkWriteException(DuplicateKey)`, não `DbUpdateException` → traduzir no repositório/UoW
**Tests**: none (spike; execução: `dotnet run --project backend/spikes/EfMongoSpike` com o Mongo do compose no ar)
**Gate**: — (decisão registrada)

---

### B04: Domain — entidades, VOs, enums, regras  ✅ concluída

**What**: Entidades e regras puras do design §4 com testes.
**Where**: `Domain/Entities/*`, `Domain/ValueObjects/{Ice,FieldChange}.cs`, `Domain/Enums/*`, `Domain/Rules/PointsRules.cs`, `Domain/Exceptions/*`; `Domain.Tests/*`
**Depends on**: B03
**Reuses**: `mobile/.../core/domain/model/*` (Enums, Ice, Idea, Project, User), `mobile/.../BadgeEvaluatorTest.kt`, `IceTest.kt`
**Requirement**: R2-03.5–R2-03.11, R2-04.1, R2-04.4, R2-05.1–R2-05.2

**Done when**:
- [x] Enums: `Role`, `Division`, `IdeaStatus`, `ProjectStage`, `Pillar`, `Period`, `PointReason`, `GuidelineAction`
- [x] `Ice`: valida 1–10; `Score = I×C×F`
- [x] `Idea`: `EditContent` só em `SUBMETIDA` (senão `DomainException IDEA_NOT_EDITABLE`); `SaveIce` (SUBMETIDA/EM_ANALISE → EM_ANALISE); `Approve(reviewerId)` bloqueia autor; `Reject` exige comentário; `MarkImplemented` idempotente; normaliza categoria (trim + 1ª maiúscula, 2–40)
- [x] `AppUser.ApplyPoints(delta)` devolve delta efetivo com clamp em 0; `AddBadges` sem duplicar
- [x] `Project.ApplyUpdate(...)` devolve diff (`field/from/to`) só dos campos alterados, incrementa `Version`, sinaliza transição para `CONCLUIDO`; `IsOverdue(now)`
- [x] `RefreshToken` (`Rotate/Revoke/IsActive`), `PointEvent`, `GuidelineHistoryEntry`, `ProjectUpdate` imutáveis
- [x] `BadgeEvaluator` (5 badges, retorna só novas) portado com os **mesmos casos** do `BadgeEvaluatorTest.kt`
- [x] Domain sem referência a ASP.NET/EF/Mongo
- [x] Gate: `dotnet test --filter "Category!=Integration"` (≥ 25 testes Domain)

**Notas de execução**: enums em `UPPER_SNAKE` (`EM_ANALISE`) — são exatamente os valores do Mongo/API/app, sem camada de tradução. Ids do Domain são strings no formato ObjectId geradas por `EntityId` (sem depender do driver). `AppUser` é também o usuário do Identity (campos de credencial com setter público; o resto é encapsulado). Valores monetários em `decimal`. `FieldChange` guarda `Kind` (TEXT/NUMBER/DATE) + valores canônicos em texto, para a API devolver JSON tipado sem `object` no banco. `BadgeEvaluator` recebe o fuso do "mês calendário" (padrão UTC; a Application passa `America/Sao_Paulo`). **109 testes** (meta ≥ 25), incluindo os 10 casos portados de `BadgeEvaluatorTest.kt` e os de `IceTest.kt`.
**Tests**: unit
**Gate**: unit

---

### B05: Application — fundamentos (Result, abstrações, validação)  ✅ concluída

**What**: `Result<T>`/`Error`/`ErrorType`, `ICurrentUser`, `IClock`, `IUnitOfWork`, interfaces de repositório, `PageRequest/PagedResponse`, infraestrutura de handlers e validação (FluentValidation), `DependencyInjection.AddApplication()`.
**Where**: `Application/Common/*`, `Application/DependencyInjection.cs`, `Application.Tests/Common/*`
**Depends on**: B04
**Reuses**: design §5.1–5.3, §6
**Requirement**: R2-10.1, R2-10.2, R2-10.3

**Done when**:
- [x] `Result<T>` com fábricas `Ok/Fail(Error…)`; mapeamento `ErrorType → HTTP` disponível para a Api
- [x] `IHandler<TCommand,TResult>` + scan de assembly registra todos os handlers
- [x] Helper de validação: valida request com FluentValidation e devolve `Result` Validation com `errors[]` (field/code/message)
- [x] `PageRequest` limita `pageSize` a 1–200 (default 50)
- [x] Interfaces de repositório: Idea, Guideline, GuidelineHistory, Project, ProjectUpdate, PointEvent, User, RefreshToken, InsightCache
- [x] Gate: unit (≥ 6 testes: Result, paginação, pipeline de validação)

**Notas de execução**: além do previsto, foram criadas `DuplicateKeyException` e `ConcurrencyConflictException` (Application) — a Infrastructure traduz as exceções do driver/EF nelas — e `DomainErrorMapper` (`DomainException` → `Error`). `IUnitOfWork` expõe `SaveChangesAsync` e `ExecuteInTransactionAsync`. Handlers registrados como classe concreta **e** interface. **46 testes** (meta ≥ 6).
**Tests**: unit
**Gate**: unit

---

### B06: Infrastructure — persistência Mongo (DbContext, repositórios, UoW, índices)  ✅ concluída

**What**: `AppDbContext`, configurações de entidade, repositórios, `MongoUnitOfWork` com transação/retry e `IndexInitializer`.
**Where**: `Infrastructure/Persistence/{AppDbContext,Configurations,Repositories,MongoUnitOfWork,Indexes}`, `Infrastructure/DependencyInjection.cs`, `Infrastructure.Tests/Persistence/*`
**Depends on**: B05
**Reuses**: resultado do spike B03; spec — modelo de dados e índices
**Requirement**: R2-09.1, R2-09.7, R2-10.1

**Done when**:
- [x] Coleções e campos conforme spec: camelCase por convenção no `OnModelCreating` (`SetElementName` + `HasElementName` nos embutidos), `_id`/referências como `ObjectId` nativo via `HasConversion` (sem atributos no Domain), enums como string, `Ice`/`changes` embutidos, `legacyId` opcional (achados do B03)
- [x] `IndexInitializer` cria **todos** os índices do spec (únicos, parcial `projects.originatingIdeaId`, TTL `refreshTokens` e `aiInsights`); reexecutar não falha
- [x] `MongoUnitOfWork.ExecuteInTransactionAsync`: commit ok; exceção → rollback total; retry em `TransientTransactionError`; `MongoBulkWriteException` com `DuplicateKey` traduzida em erro de conflito (não vaza exceção do driver)
- [x] Startup verifica replica set e loga erro claro se ausente
- [x] Testes de integração (Testcontainers rs): CRUD por repositório, rollback atômico (2 escritas, 2ª falha → nenhuma persiste), índice único parcial rejeita 2º projeto com mesmo `originatingIdeaId`
- [x] Gate: `dotnet test --filter "Category=Integration"` (≥ 8 testes)

**Notas de execução**: `Decimal128` é o mapeamento padrão de `decimal` (confirmado nos testes). `MongoOptions` ganhou `RequireReplicaSet` (falha o startup sem replica set, com mensagem clara) e `InitializeOnStartup`. TTL de `refreshTokens` = 1 dia após `expiresAt` (mantém tokens rotacionados para detectar reuso). Filtros com ids fora do formato ObjectId retornam vazio/`null` sem consultar o banco. Testes usam Testcontainers (Mongo 7 em replica set) ou `AGUIA_TEST_MONGO` para o ciclo local rápido. **45 testes de integração** (meta ≥ 8) + 18 unitários de options.
**Tests**: integration
**Gate**: full

---

### B07: Api base — pipeline, erros, logs, Swagger, health  ✅ concluída

**What**: `Program.cs` com composition root; `ExceptionHandlingMiddleware` (ProblemDetails + `traceId`); `CorrelationIdMiddleware`; Serilog; Swagger com Bearer; health checks; mapeamento `Result → IActionResult`.
**Where**: `Api/Program.cs`, `Api/Middleware/*`, `Api/Extensions/*`, `Api.Tests/Infrastructure/*`
**Depends on**: B06
**Reuses**: design §10, §11, §14
**Requirement**: R2-10.2, R2-10.4, R2-10.5

**Done when**:
- [x] `GET /health/live` = 200; `/health/ready` = 200 com Mongo, 503 sem Mongo; `/health` agrega
- [x] Exceção não tratada → `500` `application/problem+json` com `code=INTERNAL_ERROR` + `traceId`, **sem stack trace**
- [x] `X-Correlation-ID` presente na resposta e nos logs
- [x] `/swagger` exibe esquema Bearer (Development)
- [x] Logs estruturados com `CorrelationId`; nenhum log de `Authorization`
- [x] `WebApplicationFactory<Program>` funcional (base para testes seguintes)
- [x] Gate: unit + integration Api (≥ 5 testes: 500, 404 ProblemDetails, correlation id, health ok/fail)

**Notas de execução**: `AddApi` recebe `IHostEnvironment` (Swagger só em Development ou com `Swagger:Enabled`). `SuppressMapClientErrors=true` para 404/415 gerados pelo MVC também usarem o formato de erro da API. Serilog com `preserveStaticLogger: true` (hosts paralelos nos testes) e sinks extras do DI. Rota `{id:objectid}` (id inválido → 404, nunca 500). Health em JSON sem detalhes da exceção. Endpoints de teste (`TestProbeController`) só existem no projeto de testes. **58 testes de Api**; smoke com o processo real (`dotnet run`) validado: `/health/ready` 200, 404 `problem+json`, Swagger e índices no startup.
**Tests**: integration
**Gate**: full

---

### B08: Identity — stores Mongo customizados

**What**: `MongoUserStore` (password, email, lockout, security stamp; a base já existe no spike `backend/spikes/EfMongoSpike/IdentityStore.cs`), sem role store; registro do Identity (`AddIdentityCore<AppUser>`), política de senha e lockout.
**Where**: `Infrastructure/Identity/*`, `Infrastructure.Tests/Identity/*`
**Depends on**: B06 (reaproveita resultado do spike B03-e)
**Reuses**: design §7.1, ADR-003
**Requirement**: R2-01.5, R2-01.6

**Done when**:
- [ ] `UserManager.CreateAsync(user, password)` grava `passwordHash` (PBKDF2), nunca a senha
- [ ] `CheckPasswordAsync` ok/erro; 5 falhas → `IsLockedOut` por 15 min
- [ ] Senha < 8 caracteres rejeitada
- [ ] Role única (`OPERADOR|GESTOR|LIDER`) persistida em `users.role` — **sem** `IRoleStore`/`IUserRoleStore` (decisão do B03; o perfil é campo do usuário)
- [ ] Testes de integração dos stores (≥ 6 testes)
- [ ] Gate: `dotnet test --filter "Category=Integration"`

**Tests**: integration
**Gate**: full

---

### B09: Auth — JWT, refresh rotativo e endpoints

**What**: `JwtTokenService`, `RefreshTokenService` (hash SHA-256, família, rotação, reuso), handlers `Login/Refresh/Logout/Me`, `AuthController`, rate limit `/auth/*`.
**Where**: `Infrastructure/Authentication/*`, `Application/Features/Auth/*`, `Api/Controllers/AuthController.cs`, testes correspondentes
**Depends on**: B07, B08
**Reuses**: design §7.2, §7.3, §7.5
**Requirement**: R2-01.1–R2-01.4, R2-01.6, R2-01.7, R2-01.9

**Done when**:
- [ ] Login válido → 200 com `accessToken` (claims `sub,email,name,role,division,jti`, exp 30 min), `refreshToken`, `expiresIn=1800`, `user`
- [ ] Usuário inexistente e senha errada → **mesma** resposta `401 INVALID_CREDENTIALS`
- [ ] Bloqueio após 5 falhas → `429` + `Retry-After`
- [ ] `refresh` rotaciona (antigo passa a inválido); reapresentar antigo → `401 TOKEN_INVALID` e **família revogada** (novo token também falha)
- [ ] Refresh armazenado **somente como hash** (teste inspeciona o banco)
- [ ] `logout` revoga; `me` retorna perfil com `points`/`badges`
- [ ] JWT inválido/expirado → 401; resposta nunca contém `passwordHash`/`securityStamp`
- [ ] Rate limit: 11ª chamada/min ao `/auth/login` do mesmo IP → 429
- [ ] Gate: unit + integration (≥ 12 testes)

**Tests**: unit + integration
**Gate**: full

---

### B10: Autorização — policies, seed e harness de matriz

**What**: Policies (`GestorOnly`, `LiderOnly`, `CanCreateIdea`, `ProjectsRead`, `UsersRead`, fallback autenticado), `DatabaseSeeder`, e **harness** de teste que cria os 3 usuários e obtém tokens (base para a matriz da B22).
**Where**: `Api/Extensions/AuthorizationExtensions.cs`, `Infrastructure/Seed/DatabaseSeeder.cs`, `Api.Tests/Support/{TestServerFixture,AuthHelper}.cs`
**Depends on**: B09
**Reuses**: design §7.4, §8.5; credenciais as-built (`*@aguiabranca.com` / `aguiabranca123`)
**Requirement**: R2-01.5, R2-01.8

**Done when**:
- [ ] Seed idempotente cria 3 usuários demo + 4 orientações + 6 ideias (estados variados) + 3 projetos com histórico; rodar 2× não duplica
- [ ] Seed só roda com `Seed:Enabled=true` (padrão em Development)
- [ ] Endpoint sem `[AllowAnonymous]` e sem token → 401 (fallback policy)
- [ ] Endpoint de teste por policy: token errado → 403; correto → 200
- [ ] Harness expõe `ClientAs(Role)` para os testes seguintes
- [ ] Gate: integration (≥ 6 testes)

**Tests**: integration
**Gate**: full

---

### B11: Orientações estratégicas + histórico

**What**: CRUD de orientações (`LIDER`), leitura para todos, e **histórico imutável** com filtros.
**Where**: `Application/Features/Guidelines/*`, `Api/Controllers/GuidelinesController.cs`, `Api/Contracts/*`, testes
**Depends on**: B10
**Reuses**: `mobile/.../GuidelinesRepository.kt`; regra R-02
**Requirement**: R2-02.1–R2-02.6

**Done when**:
- [ ] `GET /guidelines` ordenado por `updatedAt` desc; `GET /guidelines/{id}`; `POST/PUT/DELETE` só LIDER (gestor/operador → 403)
- [ ] Validação: título 3–120, descrição ≤ 2000, pilar válido, campanha ≤ 80
- [ ] Criar/editar/excluir gravam `guidelineHistory` (`id`, `occurredAt`, `category`=pillar, `campaign`, `action`, snapshot, autor) **na mesma transação**
- [ ] `GET /guidelines/history?guidelineId&category&campaign&from&to` paginado, mais recente primeiro; entradas de orientação já excluída permanecem
- [ ] Excluir orientação com ideia vinculada → 204 e ideia continua acessível (`guidelineTitle=null`) — coberto no teste da B13
- [ ] Gate: unit + integration (≥ 12 testes)

**Tests**: unit + integration
**Gate**: full

---

### B12: Gamificação — pontos, badges persistidas, ranking mensal

**What**: `PointsService` (razão + clamp), integração do `BadgeEvaluator` (B04), consulta de ranking mensal, `GET /users`, `GET /users/ranking`.
**Where**: `Application/Features/Gamification/*`, `Application/Features/Users/*`, `Api/Controllers/UsersController.cs`, testes
**Depends on**: B11
**Reuses**: B04 (`BadgeEvaluator`, `PointsRules`); achados A1/A2
**Requirement**: R2-05.1–R2-05.5

**Done when**:
- [ ] `PointsService.AwardAsync` grava `pointEvents` com delta **efetivo** e atualiza `users.points` (≥ 0)
- [ ] Após evento: `BadgeEvaluator` roda e novas badges são gravadas em `users.badges` sem duplicar
- [ ] `GET /users/ranking?limit=5`: só `OPERADOR`, soma dos eventos do **mês corrente** em `America/Sao_Paulo`; desempate por pontos totais e nome; retorna `id,name,monthPoints`
- [ ] Ranking ignora eventos de meses anteriores (teste com `IClock` fake cruzando virada de mês)
- [ ] `GET /users?role=` permitido a GESTOR/LIDER; operador → 403
- [ ] Gate: unit + integration (≥ 10 testes)

**Tests**: unit + integration
**Gate**: full

---

### B13: Ideias — CRUD, posse, vínculo e pontos

**What**: `POST/GET/PUT/DELETE /ideas`, listagem com `scope`, resposta com `guidelineTitle` e `linkedProject`, créditos e estornos de pontos.
**Where**: `Application/Features/Ideas/{Create,Update,Delete,Get,List}/*`, `Api/Controllers/IdeasController.cs`, testes
**Depends on**: B12
**Reuses**: `mobile/.../IdeasRepository.kt`, `FirestoreIdeasRepository.kt` (regras +10/+5/−10/−15); R-03
**Requirement**: R2-03.1–R2-03.5, R2-03.9, R2-03.10, R2-05

**Done when**:
- [ ] `POST /ideas`: servidor define `authorId/authorName/status`; corpo com `status`/`authorId` é ignorado; `guidelineId` inexistente → `422 GUIDELINE_NOT_FOUND`; retorna 201 + `pointsAwarded` (15 com orientação, 10 sem)
- [ ] `GET /ideas?scope=mine|curation|all&status&guidelineId&division`: operador só `mine` (tentar `all` devolve apenas as próprias); detalhe de ideia alheia → 404 para operador
- [ ] `scope=curation`: `SUBMETIDA`+`EM_ANALISE`, ICE score desc, sem ICE ao fim
- [ ] `PUT/DELETE`: só o autor e só `SUBMETIDA` (senão `409 IDEA_NOT_EDITABLE`); `DELETE` estorna −10/−15 com clamp (evento efetivo)
- [ ] Resposta inclui `linkedProject {id,stage,updatedAt}` e `guidelineTitle` (`null` se órfã)
- [ ] Badges "Primeira Ideia"/"Inovador do Mês" concedidas na criação (5ª ideia no mês, 1× só)
- [ ] Gate: unit + integration (≥ 16 testes)

**Tests**: unit + integration
**Gate**: full

---

### B14: Ideias — ICE, rejeição e aprovação (→ projeto rascunho)

**What**: `PUT /ideas/{id}/ice`, `POST /ideas/{id}/reject`, `POST /ideas/{id}/approve` com a automação 1.
**Where**: `Application/Features/Ideas/{SaveIce,Reject,Approve}/*`, controller, testes
**Depends on**: B13
**Reuses**: `mobile/.../ApproveIdeaUseCase.kt` (campos do projeto criado, autoaprovação); R-03.5/.7/.8/.12
**Requirement**: R2-03.6–R2-03.8, R2-03.11, R2-05

**Done when**:
- [ ] ICE: valores fora de 1–10 ou não inteiros → 400; `score` calculado no servidor; `SUBMETIDA` → `EM_ANALISE` automático; ICE em ideia `APROVADA/REJEITADA/IMPLEMENTADA` → `409 IDEA_INVALID_STATE`
- [ ] `reject` sem `comment` → 400; com comentário → `REJEITADA` (+`reviewerId`, `reviewedAt`), **sem pontos**
- [ ] `approve` em **uma transação**: ideia `APROVADA`; projeto `PLANEJAMENTO` (`title="PROJ: "+título`, herda `division/guidelineId`, `originatingIdeaId`, `creatorManagerId`, `priorityScore=ice.score`, `reporter*`=autor, `responsible*`=gestor); 1ª entrada de histórico "Criado automaticamente a partir da ideia: {título}"; +50 pts ao autor; badges avaliadas (ex.: Estrategista)
- [ ] Autor tentando aprovar a própria ideia → `403 SELF_APPROVAL_FORBIDDEN`
- [ ] **Idempotência:** aprovar 2× (inclusive em concorrência 2 requisições simultâneas) → 1 projeto, +50 uma vez, 2ª resposta `alreadyApproved=true` com o mesmo `projectId`
- [ ] Falha no meio (simulada) → nada persiste (rollback)
- [ ] Operador → 403 em ICE/approve/reject
- [ ] Gate: unit + integration (≥ 16 testes)

**Tests**: unit + integration
**Gate**: full

---

### B15: Projetos — CRUD + histórico com diff

**What**: `GET/POST/PUT/DELETE /projects`, `GET /projects/{id}/updates`, diff calculado no servidor, concorrência opcional por `version`.
**Where**: `Application/Features/Projects/{Create,Update,Delete,Get,List,Updates}/*`, `Api/Controllers/ProjectsController.cs`, testes
**Depends on**: B14
**Reuses**: `mobile/.../FirestoreProjectsRepository.kt` (campos do diff, notas), `ProjectInput`; R-04
**Requirement**: R2-04.1–R2-04.4, R2-04.6, R2-04.7

**Done when**:
- [ ] Leitura só GESTOR/LIDER (operador → 403); escrita só GESTOR (líder → 403)
- [ ] Validação: `investment/financialReturn/productivityGain/costReduction ≥ 0`; `guidelineId` existente (senão 422); enums válidos
- [ ] Criação registra entrada "Projeto criado"; `PUT` grava entrada com autor, nota e `changes[{field,from,to}]` **só dos campos alterados** (`investment` 100→120 gera exatamente 1 change)
- [ ] `PUT` sem alteração real ainda registra nota com `changes=[]`
- [ ] `version` enviada e divergente → `409 CONCURRENCY_CONFLICT`
- [ ] `GET /projects?stage&division&guidelineId` paginado; histórico mais recente primeiro
- [ ] `DELETE` remove projeto + `projectUpdates`; ideia de origem permanece
- [ ] Gate: unit + integration (≥ 16 testes)

**Tests**: unit + integration
**Gate**: full

---

### B16: Conclusão de projeto → ideia IMPLEMENTADA (+200)

**What**: Automação 2 dentro do `UpdateProjectHandler`.
**Where**: `Application/Features/Projects/Update/*` (extensão), testes
**Depends on**: B15
**Reuses**: `mobile/.../CompleteProjectUseCase.kt`; R-03.13
**Requirement**: R2-04.5, R2-05

**Done when**:
- [ ] `PUT` que muda `stage` para `CONCLUIDO` em projeto com `originatingIdeaId`: ideia → `IMPLEMENTADA`, autor +200, badge "Impacto Real" — **tudo na mesma transação da edição**
- [ ] Repetir o `PUT` (já `CONCLUIDO`/ideia `IMPLEMENTADA`) → sem novo crédito
- [ ] Projeto sem ideia de origem ou ideia inexistente → sem efeito colateral, sem erro
- [ ] Falha ao creditar → edição do projeto também desfeita
- [ ] Gate: unit + integration (≥ 8 testes: com ideia, sem ideia, idempotência, rollback, badge)

**Tests**: unit + integration
**Gate**: full

---

### B17: ReportCalculator + testes de paridade (golden)

**What**: Porte do `DashboardComputer.kt` para C# como função pura + adição de `overdue/daysToDeadline`.
**Where**: `Application/Features/Reports/{ReportCalculator,ReportFilters,ReportSummary}.cs`, `Application.Tests/Reports/*` (+ `golden/*.json`)
**Depends on**: B16
**Reuses**: `mobile/.../feature/dashboard/DashboardComputer.kt` (fonte da verdade da lógica), R-05
**Requirement**: R2-06.1–R2-06.6

**Done when**:
- [ ] Funil (submitted/evaluated/approved/inExecution/roiPositive), KPIs, sparkline 6 meses (janela fixa, sensível a divisão, `null` para mês com investimento 0), impacto por orientação (ordenação: com projeto primeiro, ROI desc), lista por ROI desc
- [ ] ROI = (Σretorno−Σinvest)/Σinvest×100; Σinvest=0 → `null`
- [ ] Janelas de período idênticas às do Kotlin (`THIS_MONTH`, `LAST_QUARTER`, `THIS_YEAR`, `ALL`) usando `IClock` e fuso `America/Sao_Paulo`
- [ ] `overdueProjects` e `daysToDeadline/overdue` por projeto (projetos `CONCLUIDO/CANCELADO` nunca "atrasados")
- [ ] **Golden**: ≥ 3 conjuntos de dados de referência com resultado esperado calculado à mão/derivado do Kotlin; asserts numéricos exatos
- [ ] Testes: filtros combinados, investimento 0, < 6 meses de dados, orientação sem projeto
- [ ] Gate: unit (≥ 12 testes)

**Tests**: unit
**Gate**: unit

---

### B18: Endpoints de relatórios

**What**: `GET /reports/summary`, `/reports/guidelines`, `/reports/guidelines/{id}`, `/reports/projects/{id}` (LIDER).
**Where**: `Application/Features/Reports/{Summary,Guidelines,Project}/*`, `Api/Controllers/ReportsController.cs`, testes
**Depends on**: B17
**Reuses**: B17; contratos do spec
**Requirement**: R2-06.1–R2-06.6

**Done when**:
- [ ] `summary?period&division` retorna exatamente o contrato do spec (funil, kpis, sparkline, guidelineImpacts, projects)
- [ ] `guidelines/{id}` traz ideias, projetos, investimento, retorno, lucro e ROI da orientação; id inexistente → 404
- [ ] `projects/{id}` traz investimento, retorno, lucro, ROI, produtividade, redução de custo, `targetDate`, `daysToDeadline`, `overdue`
- [ ] Filtro `division` reduz funil/KPIs/impacto/lista; período inválido → 400
- [ ] Gestor/operador → 403; sem token → 401
- [ ] Teste de integração: editar `financialReturn` de um projeto altera KPIs na próxima chamada
- [ ] Gate: integration (≥ 10 testes)

**Tests**: integration
**Gate**: full

---

### B19: GeminiClient resiliente [P]

**What**: Cliente HTTP tipado para `generateContent` com `responseSchema`, timeout, retry/backoff, circuit breaker e mapeamento de erros. Confirmar o modelo gratuito disponível e configurá-lo.
**Where**: `Infrastructure/Ai/{GeminiClient,GeminiOptions,GeminiSchemas}.cs`, `Application/Common/Abstractions/IInsightGenerator.cs`, `Infrastructure.Tests/Ai/*`
**Depends on**: B10 (independente das features; pode rodar em paralelo à Fase 4–6)
**Reuses**: design §9.1, §9.4
**Requirement**: R2-07.1, R2-07.5, R2-07.7 · **OP-6**

**Done when**:
- [ ] Modelo definido em `Gemini:Model` (verificar em ai.google.dev o modelo disponível no free tier da conta e registrar no README/OP-6); nenhum nome de modelo hardcoded no código
- [ ] Chave enviada no header `x-goog-api-key` (não na query); chave nunca aparece em log (teste com logger fake)
- [ ] Timeout 20 s; 1 retry com backoff em 429/5xx/timeout; circuit breaker; falha final → `Error AI_UNAVAILABLE`
- [ ] Resposta fora do schema/JSON inválido → `Error AI_INVALID_RESPONSE`
- [ ] Testes com `HttpMessageHandler` fake: sucesso, 429 seguido de 200, 500 persistente, timeout, JSON inválido (≥ 8 testes)
- [ ] Smoke manual documentado: uma chamada real com chave de teste retorna JSON válido (não roda no CI)
- [ ] Gate: unit

**Tests**: unit
**Gate**: unit

---

### B20: Insights de IA — prompt, cache, cota e endpoint

**What**: `GenerateInsightsHandler`, `InsightPromptBuilder` (sem PII, sanitizado), cache Mongo com TTL, rate limit por usuário + teto diário, `POST /reports/insights`.
**Where**: `Application/Features/Reports/Insights/*`, `Infrastructure/Ai/{InsightPromptBuilder,InsightCache}.cs`, `Api/Controllers/ReportsController.cs`, testes
**Depends on**: B18, B19
**Reuses**: design §9.2–9.4; ADR-007
**Requirement**: R2-07.1–R2-07.8

**Done when**:
- [ ] Fluxo: resumo (B17) → prompt → cache → Gemini → validação → grava → responde `{summary,highlights[],risks[],recommendations[],generatedAt,model,fromCache}`
- [ ] **Sem PII:** teste captura o corpo enviado ao Gemini e afirma ausência de nomes, e-mails e IDs de usuário; títulos truncados em 80 chars; máx. 10 projetos e 10 orientações
- [ ] Título malicioso ("ignore as instruções…") aparece apenas dentro do bloco `<dados>` delimitado/escapado; saída continua validada pelo schema
- [ ] 2ª chamada idêntica → `fromCache=true`, **0** chamadas ao Gemini; `refresh=true` força nova geração
- [ ] Mudança nos dados (ex.: retorno de projeto) altera o digest → cache miss
- [ ] Rate limit 6/min por usuário → 429; teto diário `Gemini:DailyLimit` → 429/`RATE_LIMITED`
- [ ] Gemini indisponível → `503 AI_UNAVAILABLE`; schema inválido → `502 AI_INVALID_RESPONSE` (nada é cacheado)
- [ ] Operador/gestor → 403
- [ ] Gate: unit + integration com `IInsightGenerator` fake (≥ 12 testes)

**Tests**: unit + integration
**Gate**: full

---

### B21: Hardening de segurança

**What**: CORS restrito, limites de payload, headers de segurança, HTTPS/HSTS em produção, revisão de logs, varredura de segredos.
**Where**: `Api/Extensions/SecurityExtensions.cs`, `Api/Middleware/SecurityHeadersMiddleware.cs`, testes
**Depends on**: B20
**Reuses**: design §12
**Requirement**: R2-10.6

**Done when**:
- [ ] CORS só para `Cors:Origins` configuradas; origem não listada sem `Access-Control-Allow-Origin`
- [ ] Payload > limite (1 MB) → 413
- [ ] Headers: `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store` em `/auth/*`; HSTS em Production
- [ ] IDs de rota inválidos (não ObjectId) → 400/404, sem 500
- [ ] Varredura: `git grep` de padrões de segredo (`Key=`, `ApiKey`, `mongodb+srv://`) não encontra valores reais; `.env` fora do git
- [ ] Teste garante que nenhuma resposta de erro contém stack trace
- [ ] Gate: integration (≥ 6 testes)

**Tests**: integration
**Gate**: full

---

### B22: Matriz de autorização completa + fluxo ponta a ponta

**What**: Teste parametrizado **endpoint × perfil** (anônimo, OPERADOR, GESTOR, LIDER) e teste de fluxo completo com Mongo real.
**Where**: `Api.Tests/Authorization/AuthorizationMatrixTests.cs`, `Api.Tests/Flows/FullFlowTests.cs`
**Depends on**: B21
**Reuses**: matriz de permissões do spec; harness B10
**Requirement**: R2-01.5, R2-10.7

**Done when**:
- [ ] Matriz cobre **todos** os endpoints do contrato: esperado 401 (anônimo), 403 (perfil sem permissão) e 2xx (perfil permitido); um teste falha se surgir endpoint sem entrada na matriz (reflexão nas rotas)
- [ ] Fluxo: líder cria orientação → operador cria ideia vinculada (+15) → gestor salva ICE → gestor aprova (projeto criado, +50) → gestor edita projeto (diff) → conclui (ideia IMPLEMENTADA, +200, "Impacto Real") → líder lê `/reports/summary` com números esperados → ranking mensal → histórico da orientação
- [ ] Cobertura reportada (coverlet): Domain + Application ≥ 80%
- [ ] Gate: `dotnet test backend/AguiaBranca.sln` verde

**Tests**: integration
**Gate**: full

---

### B23: FirestoreMigrator (Firestore → MongoDB) [P]

**What**: Ferramenta console para migrar dados (R2-09).
**Where**: `backend/tools/AguiaBranca.FirestoreMigrator/*`, `backend/tools/AguiaBranca.FirestoreMigrator.Tests/*`, `backend/tools/README.md`
**Depends on**: B06 (esquema), B12 (pontos/badges); independente dos endpoints
**Reuses**: mapeamento do spec R2-09; `mobile/.../core/data/dto/*`, `Mappers.kt` (campos/enums)
**Requirement**: R2-09.1–R2-09.7

**Done when**:
- [ ] CLI: `--firestore-project`, `--credentials`, `--mongo`, `--database`, `--dry-run`
- [ ] Ordem `users → guidelines(+history CREATED) → ideas → projects → projectUpdates → pointEvents(MIGRATION) → badges recalculadas`
- [ ] `IdMap` legacyId→ObjectId persistido (`migration_idmap`); **todas** as referências remapeadas
- [ ] Idempotente: 2ª execução não duplica (upsert por `legacyId`); `--dry-run` não escreve
- [ ] `Timestamp`→UTC; enum inválido → item reportado, migração continua
- [ ] Usuários recriados no Identity com senha temporária; e-mail preservado
- [ ] **Relatório de conciliação** (JSON/console): contagens origem×destino por coleção, órfãos, inválidos
- [ ] Testes com transformações puras (mapeamento, remap, enums, timestamps) e dataset fake em memória (≥ 8 testes); execução real validada contra o emulador do Firestore ou projeto de teste (evidência no README)
- [ ] Gate: unit do migrator

**Tests**: unit
**Gate**: unit

---

### B24: Dockerfile, README do backend e deploy de demonstração

**What**: Dockerfile multi-stage, compose completo (Mongo + API + seed), README de execução, publicação da API para o APK (OP-5).
**Where**: `backend/Dockerfile`, `backend/docker-compose.yml`, `backend/README.md`, configuração do host escolhido
**Depends on**: B22
**Reuses**: design §15
**Requirement**: R2-10.8, R2-11.1 · **OP-5**

**Done when**:
- [ ] `docker compose up --build` sobe Mongo (rs0) + API; `GET /health/ready` = 200 e login demo funciona (seed)
- [ ] README: pré-requisitos, `docker compose`, `dotnet run`, variáveis de ambiente, usuários demo, como gerar a chave Gemini (Google AI Studio), rodar testes, estrutura de camadas, migração, portas/URLs, troubleshooting (replica set, SDK 8)
- [ ] Imagem roda como usuário não-root; sem segredos na imagem
- [ ] **Deploy** (decisão OP-5): API em host gratuito (ex.: Render/Azure/Railway) + MongoDB Atlas M0; URL HTTPS anotada no README e usada em M11. Se inviável, registrar fallback `docker compose` + IP da LAN e ajustar M11
- [ ] Smoke pós-deploy: login + `/reports/summary` (seed) em ambiente publicado
- [ ] Gate: `docker compose up` + smoke script

**Tests**: none (smoke manual/script)
**Gate**: full

---

## Task Breakdown — Mobile

> Toda a trilha mobile **mantém** as interfaces de `core/domain/*Repository`, os ViewModels e as telas; só troca a camada de dados. Consultar os contratos em `spec.md` e o Swagger do backend.

### M01: Dependências de rede, BuildConfig e configuração de segurança

**What**: Adicionar Retrofit/OkHttp/converter kotlinx-serialization/DataStore/MockWebServer ao version catalog; `BuildConfig.API_BASE_URL` por build type; `network_security_config`.
**Where**: `mobile/gradle/libs.versions.toml`, `mobile/app/build.gradle.kts`, `mobile/app/src/main/res/xml/network_security_config.xml`, `AndroidManifest.xml`
**Depends on**: B09 (contrato de auth publicado no Swagger)
**Reuses**: design §17
**Requirement**: R2-08.7

**Done when**:
- [ ] Dependências adicionadas ao catálogo (versões estáveis compatíveis com Kotlin/AGP atuais do projeto)
- [ ] `debug`: `API_BASE_URL = http://10.0.2.2:5080/api/v1/` (cleartext permitido **só** para 10.0.2.2/localhost); `release`: valor de `local.properties`/gradle property `api.baseUrl` (HTTPS), com falha clara se ausente
- [ ] Permissão `INTERNET` no manifest; `usesCleartextTraffic` não habilitado globalmente
- [ ] Gate: `./gradlew :app:compileDebugKotlin`

**Tests**: none
**Gate**: quick

---

### M02: Camada de rede — Api, DTOs, tokens, interceptors, mapeamento de erros

**What**: `ApiModule` (Hilt), interfaces `AuthApi/GuidelinesApi/IdeasApi/ProjectsApi/ReportsApi/UsersApi`, DTOs `@Serializable`, `TokenStore` (DataStore + AES-GCM/Keystore), `AuthInterceptor`, `TokenAuthenticator`, `ProblemDetailsMapper`, `PollingFlow` + `RefreshBus`.
**Where**: `mobile/.../core/network/*`, `core/data/dto/*` (novos DTOs de rede), `core/di/ApiModule.kt`
**Depends on**: M01
**Reuses**: `core/data/FirestoreHelpers.kt` (padrão `runOutcome`/`toDomainError`), design §17
**Requirement**: R2-08.2–R2-08.4, R2-08.9

**Done when**:
- [ ] `TokenStore`: tokens cifrados (não ficam em texto puro nas prefs); `clear()` no logout
- [ ] `AuthInterceptor` injeta `Bearer`; ausente token → não injeta (rotas de auth)
- [ ] `TokenAuthenticator`: em `401` faz **um** refresh (mutex, sem laço), repete a requisição; falha de refresh → limpa sessão e sinaliza logout
- [ ] `ProblemDetailsMapper`: `401→NotAuthenticated`, `403→PermissionDenied`, `404→NotFound`, `400/422→ValidationFailed`, `409→ConflictingState`, `429`→mensagem de limite, `IOException/timeout→NetworkUnavailable`; usa `errors[]`/`code` quando presentes
- [ ] `pollingFlow(interval=15s)` emite imediatamente e a cada intervalo enquanto coletado; `RefreshBus.invalidate(key)` força refetch imediato
- [ ] Testes MockWebServer: refresh transparente, refresh falho → logout, mapeamento de erros, polling/invalidação (≥ 10 testes)
- [ ] Gate: `./gradlew :app:testDebugUnitTest`

**Tests**: unit (MockWebServer)
**Gate**: full

---

### M03: Autenticação e sessão via API

**What**: Reescrever `SessionManager` e `LoginViewModel` para `/auth/*`; `UsersRepository` remoto; remover dependência de `FirebaseAuth` na sessão.
**Where**: `core/auth/SessionManager.kt`, `feature/auth/ui/LoginViewModel.kt`, `feature/auth/data/RemoteUsersRepository.kt`, `core/di/RepositoryModule.kt`, `feature/profile/ui/ProfileViewModel.kt` (logout)
**Depends on**: M02
**Reuses**: contratos existentes (`AuthSession`, `UiState<Role>`), quick-login dos 3 perfis
**Requirement**: R2-08.2, R2-01

**Done when**:
- [ ] Login chama `/auth/login`, guarda tokens, `currentUser` reflete `/auth/me`; `Success(role)` navega para a home do perfil (comportamento atual preservado)
- [ ] Reabrir o app mantém a sessão (tokens persistidos → `/auth/me`); token inválido → Login
- [ ] Logout chama `/auth/logout` (falha de rede não impede limpar sessão local) e volta ao Login
- [ ] Erros: credencial inválida → mensagem específica; 429 → "muitas tentativas"; sem rede → `NetworkUnavailable`
- [ ] Quick-login (líder/gestor/operador) funciona contra o backend
- [ ] Gate: compila + testes de `SessionManager`/`LoginViewModel` com fake `AuthApi` (≥ 5 testes)

**Tests**: unit
**Gate**: full

---

### M04: Orientações via API

**What**: `RemoteGuidelinesRepository` (CRUD + polling) e histórico de estratégia (consulta) se houver tela; interface `GuidelinesRepository` mantida.
**Where**: `feature/guidelines/data/RemoteGuidelinesRepository.kt`, `core/di/RepositoryModule.kt`, DTO/mappers
**Depends on**: M03, B11
**Reuses**: `FirestoreGuidelinesRepository.kt` (assinaturas), `GuidelinesRepository.kt`
**Requirement**: R2-08.1, R2-02

**Done when**:
- [ ] `observeAll/observe` via polling+invalidate; `create/update/delete` via REST e invalidam a lista
- [ ] Líder cria → item no topo imediatamente; editar reposiciona; excluir remove
- [ ] Gestor/operador não veem ações de escrita (UI atual) e o servidor retorna 403 se forçado (erro tratado)
- [ ] Campo `campaign` adicionado ao modelo/tela de admin (opcional)
- [ ] Gate: compila + testes MockWebServer do repositório (≥ 4 testes)

**Tests**: unit (MockWebServer)
**Gate**: full

---

### M05: Ideias via API (incl. ICE, aprovar, rejeitar)

**What**: `RemoteIdeasRepository`; `ApproveIdeaUseCase` vira chamada REST; ICE/reject via REST; stepper usa `linkedProject`.
**Where**: `feature/ideas/data/RemoteIdeasRepository.kt`, `core/domain/usecase/ApproveIdeaUseCase.kt`, `RejectIdeaUseCase.kt`, `core/domain/model/Idea.kt` (+`linkedProject`, `guidelineTitle`), mappers, `IdeasViewModels.kt`
**Depends on**: M04, B14
**Reuses**: `IdeasRepository.kt` (`createIdea/updateIdea/deleteIdea/saveIce/rejectIdea`, observes)
**Requirement**: R2-08.1, R2-08.5, R2-03

**Done when**:
- [ ] `observeByAuthor` → `scope=mine`; `observeForCuration` → `scope=curation`; `observeAll`/`observeByGuideline`/`observe(id)` mapeados
- [ ] `createIdea` exibe os pontos vindos do servidor (`pointsAwarded`); sem crédito calculado no cliente
- [ ] `ApproveIdeaUseCase` chama `POST /ideas/{id}/approve` e mantém `analytics.logIdeaApproved`; erro `SELF_APPROVAL_FORBIDDEN` → mensagem existente
- [ ] Stepper "Em execução" usa `linkedProject.stage`; "Orientação removida" quando `guidelineTitle == null` e `guidelineId != null`
- [ ] Nenhuma referência a `FirebaseFirestore` em ideias/usecases de ideias
- [ ] Gate: compila + MockWebServer (create/approve/reject/ice, erros 403/409) (≥ 8 testes)

**Tests**: unit (MockWebServer)
**Gate**: full

---

### M06: Projetos via API

**What**: `RemoteProjectsRepository`; remover `CompleteProjectUseCase` (conclusão é efeito do `PUT`).
**Where**: `feature/projects/data/RemoteProjectsRepository.kt`, `core/domain/usecase/CompleteProjectUseCase.kt` (remover), `ProjectsViewModels.kt`, mappers (diff `changes` tipado)
**Depends on**: M05, B16
**Reuses**: `ProjectsRepository.kt` (`ProjectInput`, `create/update/delete`, `observeUpdates`)
**Requirement**: R2-08.1, R2-08.5, R2-04

**Done when**:
- [ ] `FieldChange.from/to` desserializado de JSON tipado (número/string/null) e exibido pelo `TimelineEntry` como antes
- [ ] Editar projeto adiciona entrada no topo do histórico; `stage=CONCLUIDO` não dispara escrita extra no cliente (analytics `project_completed` mantido)
- [ ] `version` enviada no `PUT`; `409 CONCURRENCY_CONFLICT` → mensagem "projeto alterado por outro gestor"
- [ ] Operador não acessa; líder só lê (UI atual) e servidor confirma 403 em escrita
- [ ] Gate: compila + MockWebServer (≥ 6 testes: list, create, update com diff, 409, 403)

**Tests**: unit (MockWebServer)
**Gate**: full

---

### M07: Dashboard via `/reports` (sem cálculo local)

**What**: `ReportsRepository` (interface + remoto); `DashboardViewModel` consome `/reports/summary` com filtros; remover `DashboardComputer.kt`; drill-down por orientação usa `/reports/guidelines/{id}` + ideias/projetos por `guidelineId`.
**Where**: `core/domain/ReportsRepository.kt`, `feature/dashboard/data/RemoteReportsRepository.kt`, `feature/dashboard/ui/DashboardViewModel.kt`, `GuidelineDrillDownScreen.kt`, remove `feature/dashboard/DashboardComputer.kt`
**Depends on**: M06, B18
**Reuses**: `DashboardScreen.kt` (UI, filtros, modo apresentação), modelos `DashboardState/FunnelData/GuidelineImpact`
**Requirement**: R2-06.7, R2-08.6

**Done when**:
- [ ] Filtros período/divisão enviados ao servidor; troca de filtro refaz a chamada
- [ ] Funil, KPIs, sparkline (com `null`→"—"), impacto por orientação, lista por ROI renderizados a partir da resposta (UI/modo apresentação inalterados)
- [ ] `roiPercent == null` mostra "—"
- [ ] `DashboardComputer.kt` removido; nenhum cálculo de ROI/funil no cliente
- [ ] Atualização automática ≤ 15 s e imediata após editar projeto (invalidate)
- [ ] Gate: compila + testes do `DashboardViewModel` com repositório fake (≥ 3 testes) + MockWebServer do `RemoteReportsRepository` (≥ 3)

**Tests**: unit
**Gate**: full

---

### M08: Ranking mensal e perfil com badges reais [P]

**What**: Ranking via `/users/ranking`; perfil lê pontos/badges de `/auth/me`; lista de responsáveis via `/users?role=`.
**Where**: `feature/auth/data/RemoteUsersRepository.kt` (`topByPointsThisMonth`, `listByRole`), `feature/profile/ui/ProfileViewModel.kt`, `RankingTop5.kt`
**Depends on**: M03, B12
**Reuses**: `UsersRepository.kt`
**Requirement**: R2-05.4, R2-05.5, R2-08.6

**Done when**:
- [ ] Ranking mostra `monthPoints` do mês corrente (não mais pontos totais)
- [ ] Badges conquistadas (persistidas pelo servidor) aparecem coloridas no perfil; atualiza após aprovar/concluir
- [ ] Gate: compila + MockWebServer (≥ 3 testes)

**Tests**: unit
**Gate**: full

---

### M09: Card "Insights da IA" no dashboard

**What**: Tela/seção que chama `POST /reports/insights` com os filtros ativos e exibe resultado estruturado.
**Where**: `core/domain/ReportsRepository.kt` (`generateInsights`), `feature/dashboard/ui/InsightsCard.kt`, `DashboardViewModel.kt`/`InsightsViewModel.kt`, `core/domain/model/Insights.kt`
**Depends on**: M07, B20
**Reuses**: componentes de `Common.kt` (EmptyState, cards), tema
**Requirement**: R2-07.8

**Done when**:
- [ ] Botão "✨ Gerar insights" (só líder) → estado Loading (indicador) → Success
- [ ] Sucesso: parágrafo de resumo + seções **Destaques / Riscos / Recomendações** (chips de prioridade ALTA/MÉDIA/BAIXA, referência à orientação quando houver), selo "Gerado por IA — valide antes de decidir", data/hora e indicação de cache
- [ ] Erros `503 AI_UNAVAILABLE`/`502`/`429`: mensagem amigável + "Tentar novamente"; **nunca** mostra conteúdo falso
- [ ] Botão "Atualizar" envia `refresh=true`
- [ ] Respeita filtros do dashboard; funciona no modo apresentação (ocultável)
- [ ] Gate: compila + testes do ViewModel de insights (loading/sucesso/erro/retry) (≥ 4 testes) + MockWebServer (≥ 2)

**Tests**: unit
**Gate**: full

---

### M10: Remoção do Firebase Auth/Firestore e limpeza

**What**: Eliminar código e dependências do Firebase Auth/Firestore; manter Analytics/Crashlytics; atualizar testes e README.
**Where**: `mobile/app/build.gradle.kts`, `libs.versions.toml`, `core/di/FirebaseModule.kt` (só Analytics/Crashlytics), remover `FirestoreHelpers.kt`, `Firestore*Repository.kt`, DTOs Firestore, `firestore.rules`, `firebase.json`, `firebase/firestore.indexes.json`, `mobile/README.md`, `core/data/mapper/*`, testes
**Depends on**: M03–M09
**Reuses**: —
**Requirement**: R2-08.8, R2-08.9

**Done when**:
- [ ] `git grep -nE "FirebaseFirestore|FirebaseAuth|com.google.firebase.(auth|firestore)"` em `mobile/app/src` → **0** ocorrências
- [ ] `firebase-auth` e `firebase-firestore` removidos das dependências; `google-services`/Analytics/Crashlytics preservados
- [ ] `MapperTest` migrado para DTOs de rede (round-trip por entidade, `null` de `targetDate/ice/guidelineId`)
- [ ] `IceTest`, `BadgeEvaluatorTest` mantidos (o avaliador do app deixa de ser usado em produção → decidir: remover do app junto com o teste **ou** manter; registrar decisão)
- [ ] `mobile/README.md` atualizado (stack, setup do backend, `api.baseUrl`, sem instruções de Firestore)
- [ ] Gate: `./gradlew :app:compileDebugKotlin :app:testDebugUnitTest` verde

**Tests**: unit
**Gate**: full

---

### M11: E2E no emulador contra o backend real + APK release

**What**: Validar todos os fluxos no emulador com o backend real e gerar o APK assinado apontando para a URL publicada.
**Where**: checklist em `mobile/docs/E2E_CHECKLIST.md`, `mobile/app/build.gradle.kts` (signing já existente), README
**Depends on**: M10, B24
**Reuses**: configuração de assinatura da Sprint 1 (`keystore.properties`)
**Requirement**: R2-08, R2-11.2

**Done when**:
- [ ] Checklist executado e marcado (evidências/prints): login 3 perfis; líder cria/edita/exclui orientação e vê histórico; operador cadastra ideia (+15/+10) e vê stepper; gestor salva ICE, aprova (projeto rascunho, +50), rejeita com comentário, edita projeto (timeline com diff), conclui (ideia IMPLEMENTADA, +200, badge "Impacto Real"); líder abre dashboard (funil/KPIs/sparkline/impacto), aplica filtros, modo apresentação, **gera insights de IA**; token expirado → refresh silencioso; sem rede → erro tratado; operador tentando rota de gestor/líder → bloqueio
- [ ] Ranking mostra pontos **do mês**; badges reais no perfil
- [ ] `./gradlew :app:assembleRelease` gera APK assinado com `API_BASE_URL` HTTPS do deploy (B24); instala e abre em dispositivo/emulador limpo
- [ ] Gate: `./gradlew :app:assembleRelease`

**Tests**: none (validação manual)
**Gate**: build

---

## Task Breakdown — Documentação e Entregáveis

### D01: Especificação dos endpoints (`docs/api/ENDPOINTS.md`)

**What**: Gerar a especificação (rota, método, perfis, payload, resposta, erros) a partir do OpenAPI do backend.
**Where**: `docs/api/openapi.json`, `docs/api/ENDPOINTS.md`
**Depends on**: B24
**Reuses**: Swagger exportado (`/swagger/v1/swagger.json`), tabela do spec
**Requirement**: R2-11.4

**Done when**:
- [ ] Todos os endpoints do contrato listados com método, rota, perfis, exemplo de request/response e códigos de erro
- [ ] Gerado a partir do `swagger.json` (script reproduzível), sem edição manual divergente
- [ ] Gate: revisão contra a tabela do spec (nenhum endpoint faltando)

**Tests**: none
**Gate**: —

---

### D02: Diagrama de arquitetura do backend + seção do modelo de IA

**What**: Diagrama (camadas, Mongo, Gemini, app) em PNG/SVG e texto explicando o modelo de IA e a funcionalidade escolhida.
**Where**: `docs/arquitetura/backend-arquitetura.{md,png}`, `docs/arquitetura/ia-insights.md`
**Depends on**: D01
**Reuses**: design §1, §9, §22
**Requirement**: R2-11.3

**Done when**:
- [ ] Diagrama mostra App → API (Controllers→Application→Domain←Infrastructure) → MongoDB e → Gemini, com autenticação JWT
- [ ] Seção de IA: modelo usado (nome real configurado), por que insights sobre dashboards, entrada/saída, guardrails (sem PII, schema, cache, cota), limitações e exemplo real de resposta

**Tests**: none
**Gate**: —

---

### D03: Apresentação (PDF/PPT)

**What**: Deck final conforme o enunciado.
**Where**: `docs/apresentacao/Sprint2_AguiaBranca.{pptx,pdf}`
**Depends on**: D02, M11
**Reuses**: D01, D02, prints do M11
**Requirement**: R2-11.3

**Done when**:
- [ ] Nome e **RM** de todos os integrantes (a preencher pelo grupo)
- [ ] Diagrama de arquitetura do backend
- [ ] Especificação dos endpoints (rota, método, payload, resposta)
- [ ] Modelo de IA e funcionalidade escolhida (com demonstração/print)
- [ ] Demonstração do fluxo integrado app ↔ backend; migração Firebase→MongoDB; segurança (JWT/roles)

**Tests**: none
**Gate**: —

---

### D04: Empacotamento e checklist final

**What**: Gerar os `.zip` e validar que rodam a partir do zero.
**Where**: `dist/backend.zip`, `dist/app.zip` (+ APK), checklist
**Depends on**: D03, B24, M11
**Reuses**: —
**Requirement**: R2-11.1, R2-11.2

**Done when**:
- [ ] `backend.zip` sem `bin/ obj/ .env`, sem segredos, sem `spikes/`; contém README; extraído em pasta limpa: `docker compose up --build` funciona e `dotnet test` passa
- [ ] `app.zip` contém projeto completo (sem `build/`, `local.properties`, `keystore.properties`) + `app-release.apk`; extraído em pasta limpa compila (`./gradlew :app:assembleDebug`)
- [ ] Varredura de segredos nos dois zips (chave Gemini, JWT key, connection string, keystore) → nenhum achado
- [ ] Checklist do enunciado marcado: backend zip ✔ · app zip + APK ✔ · apresentação com RM/arquitetura/endpoints/IA ✔

**Tests**: none
**Gate**: —

---

## Granularity Check

| Task | Escopo | Status |
|---|---|---|
| B01 Scaffold | 1 solution + config | ✅ |
| B03 Spike | 6 verificações descartáveis, saída = decisão | ✅ (timeboxed) |
| B04 Domain | Entidades + regras + testes | ⚠️ grande, coesa (mesmo projeto, sem I/O) |
| B06 Persistência | DbContext + repos + UoW + índices | ⚠️ grande, coesa — dividir se o spike mostrar fallback para driver puro |
| B09 Auth | JWT + refresh + 4 endpoints | ⚠️ grande; refresh rotativo é *cortável* (ver ADR-004) |
| B13/B14 Ideias | CRUD / curadoria+aprovação | ✅ (divididas por risco: B14 concentra a transação crítica) |
| B15/B16 Projetos | CRUD+diff / conclusão | ✅ |
| B19/B20 IA | cliente / caso de uso | ✅ |
| M02 Rede | 6 artefatos de infraestrutura | ⚠️ grande, coesa — base de todo o app |
| M05 Ideias | repo + 2 usecases + stepper | ⚠️ borderline mas coesa |

## Diagram-Definition Cross-Check

| Task | Depends on (corpo) | Diagrama mostra | Status |
|---|---|---|---|
| B01 | — | — | ✅ |
| B02 | B01 | B01→B02 | ✅ |
| B03 | B02 | B02→B03 | ✅ |
| B04 | B03 | B03→B04 | ✅ |
| B05 | B04 | B04→B05 | ✅ |
| B06 | B05 | B05→B06 | ✅ |
| B07 | B06 | B06→B07 | ✅ |
| B08 | B06 | B07→B08 (ordem sequencial; dependência real: B06) | ✅ |
| B09 | B07, B08 | B08→B09 | ✅ |
| B10 | B09 | B09→B10 | ✅ |
| B11 | B10 | B10→B11 | ✅ |
| B12 | B11 | B11→B12 | ✅ |
| B13 | B12 | B12→B13 | ✅ |
| B14 | B13 | B13→B14 | ✅ |
| B15 | B14 | B14→B15 | ✅ |
| B16 | B15 | B15→B16 | ✅ |
| B17 | B16 | B16→B17 | ✅ |
| B18 | B17 | B17→B18 | ✅ |
| B19 [P] | B10 | B10→B19 (paralela) | ✅ |
| B20 | B18, B19 | B18+B19→B20 | ✅ |
| B21 | B20 | B20→B21 | ✅ |
| B22 | B21 | B21→B22 | ✅ |
| B23 [P] | B06, B12 | paralela após B22 | ✅ (pode iniciar antes) |
| B24 | B22 | B22→B24 | ✅ |
| M01 | B09 | B09→M01 | ✅ |
| M02 | M01 | M01→M02 | ✅ |
| M03 | M02 | M02→M03 | ✅ |
| M04 | M03, B11 | B11→M04 | ✅ |
| M05 | M04, B14 | B14→M05 | ✅ |
| M06 | M05, B16 | B16→M06 | ✅ |
| M07 | M06, B18 | B18→M07 | ✅ |
| M08 [P] | M03, B12 | B12→M08 | ✅ |
| M09 | M07, B20 | B20→M09 | ✅ |
| M10 | M03–M09 | M03..M09→M10 | ✅ |
| M11 | M10, B24 | M10→M11 | ✅ |
| D01 | B24 | B24→D01 | ✅ |
| D02 | D01 | D01→D02 | ✅ |
| D03 | D02, M11 | D02→D03 | ✅ |
| D04 | D03, B24, M11 | D03→D04 | ✅ |

**Nota B14 → B15:** `approve` (B14) cria um `Project` e um `ProjectUpdate`. A entidade/repositório de projeto já existem (B04/B06), então B14 não depende de B15; B15 apenas adiciona os endpoints e o diff. A ordem sequencial mantém o risco concentrado nas transações (B14, B16) logo no início.

**Corte de escopo (se o prazo apertar), na ordem:** (1) refresh token rotativo (B09/M02 — usar access token longo), (2) B23 Migrator com relatório (manter apenas transformação + dry-run), (3) B21 itens não críticos (headers extras), (4) `GET /guidelines/history` na UI (manter só API). **Não cortar:** B03, B14, B16, B17/B18, B20 (IA), M05–M07, M11.

## Test Co-location Validation

| Task | Camada | Matriz exige | Task diz | Status |
|---|---|---|---|---|
| B04 | Domain | unit | unit | ✅ |
| B05 | Application common | unit | unit | ✅ |
| B06 | Infra persistência | integração | integration | ✅ |
| B07 | Api base | integração | integration | ✅ |
| B08 | Identity stores | integração | integration | ✅ |
| B09 | Auth | unit + integração | unit + integration | ✅ |
| B10 | Policies/seed | integração | integration | ✅ |
| B11–B16 | Features | unit + integração | unit + integration | ✅ |
| B17 | ReportCalculator | unit (golden) | unit | ✅ |
| B18 | Reports API | integração | integration | ✅ |
| B19 | GeminiClient | unit (fake handler) | unit | ✅ |
| B20 | Insights | unit + integração | unit + integration | ✅ |
| B21–B22 | Hardening/authz/E2E | integração | integration | ✅ |
| B23 | Migrator | unit | unit | ✅ |
| M02 | Rede | unit MockWebServer | unit | ✅ |
| M03–M09 | Repos remotos/VMs | unit | unit | ✅ |
| Telas (Composables) | UI | none | none (E2E manual M11) | ✅ |

---

## Resumo

- **24 tasks de backend** (B01–B24), **11 de mobile** (M01–M11) e **4 de entregáveis** (D01–D04) = **39 tasks**
- **Riscos tratados primeiro:** spike do provider EF Core Mongo (B03) antes de qualquer feature; transações críticas (B14, B16) logo após auth
- **Testes obrigatórios:** golden do `ReportCalculator` (paridade com o Kotlin), **matriz de autorização** por endpoint × perfil, integração com Mongo real, MockWebServer no app, ausência de PII no payload da IA
- **IA (Plus):** insights de dashboard com Google Gemini — B19 (cliente), B20 (caso de uso), M09 (UI), D02 (explicação para a apresentação)
- Toda task é rastreável a `R2-XX` (spec) ou a um ponto aberto `OP-X/DS-X`
