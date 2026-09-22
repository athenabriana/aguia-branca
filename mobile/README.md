# ÁGUIA BRANCA

Plataforma móvel de inovação corporativa do Grupo Águia Branca — Challenge FIAP 2026 (Sprint 1: app · **Sprint 2: integrado ao backend .NET + MongoDB + IA**).

App Android nativo (Kotlin + Jetpack Compose + API REST própria) que conecta **estratégia → execução → mensuração** da inovação, ligando líderes, gestores e operadores em um único fluxo.

## Stack

- Kotlin 2.1 · Jetpack Compose (BOM 2024.12.01) · Material 3
- Arquitetura: MVVM + Repository + UseCases + DTO↔Domain mapping
- DI: Hilt 2.55 · Navigation Compose type-safe com kotlinx.serialization
- Rede: Retrofit 2.11 + OkHttp 4.12 + kotlinx-serialization (backend em [`../backend`](../backend/README.md): .NET 8, JWT, MongoDB)
- Sessão: tokens JWT **cifrados** (AES-GCM no Android Keystore) em DataStore; refresh transparente em `401`
- "Tempo real" por polling de 15 s com invalidação imediata após cada escrita
- Firebase BOM 33.7: **somente** Crashlytics e Analytics (Auth e Firestore foram removidos na Sprint 2)
- Splash Screen 1.0.1 + Edge-to-edge + Predictive Back
- Testes: JUnit 4 · MockK · Turbine · kotlinx-coroutines-test · MockWebServer
- Build: Gradle 8.11 + AGP 8.7.3 + **JDK 17 ou 21** (o Gradle 8.11 **não roda em JDK 24**: falha com "Type T not present"; use o JBR do Android Studio ou um JDK 17/21 em `JAVA_HOME`)
- compileSdk **35** · minSdk **24** · targetSdk **35**

## Pré-requisitos para build

1. JDK 17 ou 21
2. Android SDK com `platforms;android-35` + `build-tools;35.0.0`
3. Variáveis: `ANDROID_HOME` apontando para o SDK; `JAVA_HOME` opcional

## Backend (obrigatório para rodar)

O app conversa com a API do repositório (`../backend`). Suba-a antes:

```bash
cd ../backend
cp .env.example .env        # preencha JWT_KEY (openssl rand -base64 48); GEMINI_* é opcional (insights de IA)
docker compose --profile api up --build -d   # Mongo (replica set) + API em http://localhost:5080
```

| Build | URL da API | Como |
|---|---|---|
| **debug** (emulador) | `http://10.0.2.2:5080/api/v1/` (o `10.0.2.2` é a sua máquina vista do emulador) | fixa no `build.gradle.kts`; HTTP liberado **só** para `10.0.2.2` e `localhost` (`src/debug/res/xml/network_security_config.xml`) |
| **release** | **HTTPS** obrigatório | `-Papi.baseUrl=https://sua-api/api/v1/` ou `api.baseUrl=…` no `local.properties` (gitignored). Sem isso, `assembleRelease` falha com mensagem clara |

Em **dispositivo físico** na mesma rede, use um build com a URL da LAN (`https` ou ajuste o `network_security_config` de debug para o IP) — o `10.0.2.2` só existe no emulador.

O `app/google-services.json` continua necessário apenas para **Analytics e Crashlytics** (Firebase Console → app Android `com.aguiabranca.app` e `.debug`). A autenticação e os dados **não** usam mais o Firebase.

## Comandos

```bash
# Compilar debug
./gradlew :app:compileDebugKotlin  # (com JAVA_HOME em JDK 17/21)

# Rodar testes unit
./gradlew :app:testDebugUnitTest

# Gerar APK release (assinado se keystore.properties existir; exige api.baseUrl HTTPS)
./gradlew :app:assembleRelease -Papi.baseUrl=https://sua-api/api/v1/

# APK debug instalável
./gradlew :app:assembleDebug
```

## Assinatura do APK release

Crie um arquivo `keystore.properties` na raiz (gitignored):
```
storeFile=keystore/release.jks
storePassword=...
keyAlias=aguiabranca
keyPassword=...
```

Gere o keystore com:
```
keytool -genkey -v -keystore keystore/release.jks -alias aguiabranca -keyalg RSA -keysize 2048 -validity 10000
```

## Usuários de demonstração

| Email | Senha | Perfil |
|---|---|---|
| `lider@aguiabranca.com` | `aguiabranca123` | Líder |
| `gestor@aguiabranca.com` | `aguiabranca123` | Gestor |
| `operador@aguiabranca.com` | `aguiabranca123` | Operador |

## Perfis e funcionalidades

| Perfil | Capacidades |
|---|---|
| **Operador** | Cadastrar ideias (pontos concedidos pelo servidor), acompanhar a jornada (incl. "Em execução" pelo projeto vinculado), orientações vigentes, ranking **do mês**, perfil com badges reais |
| **Gestor** | Curar ideias com matriz **ICE**, aprovar (cria projeto rascunho automaticamente) ou rejeitar, criar/editar/excluir projetos (com histórico) |
| **Líder** | CRUD de orientações (com campanha), dashboard calculado no servidor (funil, KPIs, projetos atrasados, sparkline ROI, impacto por orientação, modo apresentação), drill-down e **✨ Insights da IA** (Gemini: destaques, riscos e recomendações) |

## Documentação técnica

- Sprint 2: [`.specs/features/sprint2/`](../.specs/features/sprint2) (spec, design, tasks), [`../backend/README.md`](../backend/README.md) e [`docs/E2E_CHECKLIST.md`](docs/E2E_CHECKLIST.md)
- Sprint 1: `.specs/features/sprint1/` e `docs/DOCUMENTACAO_TECNICA.html`

## Estrutura do projeto

```
app/src/main/java/com/aguiabranca/app/
├── AguiaBrancaApplication.kt        # Hilt root + Crashlytics
├── MainActivity.kt                  # Splash (espera restaurar a sessão), edge-to-edge
├── core/
│   ├── auth/SessionManager.kt       # /auth/login|me|logout, tokens cifrados
│   ├── network/                     # Retrofit APIs, DTOs @Serializable, TokenStore, AuthInterceptor,
│   │                                #   TokenAuthenticator (refresh), ProblemDetailsMapper, Polling/RefreshBus
│   ├── di/{NetworkModule, RepositoryModule, FirebaseModule}   # Firebase = só Analytics/Crashlytics
│   ├── domain/
│   │   ├── model/                   # User, Guideline, Idea, Project, Dashboard, Insights…
│   │   ├── error/                   # DomainError + Outcome
│   │   ├── usecase/                 # ApproveIdea (REST), RejectIdea
│   │   └── *Repository.kt           # Interfaces (inalteradas desde a Sprint 1)
│   ├── ui/components/, ui/local/    # Tema, gráficos, badges, scaffolds
│   └── util/Analytics.kt
├── feature/
│   ├── auth/                        # Login + RemoteUsersRepository
│   ├── guidelines/                  # CRUD (com campanha) + RemoteGuidelinesRepository
│   ├── ideas/                       # CRUD + curadoria + ICE + jornada + RemoteIdeasRepository
│   ├── projects/                    # CRUD + timeline + RemoteProjectsRepository (versão / 409)
│   ├── dashboard/                   # Dashboard, drill-down, InsightsCard + RemoteReportsRepository
│   └── profile/                     # Pontos + badges + ranking do mês
└── navigation/{Routes, AppNavHost}.kt
```

## Como as regras de negócio funcionam agora

O servidor é a fonte da verdade: pontos, badges, aprovação de ideia (projeto rascunho, +50), conclusão de projeto (ideia
implementada, +200), ranking mensal e o cálculo do dashboard. O app só envia comandos (`POST /ideas/{id}/approve`,
`PUT /projects/{id}`…) e renderiza o resultado. `409 CONCURRENCY_CONFLICT` → "projeto alterado por outro gestor".
