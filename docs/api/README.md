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
10. [Migração dos dados do Firebase](#10-migração-dos-dados-do-firebase)
11. [Deploy de demonstração](#11-deploy-de-demonstração)
12. [Solução de problemas](#12-solução-de-problemas)

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
| Projetos: CRUD do gestor, histórico com diff e **conclusão → ideia implementada (+200 pontos)** | ✅ |
| Relatórios/dashboard: funil, KPIs, sparkline, retorno por orientação e por projeto (portados do app, com testes de paridade) | ✅ |
| **Insights de IA (Google Gemini)** sobre o dashboard, com cache, cota diária e resiliência | ✅ |
| Endurecimento de segurança: CORS restrito, limite de payload, headers de segurança, HSTS, varredura de segredos | ✅ |
| Matriz de autorização completa (todas as rotas × 4 identidades) e fluxo ponta a ponta com MongoDB real | ✅ |
| Ferramenta de migração Firebase → MongoDB ([`tools/`](tools/README.md)) | ✅ (leitura do Firestore real ainda não validada — ver o README da ferramenta) |
| Deploy de demonstração (API + MongoDB Atlas) | ⏳ depende de contas externas — passo a passo na [seção 11](#11-deploy-de-demonstração) |

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
| `GET` | `/api/v1/projects` | GESTOR, LIDER | Lista paginada; filtros `stage`, `division`, `guidelineId` |
| `GET` | `/api/v1/projects/{id}` | GESTOR, LIDER | Detalhe (com `netProfit` e `roiPercent`) |
| `GET` | `/api/v1/projects/{id}/updates` | GESTOR, LIDER | Histórico (timeline) com o diff dos campos |
| `POST` | `/api/v1/projects` | **GESTOR** | Cadastro direto de projeto |
| `PUT` | `/api/v1/projects/{id}` | **GESTOR** | Substituição completa; grava histórico com diff. `stage=CONCLUIDO` conclui a ideia de origem (+200 ao autor, uma única vez). `version` opcional para concorrência |
| `DELETE` | `/api/v1/projects/{id}` | **GESTOR** | Exclui o projeto e o histórico (a ideia de origem permanece) |
| `GET` | `/api/v1/users` | GESTOR, LIDER | Usuários (`id`, `name`, `role`, `division`); filtro `role` |
| `GET` | `/api/v1/users/ranking` | autenticado | Top do mês (operadores); `limit` 1–50 (padrão 5) |
| `GET` | `/api/v1/reports/summary` | **LIDER** | Dashboard: `funnel`, `kpis`, `sparkline` (6 meses), `guidelineImpacts`, `projects` por ROI. Filtros `period` (`THIS_MONTH` \| `LAST_QUARTER` \| `THIS_YEAR` \| `ALL`) e `division` |
| `GET` | `/api/v1/reports/guidelines` | **LIDER** | Retorno por estratégia: impacto de cada orientação (mesmos filtros) |
| `GET` | `/api/v1/reports/guidelines/{id}` | **LIDER** | Ideias (por status), projetos, investimento, retorno, lucro e ROI da orientação |
| `GET` | `/api/v1/reports/projects/{id}` | **LIDER** | Investimento, retorno, lucro, ROI, produtividade, redução de custo, `targetDate`, `daysToDeadline`, `overdue` |
| `POST` | `/api/v1/reports/insights` | **LIDER** | **Insights de IA**: `{ period?, division?, guidelineId?, refresh? }` → `summary`, `highlights[]`, `risks[]`, `recommendations[]`, `generatedAt`, `model`, `fromCache`. Cache de 6 h; 6 req/min por usuário; teto diário. `503 AI_UNAVAILABLE` · `502 AI_INVALID_RESPONSE` · `429 RATE_LIMITED` |
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

> O `global.json` fixa o SDK em **8.0.x**. Ter apenas o SDK 9 instalado **não** basta (veja a [solução de problemas](#12-solução-de-problemas)). SDKs de várias versões podem conviver na mesma máquina.

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

> A primeira restauração de pacotes NuGet precisa de internet. Se algum passo reclamar de SDK, veja a [solução de problemas](#12-solução-de-problemas).

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
| `Cors:Origins` | `Cors__Origins__0` … | vazio | Origens de navegador permitidas (exatas, ex.: `https://painel.exemplo.com`; sem curinga, barra final ou caminho — inválidas derrubam o startup). O app nativo não usa CORS |
| `Security:MaxRequestBodyBytes` | `Security__MaxRequestBodyBytes` | `1048576` (1 MB) | Corpo maior → `413 PAYLOAD_TOO_LARGE` |
| `Security:ForwardedHeaders` | `Security__ForwardedHeaders` | `false` | **Ligue só atrás de proxy/balanceador confiável** que termina o HTTPS (Render, Azure, Nginx): passa a valer `X-Forwarded-For/Proto` (IP real no rate limit, HSTS). Sem proxy, deixe desligado — senão um cliente forja o IP |
| `Security:HstsMaxAgeDays` | `Security__HstsMaxAgeDays` | `365` | HSTS (só fora de Development e só em requisições HTTPS) |
| `Reports:TimeZone` | `Reports__TimeZone` | `America/Sao_Paulo` | Fuso do "mês corrente" (ranking, badges) |
| `Gemini:ApiKey` | `Gemini__ApiKey` (`GEMINI_API_KEY` no `.env`) | vazio | Chave do [Google AI Studio](https://aistudio.google.com/). **Sem chave a API funciona normalmente** e `/reports/insights` responde `503 AI_UNAVAILABLE` |
| `Gemini:Model` | `Gemini__Model` (`GEMINI_MODEL`) | vazio (**obrigatório com chave**) | Modelo do free tier da sua conta. Este projeto usa **`gemini-3.1-flash-lite`** (menor custo em tokens). Os nomes mudam com frequência: liste os disponíveis em [ai.google.dev/gemini-api/docs/models](https://ai.google.dev/gemini-api/docs/models) |
| `Gemini:DailyLimit` | `Gemini__DailyLimit` (`GEMINI_DAILY_LIMIT`) | `100` | Teto diário de gerações (todos os líderes juntos, dia no fuso `Reports:TimeZone`). Ajuste à cota da sua conta |
| `Gemini:CacheHours` | `Gemini__CacheHours` | `6` | Validade do cache dos insights (`0` desliga) |
| `Gemini:TimeoutSeconds` | `Gemini__TimeoutSeconds` | `20` | Timeout **por tentativa** (há 1 nova tentativa em 429/5xx/timeout) |
| `Gemini:MaxOutputTokens` | `Gemini__MaxOutputTokens` | `2048` | Teto de tokens da resposta |
| `Gemini:ThinkingLevel` | `Gemini__ThinkingLevel` (`GEMINI_THINKING_LEVEL`) | vazio | Opcional (modelos Gemini 3): `minimal` \| `low` \| `medium` \| `high` |
| `RateLimiting:InsightsPermitLimit` | `RateLimiting__InsightsPermitLimit` | `6` | Gerações por minuto, **por usuário** |

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

### Insights de IA (Gemini)

1. Crie uma chave no [Google AI Studio](https://aistudio.google.com/) e coloque no `backend/.env` (`GEMINI_API_KEY` e `GEMINI_MODEL=gemini-3.1-flash-lite`). O `.env` é ignorado pelo Git.
2. **Docker compose:** as variáveis do `.env` já são repassadas à API. **`dotnet run`:** exporte `Gemini__ApiKey` e `Gemini__Model` no terminal (o `.env` só vale para o compose).
3. Chame como líder:

```bash
curl -s -X POST http://localhost:5080/api/v1/reports/insights \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"period":"ALL"}'
```

- Só **dados agregados e títulos truncados (80 caracteres)** vão ao modelo — nunca nomes, e-mails ou ids de usuário; textos livres entram como dado dentro de um bloco delimitado (mitigação de *prompt injection*). Orientações são citadas por referência curta (`G1`…), não por id.
- A 2ª chamada com os mesmos filtros e dados vem do **cache** (`fromCache: true`, sem custo); alterar um projeto invalida o cache. `"refresh": true` força nova geração.
- **Cota:** o plano gratuito é pequeno e muda com o tempo — confira os limites da sua conta. `Gemini:DailyLimit` protege a cota (resposta `429`); cache e limite por usuário reduzem o consumo. Cada geração usa ~1,5 mil tokens.
- A chave só trafega no header `x-goog-api-key` e **nunca é registrada em log** (o log guarda apenas modelo, status, latência e contagem de tokens).
- **Se o Google recusar** (chave inválida, cota ou créditos esgotados — ex.: `402`/`429`), a API responde `503 AI_UNAVAILABLE`; o motivo aparece no log do servidor (`RESOURCE_EXHAUSTED`, `PERMISSION_DENIED`…).

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
- **Headers em toda resposta:** `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY` e CSP restritiva (exceto no Swagger); `/auth/*` também `Cache-Control: no-store`. HSTS em produção sobre HTTPS. O header `Server` é removido.
- **CORS:** só as origens de `Cors:Origins`; qualquer outra não recebe `Access-Control-Allow-Origin`. **Corpo > 1 MB** → `413`. **Ids inválidos** na rota → `404` (nunca `500`); erros nunca trazem stack trace.
- **Segredos:** `backend/scripts/scan-secrets.sh` varre os arquivos versionados atrás de chaves (Google `AIza…`, PEM, connection strings com senha, `Jwt`/`Gemini` preenchidos) e confere que o `.env` está fora do Git. Rode antes de cada commit/PR.

---

## 7. Testes

A suíte usa **xUnit**. Os testes de integração sobem um **MongoDB real em replica set** via Testcontainers, então precisam do **Docker em execução**.

Tudo (unitários + integração):

```bash
dotnet test
```

Os testes de IA **não chamam o Gemini** (usam um `HttpMessageHandler`/gerador falsos); a única chamada real é o *smoke* manual descrito acima.

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

Cobertura de linhas (Domain e Application), com o *coverlet*:

```bash
dotnet test tests/AguiaBranca.Domain.Tests --collect:"XPlat Code Coverage"
dotnet test tests/AguiaBranca.Application.Tests --collect:"XPlat Code Coverage"
```

Só com os testes unitários: **Domain ≈ 95 %** e **Application ≈ 95 %** (meta: ≥ 80 %). Os testes da migração ficam em `tools/` e entram no `dotnet test` da solução.

### Smoke test de uma API em execução

Com a API no ar (compose ou deploy), o script confere saúde, login demo, dashboard, 401/403 e headers (e, com `SMOKE_INSIGHTS=1`, também **uma** geração de IA — consome 1 da cota diária):

```bash
backend/scripts/smoke.sh                            # http://localhost:5080
SMOKE_INSIGHTS=1 backend/scripts/smoke.sh https://minha-api.onrender.com
```

O que a suíte cobre: regras de domínio (máquina de estados da ideia, pontos, badges, diff de projeto, prazos), casos de uso de todas as funcionalidades, persistência e transações no Mongo real, **matriz de autorização de todas as rotas × 4 identidades** (um teste falha se surgir rota sem entrada), **fluxo ponta a ponta** (orientação → ideia → ICE → aprovação → projeto → conclusão → dashboard → ranking → histórico), concorrência (aprovações e conclusões simultâneas, teto de IA), dashboard com casos *golden* calculados à mão, cliente Gemini com respostas simuladas, hardening (CORS, 413, headers, HSTS), migração Firestore → Mongo, rate limit, formato de erros, health checks, Swagger e seed.

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
├── tools/
│   ├── AguiaBranca.FirestoreMigrator/         # migração Firestore → MongoDB (CLI) — ver tools/README.md
│   └── AguiaBranca.FirestoreMigrator.Tests/
├── scripts/                      # scan-secrets.sh (varredura de segredos) · smoke.sh (smoke de API em execução)
└── spikes/                       # prova de conceito do provider EF Core MongoDB (descartável)
```

**Banco de dados:** MongoDB com as coleções `users`, `refreshTokens`, `guidelines`, `guidelineHistory`, `ideas`, `projects`, `projectUpdates`, `pointEvents`, `aiInsights` (cache dos insights) e `aiUsage` (contador diário de gerações). O provider EF Core do MongoDB não gerencia migrations: os **índices** (únicos, parciais e TTL) são criados automaticamente no startup. O modelo completo está no [`spec.md`](../.specs/features/sprint2/spec.md#modelo-de-dados-mongodb).

**Segurança:** senhas com PBKDF2 (ASP.NET Identity) · JWT HS256 de 30 min com validação de emissor, audiência e expiração · refresh token opaco de uso único (só o hash é guardado; reuso de um token já trocado revoga a sessão inteira) · bloqueio de conta e rate limit contra força bruta (por IP no login; por usuário nos insights de IA) · erros sem stack trace · segredos fora do código.

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

## 10. Migração dos dados do Firebase

A migração do app da Sprint 1 (Firestore) para o MongoDB é feita pela ferramenta em [`tools/`](tools/README.md): `--dry-run`, execução
idempotente, remapeamento de todas as referências, recálculo de badges, pontos como evento de abertura e **relatório de
conciliação**. Leia o [README da ferramenta](tools/README.md) (inclui como obter a service account e as limitações — por exemplo,
as senhas do Firebase **não migram**: todos recebem uma senha temporária).

---

## 11. Deploy de demonstração

O APK precisa de uma URL **HTTPS** pública. Caminho sugerido (gratuito): **API no Render** (ou Azure/Railway) + **MongoDB Atlas M0**
(o M0 já é replica set, então as transações funcionam). Não é possível fazê-lo por script: exige contas e chaves suas.

1. **Atlas:** crie um cluster M0, um usuário de banco e libere o IP do host (ou `0.0.0.0/0` só para a demonstração). Copie a connection string `mongodb+srv://…`.
2. **Host da API:** crie um *Web Service* a partir deste repositório usando o `backend/Dockerfile` (contexto `backend/`), porta `8080`, *health check* em `/health/ready`.
3. **Variáveis de ambiente do serviço** (segredos só no painel do host, nunca no Git):

| Variável | Valor |
|---|---|
| `ConnectionStrings__Mongo` | a connection string do Atlas |
| `Jwt__Key` | chave nova (`openssl rand -base64 48`) |
| `Security__ForwardedHeaders` | `true` (o host termina o HTTPS) |
| `Seed__Enabled` | `true` para a demonstração (cria os usuários demo) |
| `Swagger__Enabled` | `true` se quiser o Swagger na demonstração |
| `Gemini__ApiKey` / `Gemini__Model` / `Gemini__DailyLimit` | chave do AI Studio, `gemini-3.1-flash-lite` e o teto da sua cota |
| `Cors__Origins__0` | só se algum front web for consumir a API |

4. **Smoke pós-deploy:** `SMOKE_INSIGHTS=1 backend/scripts/smoke.sh https://sua-api.exemplo.com`.
5. Anote a URL aqui e no `mobile/` (build *release*, tarefa M11 do plano). Planos gratuitos "dormem": a primeira chamada pode levar ~1 min.

> Sem deploy, o fallback é `docker compose up` numa máquina da mesma rede e apontar o app para o IP da LAN (ver seção 9).

---

## 12. Solução de problemas

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
