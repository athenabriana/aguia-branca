# ÁGUIA BRANCA

Plataforma móvel de inovação corporativa do Grupo Águia Branca — Sprint 1 do Challenge FIAP 2026.

App Android nativo (Kotlin + Jetpack Compose + Firebase) que conecta **estratégia → execução → mensuração** da inovação, ligando líderes, gestores e operadores em um único fluxo.

## Stack

- Kotlin 2.1 · Jetpack Compose (BOM 2024.12.01) · Material 3
- Arquitetura: MVVM + Repository + UseCases + DTO↔Domain mapping
- DI: Hilt 2.55 · Navigation Compose type-safe com kotlinx.serialization
- Firebase BOM 33.7: Auth (email/senha), Cloud Firestore, Crashlytics, Analytics
- Splash Screen 1.0.1 + Edge-to-edge + Predictive Back
- Testes: JUnit 4 · MockK · Turbine · kotlinx-coroutines-test
- Build: Gradle 8.11 + AGP 8.7.3 + JDK 17 (compatível JDK 21)
- compileSdk **35** · minSdk **24** · targetSdk **35**

## Pré-requisitos para build

1. JDK 17+ (ou JDK 21)
2. Android SDK com `platforms;android-35` + `build-tools;35.0.0`
3. Variáveis: `ANDROID_HOME` apontando para o SDK; `JAVA_HOME` opcional

## Setup Firebase (obrigatório para rodar)

1. Crie um projeto no [Firebase Console](https://console.firebase.google.com/).
2. Adicione um app Android com `com.aguiabranca.app` **e** `com.aguiabranca.app.debug` (build debug usa suffix).
3. Ative em **Authentication** → método **Email/Senha**.
4. Ative **Cloud Firestore** em modo de produção; abra as regras para autenticados:
   ```
   rules_version = '2';
   service cloud.firestore {
     match /databases/{database}/documents {
       match /{document=**} {
         allow read, write: if request.auth != null;
       }
     }
   }
   ```
5. Baixe o `google-services.json` real e substitua o placeholder em `app/google-services.json`.
6. (Opcional) Aplique índices do Firestore:
   ```
   firebase deploy --only firestore:indexes
   ```

## Comandos

```bash
# Compilar debug
./gradlew :app:compileDebugKotlin

# Rodar testes unit
./gradlew :app:testDebugUnitTest

# Gerar APK release (não assinado se keystore.properties não existir)
./gradlew :app:assembleRelease

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

## Seed de dados de demonstração

1. Abra o app no emulador / dispositivo conectado ao Firebase configurado.
2. Na tela de login, faça **long-press no logo** "IG" (centralizado).
3. Aguarde "Seed concluído". A função cria:
   - 3 usuários: `lider@aguiabranca.com`, `gestor@aguiabranca.com`, `operador@aguiabranca.com` (senha `aguiabranca123`)
   - 4 orientações estratégicas
   - 6 ideias em diferentes status (com e sem ICE, com e sem vínculo a orientação)
   - 3 projetos (1 concluído com ROI positivo, 1 em execução, 1 em planejamento)

## Perfis e funcionalidades

| Perfil | Capacidades |
|---|---|
| **Operador** | Cadastrar ideias, acompanhar status (timeline), ver orientações vigentes, ver ranking top 5, perfil com badges |
| **Gestor** | Curar ideias com matriz **ICE**, aprovar (cria projeto rascunho automaticamente) ou rejeitar, criar/editar/excluir projetos (com histórico) |
| **Líder** | CRUD de orientações estratégicas, dashboard (funil, KPIs, sparkline ROI, "Impacto por Orientação", modo apresentação fullscreen com count-up), drill-down por orientação |

## Documentação técnica

Para detalhes de arquitetura, modelo de dados, fluxos críticos (aprovação→projeto, completar projeto, impacto por orientação), consulte:
- `.specs/features/sprint1/spec.md` — requisitos com IDs rastreáveis
- `.specs/features/sprint1/design.md` — arquitetura completa
- `docs/DOCUMENTACAO_TECNICA.md` — documentação técnica do Sprint 1

## Estrutura do projeto

```
app/src/main/java/com/aguiabranca/app/
├── AguiaBrancaApplication.kt        # Hilt root + Crashlytics
├── MainActivity.kt               # Splash, edge-to-edge, predictive back
├── bootstrap/SeedData.kt
├── core/
│   ├── auth/SessionManager.kt
│   ├── data/{dto,mapper,FirestoreHelpers}
│   ├── di/{FirebaseModule, RepositoryModule}
│   ├── domain/
│   │   ├── model/                # User, Guideline, Idea, Project, Ice…
│   │   ├── error/                # DomainError + Outcome
│   │   ├── usecase/              # ApproveIdea, RejectIdea, CompleteProject
│   │   ├── badge/BadgeEvaluator.kt
│   │   └── *Repository.kt        # Interfaces
│   ├── ui/components/            # Theme tokens, charts, badges, scaffolds
│   ├── ui/local/LocalSession.kt
│   └── util/Analytics.kt
├── feature/
│   ├── auth/                     # Login + Seed
│   ├── guidelines/               # CRUD + listagem
│   ├── ideas/                    # CRUD + curadoria + ICE + jornada
│   ├── projects/                 # CRUD + timeline
│   ├── dashboard/                # Computer + screen + drill-down
│   └── profile/                  # Pontos + badges + ranking
└── navigation/{Routes, AppNavHost}.kt
```

## Sprint 2 (próximo passo)

Substituir a camada Firestore (`feature/**/data/Firestore*Repository.kt`) por implementação que chame uma API Java/C# REST, mantendo as interfaces do pacote `core/domain/`.
