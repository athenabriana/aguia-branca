# INOVAGAB — Plataforma de Inovação Corporativa

**Cliente:** Grupo Águia Branca
**Contexto acadêmico:** FIAP Challenge 2026 — Sprint 1
**Deadline:** 26/05/2026 às 23:00
**Valor:** 10 pontos

## Visão

Aplicativo móvel nativo Android que integra a gestão da inovação corporativa do Grupo Águia Branca, conectando três níveis organizacionais (operacional, tático, estratégico) em um único ambiente para capturar, estruturar e acompanhar iniciativas de inovação que gerem impacto mensurável.

## Propósito

Endereçar os quatro desafios identificados pela Águia Branca:

1. **Conexão estratégica** — alinhar a alta gestão à execução prática da inovação na ponta.
2. **Engajamento operacional** — incluir colaboradores de todos os níveis no processo criativo.
3. **Visibilidade do funil** — acompanhar a jornada desde a ideia inicial até os resultados.
4. **Mensuração de valor** — demonstrar impacto real e ROI das iniciativas.

## Cinco pilares cobertos no Sprint 1

| Pilar | Quem é responsável | Como o app entrega |
|---|---|---|
| Direcionamento estratégico | Líderes | CRUD de orientações estratégicas |
| Gestão de ideias | Operadores → Gestores | Cadastro, curadoria, priorização ICE, aprovação |
| Inovação aberta | (Sprint futuro) | Não no Sprint 1 |
| Gestão de projetos | Gestores → Líderes | Cadastro, atualização, acompanhamento |
| Mensuração | Líderes | Dashboard de resultados consolidados |

## Perfis de usuário

| Perfil | Nível | Capacidades-chave |
|---|---|---|
| **Operador** | Operacional | Consultar orientações, cadastrar ideias, acompanhar status |
| **Gestor** | Tático | Consultar orientações, priorizar/aprovar ideias, gerir projetos |
| **Líder** | Estratégico | CRUD orientações, consultar projetos, ver dashboard |

## Stack técnica (Sprint 1) — versões alvo, maio/2026

| Camada | Tecnologia | Versão |
|---|---|---|
| Plataforma | Android nativo | minSdk **24**, targetSdk **35** |
| Linguagem | Kotlin | **2.3.20** (estável) |
| UI | Jetpack Compose + Material 3 | Compose BOM **2026.05.00** |
| Compose Compiler | Plugin oficial Kotlin | `org.jetbrains.kotlin.plugin.compose` |
| Arquitetura | MVVM + Repository + UseCases pontuais + Dto↔Domain mapping | — |
| Estado | ViewModel + StateFlow + SavedStateHandle | androidx.lifecycle:*:2.9.x |
| Navegação | Navigation Compose type-safe (Kotlin Serialization) | androidx.navigation:navigation-compose:2.10.x |
| Serialização | kotlinx.serialization | **1.8.x** |
| DI | **Hilt** | `com.google.dagger:hilt-android:2.55` |
| Auth | Firebase Authentication (email/senha) | Firebase BOM **34.13.0** |
| Persistência | Cloud Firestore | Firebase BOM **34.13.0** |
| Observabilidade | Firebase Crashlytics + Analytics | Firebase BOM **34.13.0** |
| Splash | androidx.core:core-splashscreen | **1.0.1** |
| Edge-to-edge | androidx.activity:activity-compose `enableEdgeToEdge()` | **1.10.x** |
| Build system | Gradle Kotlin DSL + Version Catalog (`libs.versions.toml`) | Gradle **9.5.1** |
| Android Gradle Plugin | AGP | **9.2.0** |
| JDK | JDK | **17** |
| Testes | JUnit 4 + MockK + Turbine + kotlinx-coroutines-test | latest stable |

**Notas:**
- targetSdk 35 alinha com requisito atual do Google Play (mesmo entregando APK direto, demonstra aderência ao stack 2026).
- Kotlin 2.x usa o Compose Compiler Plugin oficial; não precisa mais especificar versão separada do compiler.
- AGP 9.2.0 estabilizou Kotlin 2.x compatibility e exige Gradle ≥ 8.11.
- Compose BOM e Firebase BOM mantêm todos os módulos do mesmo grupo sincronizados; só especificar a BOM e omitir versões nos artefatos individuais.

Sprint 2 substituirá o Firebase Firestore por um backend Java/C# REST, mantendo a app cliente.

## Critérios de avaliação (referência)

| Critério | Peso |
|---|---|
| Adequação ao problema | 20% |
| Implementação técnica funcional | 30% |
| Qualidade do código | 25% |
| Apresentação e documentação | 15% |
| Criatividade e inovação | 10% |

## Entregáveis Sprint 1

1. APK release assinado
2. Código-fonte completo zipado
3. Documentação técnica (PDF/PPT/Markdown)
4. Vídeo demonstrativo ≤ 5 minutos
