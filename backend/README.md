# INOVAGAB — Backend (Grupo Águia Branca)

API REST da plataforma de inovação corporativa **INOVAGAB**: orientações estratégicas, ideias, projetos, relatórios e insights de IA. Substitui o Firebase (Auth + Firestore) usado pelo app Android na Sprint 1.

- **Stack:** C# · .NET 8 · ASP.NET Core Web API · ASP.NET Identity + JWT · Entity Framework Core · **MongoDB**
- **Documentação de referência:** [`spec.md`](../.specs/features/sprint2/spec.md) (requisitos e contrato de API) · [`design.md`](../.specs/features/sprint2/design.md) (arquitetura e decisões) · [`tasks.md`](../.specs/features/sprint2/tasks.md) (plano e andamento)

## Sumário

1. [Status](#1-status)
2. [Pré-requisitos](#2-pré-requisitos)
3. [Início rápido](#3-início-rápido)
4. [Configuração](#4-configuração)
5. [Usuários de demonstração](#5-usuários-de-demonstração)
6. [Experimentando a API](#6-experimentando-a-api)
7. [Testes](#7-testes)
8. [Arquitetura e estrutura de pastas](#8-arquitetura-e-estrutura-de-pastas)
9. [Conectando o app Android](#9-conectando-o-app-android)
10. [Solução de problemas](#10-solução-de-problemas)

---

## 1. Status

Backend em construção (Sprint 2). O que já está pronto e funcionando:

| Área | Situação |
|---|---|
| Arquitetura em camadas, persistência MongoDB (transações, índices), erros padronizados, logs, health checks, Swagger | ✅ |
| Autenticação: login, refresh de token rotativo, logout, `/me` (JWT, lockout, rate limit) | ✅ |
| Autorização por perfil (`OPERADOR`, `GESTOR`, `LIDER`) + dados de demonstração (seed) | ✅ |
| Orientações estratégicas (CRUD do líder, leitura para todos) + **registro histórico** (id, data, categoria, campanha) | ✅ |
| Ideias: cadastro, edição, exclusão, curadoria (ICE), rejeição e **aprovação → projeto rascunho** | ✅ |
| Gamificação no servidor: pontos, badges, ranking mensal | ✅ |
| Projetos · Relatórios/dashboard · **Insights de IA (Gemini)** | ⏳ próximas fases |
| Ferramenta de migração Firebase → MongoDB | ⏳ próximas fases |

### Endpoints disponíveis hoje

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| `POST` | `/api/v1/auth/login` | público | E-mail + senha → `accessToken` (30 min), `refreshToken` (7 dias) e perfil |
| `POST` | `/api/v1/auth/refresh` | público | Troca o refresh token por um novo par (o anterior deixa de valer) |
| `POST` | `/api/v1/auth/logout` | autenticado | Revoga a sessão do refresh token informado |
| `GET` | `/api/v1/auth/me` | autenticado | Perfil, pontos e badges atualizados |
| `GET` | `/api/v1/guidelines` | autenticado | Lista paginada (`page`, `pageSize` ≤ 200), da mais recentemente alterada |
| `GET` | `/api/v1/guidelines/{id}` | autenticado | Detalhe |
| `POST` | `/api/v1/guidelines` | **LIDER** | Cria (`title`, `description`, `pillar`, `campaign?`) |
| `PUT` | `/api/v1/guidelines/{id}` | **LIDER** | Edita |
| `DELETE` | `/api/v1/guidelines/{id}` | **LIDER** | Exclui (o histórico é preservado) |
| `GET` | `/api/v1/guidelines/history` | autenticado | Histórico com filtros `guidelineId`, `category`, `campaign`, `from`, `to` |
| `GET` | `/api/v1/ideas` | autenticado | Lista paginada; `scope` = `mine` \| `curation` \| `all`, filtros `status`, `guidelineId`, `division`. Operador só vê as próprias |
| `GET` | `/api/v1/ideas/{id}` | autenticado | Detalhe (com `guidelineTitle`, `ice` e `linkedProject`) |
| `POST` | `/api/v1/ideas` | OPERADOR, GESTOR | Cadastra; credita +10 (+5 com orientação) |
| `PUT` · `DELETE` | `/api/v1/ideas/{id}` | autor | Edita / exclui enquanto `SUBMETIDA` (exclusão estorna os pontos) |
| `PUT` | `/api/v1/ideas/{id}/ice` | **GESTOR** | Salva o ICE (1–10); `SUBMETIDA` → `EM_ANALISE` |
| `POST` | `/api/v1/ideas/{id}/reject` | **GESTOR** | Rejeita (comentário obrigatório) |
| `POST` | `/api/v1/ideas/{id}/approve` | **GESTOR** | Aprova: cria o projeto rascunho, +50 ao autor. Idempotente; o autor não aprova a própria ideia |
| `GET` | `/api/v1/users` | GESTOR, LIDER | Usuários (`id`, `name`, `role`, `division`); filtro `role` |
| `GET` | `/api/v1/users/ranking` | autenticado | Top do mês (operadores); `limit` 1–50 (padrão 5) |
| `GET` | `/health/live` · `/health/ready` · `/health` | público | Saúde (processo · MongoDB · tudo) |
| `GET` | `/swagger` | público* | Documentação interativa (*só em Development ou com `Swagger__Enabled=true`) |

O contrato completo (todas as rotas planejadas, payloads e perfis) está no [`spec.md`](../.specs/features/sprint2/spec.md#contrato-de-api-apiv1).

---

## 2. Pré-requisitos

| Ferramenta | Versão | Para quê | macOS | Windows |
|---|---|---|---|---|
| **Docker Desktop** | 4.x (Compose v2) | MongoDB em replica set; testes de integração; rodar a API em contêiner | [docker.com](https://www.docker.com/products/docker-desktop/) ou `brew install --cask docker` | [docker.com](https://www.docker.com/products/docker-desktop/) (backend **WSL 2** recomendado) ou `winget install Docker.DockerDesktop` |
| **.NET SDK** | **8.0.x** | Compilar/rodar/testar (não é necessário se você só usar o Docker) | [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) ou `brew install --cask dotnet-sdk@8` | [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) ou `winget install Microsoft.DotNet.SDK.8` |
| **Git** | qualquer | Clonar o repositório | Xcode CLT / `brew install git` | [git-scm.com](https://git-scm.com/download/win) ou `winget install Git.Git` |

> O `global.json` fixa o SDK em **8.0.x**. Ter apenas o SDK 9 instalado **não** basta (veja a [solução de problemas](#10-solução-de-problemas)). SDKs de várias versões podem conviver na mesma máquina.

Conferindo a instalação:

```bash
dotnet --list-sdks     # deve listar 8.0.x
docker --version
docker compose version
```

> **Windows:** abra o **Docker Desktop** e espere o ícone indicar "Engine running" antes de qualquer comando `docker`. Os comandos abaixo para Windows usam **PowerShell**.
> **macOS:** vale para Apple Silicon (M1/M2/M3…) e Intel — a imagem `mongo:7` tem build multi-arquitetura.

---

## 3. Início rápido

Todos os comandos são executados dentro da pasta `backend/`:

```bash
cd backend
```

Existem três formas de rodar. **A é a mais simples** (só precisa do Docker); **B** é a de desenvolvimento no dia a dia; **C** usa um MongoDB na nuvem.

### 3.0 Gerar uma chave JWT (vale para A e B)

A API **recusa subir** sem uma chave JWT de pelo menos 32 bytes. Gere uma aleatória:

**macOS / Linux**

```bash
openssl rand -base64 48
```

**Windows (PowerShell)**

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
```

Guarde o valor: ele será usado como `JWT_KEY` (opção A) ou `Jwt:Key` (opção B). **Nunca versione essa chave.**

### 3.A Tudo no Docker (Mongo + API) — sem instalar o .NET

1. Crie o arquivo `.env` a partir do modelo e preencha `JWT_KEY` com a chave gerada acima:

   **macOS / Linux**

   ```bash
   cp .env.example .env
   ```

   **Windows (PowerShell)**

   ```powershell
   Copy-Item .env.example .env
   ```

   Edite o `.env` (salve como **UTF-8**; o VS Code e o Bloco de Notas atual fazem isso) — o mínimo é:

   ```dotenv
   JWT_KEY=cole-aqui-a-chave-gerada
   ```

2. Suba tudo (a primeira vez baixa as imagens e compila a API, o que leva alguns minutos):

   ```bash
   docker compose --profile api up --build -d
   ```

3. Confira:

   ```bash
   docker compose ps
   ```

   - API: <http://localhost:5080> · Swagger: <http://localhost:5080/swagger> · Saúde: <http://localhost:5080/health/ready>
   - Na primeira subida a API cria os **índices** e popula os **dados de demonstração** (seed).

4. Para parar (mantendo os dados) ou apagar tudo (inclusive o banco):

   ```bash
   docker compose --profile api down
   ```

   ```bash
   docker compose --profile api down -v
   ```

> Dica: acompanhar os logs da API — `docker compose logs -f api`.

### 3.B API local (`dotnet run`) + MongoDB no Docker — desenvolvimento

1. Suba **apenas o MongoDB** (replica set de 1 nó, necessário para transações). Aguarde ficar `healthy`:

   ```bash
   docker compose up -d mongo
   ```

   ```bash
   docker compose ps
   ```

2. Guarde a chave JWT nos *user secrets* do projeto (ficam fora do repositório; `<CHAVE>` é o valor gerado em 3.0):

   ```bash
   dotnet user-secrets set "Jwt:Key" "<CHAVE>" --project src/AguiaBranca.Api
   ```

   *Alternativa por variável de ambiente (vale só para o terminal atual):*

   | macOS / Linux | Windows (PowerShell) |
   |---|---|
   | `export Jwt__Key="<CHAVE>"` | `$env:Jwt__Key = "<CHAVE>"` |

3. Rode a API (perfil `Development`: seed ligado, Swagger ligado, Mongo em `localhost:27017`):

   ```bash
   dotnet run --project src/AguiaBranca.Api
   ```

   API em <http://localhost:5080> · Swagger em <http://localhost:5080/swagger>.

4. (Opcional) recarregar automaticamente ao editar o código:

   ```bash
   dotnet watch --project src/AguiaBranca.Api
   ```

> A primeira restauração de pacotes NuGet precisa de internet. Se algum passo reclamar de SDK, veja a [solução de problemas](#10-solução-de-problemas).

### 3.C MongoDB Atlas (sem Docker para o banco)

O plano gratuito **M0** do [MongoDB Atlas](https://www.mongodb.com/atlas) é um replica set e suporta transações. Crie o cluster, um usuário de banco, libere o seu IP e copie a *connection string* `mongodb+srv://…`. Depois:

| macOS / Linux | Windows (PowerShell) |
|---|---|
| `export ConnectionStrings__Mongo="mongodb+srv://USUARIO:SENHA@cluster.mongodb.net/?retryWrites=true&w=majority"` | `$env:ConnectionStrings__Mongo = "mongodb+srv://USUARIO:SENHA@cluster.mongodb.net/?retryWrites=true&w=majority"` |

E siga o passo 3.B (2 e 3) — sem o `docker compose up -d mongo`. Em produção, defina também `Seed__Enabled=false` (o seed cria usuários com senha pública).

---

## 4. Configuração

A configuração vem de `appsettings*.json`, *user secrets*, variáveis de ambiente e (no Docker) do arquivo `.env`. Variáveis de ambiente usam **`__`** (dois underscores) no lugar de `:` — ex.: `Jwt:Key` → `Jwt__Key`.

| Chave | Variável | Padrão | Descrição |
|---|---|---|---|
| `Jwt:Key` | `Jwt__Key` (`JWT_KEY` no `.env`) | — **obrigatória** | Segredo do JWT (≥ 32 bytes). A API não sobe sem ela |
| `Jwt:Issuer` / `Jwt:Audience` | `Jwt__Issuer` / `Jwt__Audience` | `aguiabranca-api` / `aguiabranca-app` | Validados em todo token |
| `Jwt:AccessMinutes` / `Jwt:RefreshDays` | idem | `30` / `7` | Validade dos tokens |
| `ConnectionStrings:Mongo` | `ConnectionStrings__Mongo` | `mongodb://localhost:27017/?directConnection=true` (Development) | Conexão MongoDB |
| `Mongo:Database` | `Mongo__Database` | `aguiabranca` | Nome do banco |
| `Mongo:RequireReplicaSet` | `Mongo__RequireReplicaSet` | `true` | Falha o startup sem replica set (transações) |
| `Seed:Enabled` | `Seed__Enabled` (`SEED_ENABLED`) | `true` em Development e no compose | Popula os dados de demonstração |
| `Swagger:Enabled` | `Swagger__Enabled` (`SWAGGER_ENABLED`) | Development: ligado · Production: desligado | Expõe `/swagger` |
| `RateLimiting:AuthPermitLimit` | `RateLimiting__AuthPermitLimit` | `10` | Requisições por minuto, por IP, em `/api/v1/auth/*` |
| `Cors:Origins` | `Cors__Origins__0` … | vazio | Origens permitidas (o app nativo não usa CORS) |
| `Reports:TimeZone` | `Reports__TimeZone` | `America/Sao_Paulo` | Fuso do "mês corrente" (ranking, badges) |
| `Gemini:ApiKey` / `Gemini:Model` | `Gemini__ApiKey` / `Gemini__Model` | vazio | Insights de IA (fase futura). Chave gratuita em [Google AI Studio](https://aistudio.google.com/) |

> **Segredos nunca entram no Git.** `.env`, *user secrets* e variáveis de ambiente são os lugares certos; `appsettings*.json` não contém valores reais.

---

## 5. Usuários de demonstração

Criados pelo seed (senha de todos: `aguiabranca123`):

| E-mail | Perfil | Divisão |
|---|---|---|
| `lider@aguiabranca.com` | `LIDER` | CORPORATIVO |
| `gestor@aguiabranca.com` | `GESTOR` | LOGISTICA |
| `operador@aguiabranca.com` | `OPERADOR` | LOGISTICA |
| `ana@aguiabranca.com` | `OPERADOR` | PASSAGEIROS |
| `bruno@aguiabranca.com` | `OPERADOR` | COMERCIO |

O seed também cria 4 orientações estratégicas (com histórico), 6 ideias em todos os status e 3 projetos com histórico — o `operador@` chega com 295 pontos e 3 badges. É **idempotente** (rodar de novo não duplica). Para recomeçar do zero: `docker compose down -v` e suba de novo.

---

## 6. Experimentando a API

### Pelo Swagger (mais fácil)

1. Abra <http://localhost:5080/swagger>.
2. Em `POST /api/v1/auth/login`, clique em **Try it out** e envie `{ "email": "lider@aguiabranca.com", "password": "aguiabranca123" }`.
3. Copie o `accessToken` da resposta, clique em **Authorize** (cadeado) e cole **apenas o token** (sem a palavra `Bearer`).
4. Agora as rotas protegidas funcionam — ex.: `GET /api/v1/auth/me`.

### Pela linha de comando

**macOS / Linux** (`curl` + `jq`)

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"lider@aguiabranca.com","password":"aguiabranca123"}' | jq -r .accessToken)

curl -s http://localhost:5080/api/v1/auth/me -H "Authorization: Bearer $TOKEN"
```

**Windows (PowerShell)**

```powershell
$login = Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/v1/auth/login `
  -ContentType "application/json" `
  -Body '{"email":"lider@aguiabranca.com","password":"aguiabranca123"}'

Invoke-RestMethod -Uri http://localhost:5080/api/v1/auth/me -Headers @{ Authorization = "Bearer $($login.accessToken)" }
```

### Formato dos erros

Todos os erros seguem `application/problem+json`, com um `code` estável e o `traceId` (o mesmo valor do header `X-Correlation-ID`, útil para localizar o log):

```json
{
  "type": "urn:aguiabranca:error:invalid-credentials",
  "title": "Não autenticado",
  "status": 401,
  "detail": "E-mail ou senha inválidos.",
  "instance": "/api/v1/auth/login",
  "code": "INVALID_CREDENTIALS",
  "errors": [{ "code": "INVALID_CREDENTIALS", "message": "E-mail ou senha inválidos.", "field": null }],
  "traceId": "3bd1ba8876394c548be1316bfa623c68"
}
```

### Comportamentos de segurança para saber

- **5 senhas erradas seguidas** bloqueiam a conta por 15 minutos (`429` + `Retry-After`). Para liberar durante o desenvolvimento:

  ```bash
  docker exec aguiabranca-mongo mongosh aguiabranca --quiet --eval 'db.users.updateMany({}, {$set: {accessFailedCount: 0, lockoutEnd: null}})'
  ```

  (Aspas simples: o comando funciona igual no macOS e no PowerShell.)

- **Mais de 10 chamadas por minuto** a `/api/v1/auth/*` pelo mesmo IP retornam `429` (`RATE_LIMITED`). Para testes intensivos, suba com `RateLimiting__AuthPermitLimit=1000`.
- **Rotas inexistentes respondem `401` a quem não está autenticado** (e `404` a quem está) — de propósito: anônimos não descobrem quais rotas existem.

---

## 7. Testes

A suíte usa **xUnit**. Os testes de integração sobem um **MongoDB real em replica set** via Testcontainers, então precisam do **Docker em execução**.

Tudo (unitários + integração):

```bash
dotnet test
```

Só os unitários (rápidos, sem Docker):

```bash
dotnet test --filter "Category!=Integration"
```

Só os de integração:

```bash
dotnet test --filter "Category=Integration"
```

### Ciclo de desenvolvimento mais rápido

Em vez de subir um contêiner novo a cada execução, aponte os testes para o MongoDB do compose (cada teste usa um banco próprio, removido ao final):

| macOS / Linux | Windows (PowerShell) |
|---|---|
| `docker compose up -d mongo` | `docker compose up -d mongo` |
| `export AGUIA_TEST_MONGO="mongodb://localhost:27017/?directConnection=true"` | `$env:AGUIA_TEST_MONGO = "mongodb://localhost:27017/?directConnection=true"` |
| `dotnet test` | `dotnet test` |

O que a suíte cobre: regras de domínio (máquina de estados da ideia, pontos, badges, diff de projeto), casos de uso de autenticação, persistência e transações no Mongo real, matriz de autorização (401/403/2xx por perfil), rate limit, formato de erros, health checks, Swagger e seed.

---

## 8. Arquitetura e estrutura de pastas

**Clean Architecture** em quatro camadas, com código de negócio organizado por funcionalidade (*feature*):

```text
              ┌──────────────────────┐
              │  Api                 │  Controllers · Middleware · Auth · Swagger
              └──────────┬───────────┘
                         ▼
              ┌──────────────────────┐
              │  Application         │  Handlers (casos de uso) · Validators · Result · Interfaces
              └──────────┬───────────┘
                         ▼
              ┌──────────────────────┐
              │  Domain              │  Entidades · Regras · Enums (sem dependências externas)
              └──────────────────────┘
                         ▲
              ┌──────────┴───────────┐
              │  Infrastructure      │  EF Core + MongoDB · Identity · JWT · Seed (implementa as interfaces)
              └──────────────────────┘
```

Regra de dependência: `Api → Application → Domain` e `Infrastructure → Application/Domain`. O **Domain não conhece** ASP.NET, EF Core, MongoDB nem HTTP — isso é verificado por testes de arquitetura.

```text
backend/
├── AguiaBranca.sln
├── docker-compose.yml        # MongoDB (replica set) + API (profile "api")
├── Dockerfile                # imagem da API (multi-stage, usuário não-root)
├── .env.example              # modelo do .env do compose
├── global.json               # fixa o SDK 8.0.x
├── Directory.Build.props / Directory.Packages.props   # versões centralizadas
├── src/
│   ├── AguiaBranca.Api/              # Controllers, Middleware, Extensions, Program.cs
│   ├── AguiaBranca.Application/      # Features/<Funcionalidade>/..., Common/ (Result, abstrações)
│   ├── AguiaBranca.Domain/           # Entities, ValueObjects, Enums, Rules
│   └── AguiaBranca.Infrastructure/   # Persistence, Identity, Authentication, Seed, Configuration
├── tests/
│   ├── AguiaBranca.Domain.Tests/
│   ├── AguiaBranca.Application.Tests/
│   ├── AguiaBranca.Infrastructure.Tests/   # integração com MongoDB real
│   └── AguiaBranca.Api.Tests/              # autorização, fluxos, contratos HTTP
└── spikes/                       # prova de conceito do provider EF Core MongoDB (descartável)
```

**Banco de dados:** MongoDB com as coleções `users`, `refreshTokens`, `guidelines`, `guidelineHistory`, `ideas`, `projects`, `projectUpdates`, `pointEvents` e `aiInsights`. O provider EF Core do MongoDB não gerencia migrations: os **índices** (únicos, parciais e TTL) são criados automaticamente no startup. O modelo completo está no [`spec.md`](../.specs/features/sprint2/spec.md#modelo-de-dados-mongodb).

**Segurança:** senhas com PBKDF2 (ASP.NET Identity) · JWT HS256 de 30 min com validação de emissor, audiência e expiração · refresh token opaco de uso único (só o hash é guardado; reuso de um token já trocado revoga a sessão inteira) · bloqueio de conta e rate limit contra força bruta · erros sem stack trace · segredos fora do código.

---

## 9. Conectando o app Android

O endereço da API muda conforme onde o app roda:

| Onde o app roda | URL base da API |
|---|---|
| **Emulador Android** (Android Studio) | `http://10.0.2.2:5080/api/v1/` (`10.0.2.2` é o "localhost" da máquina hospedeira) |
| **Celular físico** na mesma rede Wi-Fi | `http://<IP-DA-SUA-MÁQUINA>:5080/api/v1/` |

Descobrindo o IP da máquina e liberando a porta:

| macOS | Windows (PowerShell) |
|---|---|
| `ipconfig getifaddr en0` (Wi-Fi) | `(Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias "Wi-Fi").IPAddress` |
| Permita conexões de entrada se o firewall perguntar | Crie a regra (PowerShell **como administrador**): `New-NetFirewallRule -DisplayName "INOVAGAB API 5080" -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow` |

> Com `dotnet run` a API escuta apenas em `localhost`. Para aceitá-la vindo de outro dispositivo, use `dotnet run --project src/AguiaBranca.Api --urls "http://0.0.0.0:5080"` (o Docker já publica a porta para a rede). Na integração do app, o tráfego HTTP simples será permitido **apenas em builds de debug**; a versão de entrega deve usar HTTPS.

---

## 10. Solução de problemas

| Sintoma | Causa provável e solução |
|---|---|
| `Cannot connect to the Docker daemon` / `error during connect` | O Docker Desktop não está aberto. Abra-o e espere "Engine running" |
| `A compatible .NET SDK was not found` / `Requested SDK version: 8.0.100` | O SDK 8 não está instalado (só o 9, por exemplo). Instale o **.NET SDK 8.0** (veja [pré-requisitos](#2-pré-requisitos)) e confira com `dotnet --list-sdks` |
| A API encerra logo ao subir com `Jwt:Key é obrigatório` ou `Jwt:Key deve ter ao menos 32 bytes` | Falta a chave JWT (ou é curta). Gere uma ([3.0](#30-gerar-uma-chave-jwt-vale-para-a-e-b)) e defina em `.env`/*user secrets*/variável de ambiente |
| `O MongoDB não está em modo replica set` | Você está usando um MongoDB *standalone*. Use o `docker compose up -d mongo` (já é replica set) ou o Atlas. Só desligue com `Mongo__RequireReplicaSet=false` em ambientes descartáveis — as transações não funcionam sem replica set |
| `Timed out … server selection` / `/health/ready` retorna `503` | O MongoDB não está de pé. Rode `docker compose ps`; se não estiver `healthy`, `docker compose logs mongo` |
| `port is already allocated` / `address already in use` (5080 ou 27017) | Outro processo usa a porta. **macOS:** `lsof -i :5080` · **Windows:** `netstat -ano \| findstr :5080` (depois `taskkill /PID <pid> /F`). Se for um Mongo local, pare-o ou mude o mapeamento de porta no compose |
| `429` ao testar login repetidamente | Rate limit (10/min por IP) ou bloqueio de conta (5 erros). Veja [comportamentos de segurança](#comportamentos-de-segurança-para-saber) |
| `401` numa rota que "deveria existir" | Falta o header `Authorization: Bearer <token>` (rotas inexistentes também respondem 401 a anônimos) |
| No Windows, o `.env` não é lido ou aparecem caracteres estranhos na chave | Salve o arquivo como **UTF-8** (sem BOM) e sem aspas ao redor do valor. Rode os comandos `docker compose` **dentro de `backend/`** |
| `dotnet test` demora muito na 1ª vez / falha ao baixar `mongo:7` | A primeira execução baixa a imagem do MongoDB (Testcontainers). Confira a internet ou use `AGUIA_TEST_MONGO` ([ciclo rápido](#ciclo-de-desenvolvimento-mais-rápido)) |
| Quero recomeçar com o banco limpo | `docker compose down -v` (apaga o volume) e suba de novo — o seed recria os dados |

---

## Licença e contexto

Projeto acadêmico do **Challenge FIAP 2026** (Grupo Águia Branca — Sprint 2). Consulte o [`spec.md`](../.specs/features/sprint2/spec.md) para os requisitos e o escopo da entrega.
