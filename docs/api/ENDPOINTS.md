# INOVAGAB — Águia Branca API — Especificação de endpoints

> Gerado automaticamente por `docs/api/generate_endpoints.py` a partir de `docs/api/openapi.json` (exportado de `/swagger/v1/swagger.json` da API rodando). **Não edite este arquivo a mão** — rode o script de novo depois de atualizar o `openapi.json`. Perfis e erros de negócio vêm de uma tabela mantida no próprio script, sincronizada com a matriz de autorização testada no backend (`AuthorizationMatrixTests.cs`, B22).

Base URL: `/api/v1` · Autenticação: header `Authorization: Bearer <accessToken>` (schema `Bearer`, JWT).

Além dos erros específicos listados por rota, toda a API pode responder (formato comum, ver rodapé): `400 VALIDATION_ERROR` (corpo/query inválidos), `401 TOKEN_INVALID` (sem token ou expirado — rotas autenticadas), `403 FORBIDDEN` (perfil sem permissão), `404 RESOURCE_NOT_FOUND` (id inexistente ou malformado), `429 RATE_LIMITED` (limite de requisições) e `500 INTERNAL_ERROR`.

## Sumário

| Método | Rota | Perfis | Sucesso |
|---|---|---|---|
| POST | `/api/v1/auth/login` | público | 200 |
| POST | `/api/v1/auth/refresh` | público | 200 |
| POST | `/api/v1/auth/logout` | autenticado | 204 |
| GET | `/api/v1/auth/me` | autenticado | 200 |
| GET | `/api/v1/guidelines` | autenticado | 200 |
| GET | `/api/v1/guidelines/history` | autenticado | 200 |
| GET | `/api/v1/guidelines/{id}` | autenticado | 200 |
| POST | `/api/v1/guidelines` | LIDER | 201 |
| PUT | `/api/v1/guidelines/{id}` | LIDER | 200 |
| DELETE | `/api/v1/guidelines/{id}` | LIDER | 204 |
| GET | `/api/v1/ideas` | autenticado (operador só vê as próprias) | 200 |
| GET | `/api/v1/ideas/{id}` | autenticado (operador só vê as próprias) | 200 |
| POST | `/api/v1/ideas` | OPERADOR, GESTOR | 201 |
| PUT | `/api/v1/ideas/{id}` | autor da ideia (SUBMETIDA) | 200 |
| DELETE | `/api/v1/ideas/{id}` | autor da ideia (SUBMETIDA) | 204 |
| PUT | `/api/v1/ideas/{id}/ice` | GESTOR | 200 |
| POST | `/api/v1/ideas/{id}/approve` | GESTOR (nunca o próprio autor) | 200 |
| POST | `/api/v1/ideas/{id}/reject` | GESTOR | 200 |
| GET | `/api/v1/projects` | GESTOR, LIDER | 200 |
| GET | `/api/v1/projects/{id}` | GESTOR, LIDER | 200 |
| GET | `/api/v1/projects/{id}/updates` | GESTOR, LIDER | 200 |
| POST | `/api/v1/projects` | GESTOR | 201 |
| PUT | `/api/v1/projects/{id}` | GESTOR | 200 |
| DELETE | `/api/v1/projects/{id}` | GESTOR | 204 |
| GET | `/api/v1/reports/summary` | LIDER | 200 |
| GET | `/api/v1/reports/guidelines` | LIDER | 200 |
| GET | `/api/v1/reports/guidelines/{id}` | LIDER | 200 |
| GET | `/api/v1/reports/projects/{id}` | LIDER | 200 |
| POST | `/api/v1/reports/insights` | LIDER | 200 |
| GET | `/api/v1/users` | GESTOR, LIDER | 200 |
| GET | `/api/v1/users/ranking` | autenticado | 200 |

---

## POST `/api/v1/auth/login`

**Perfis:** público

E-mail + senha → `accessToken` (30 min), `refreshToken` (7 dias) e o perfil.

**Requisição:**
```json
{
  "email": "lider@aguiabranca.com",
  "password": "aguiabranca123"
}
```

**Resposta (200):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "q8f2c1e6b9...",
  "expiresIn": 1800,
  "user": {
    "id": "665f00000000000000000001",
    "name": "Líder INOVAGAB",
    "email": "lider@aguiabranca.com",
    "role": "LIDER",
    "division": "CORPORATIVO",
    "points": 0,
    "badges": []
  }
}
```

**Erros específicos desta rota:**
- 400 `VALIDATION_ERROR`
- 401 `INVALID_CREDENTIALS`
- 429 `RATE_LIMITED` (5ª tentativa errada bloqueia a conta por 15 min; 10 chamadas/min por IP)

---

## POST `/api/v1/auth/refresh`

**Perfis:** público

Troca o refresh token por um novo par; o anterior deixa de valer (rotação).

**Requisição:**
```json
{
  "refreshToken": "texto"
}
```

**Resposta (200):**
```json
{
  "accessToken": "texto",
  "refreshToken": "texto",
  "expiresIn": 1,
  "user": {
    "id": "665f00000000000000000001",
    "name": "Exemplo",
    "email": "usuario@aguiabranca.com",
    "role": "OPERADOR",
    "division": "PASSAGEIROS",
    "points": 1,
    "badges": [
      "texto"
    ]
  }
}
```

**Erros específicos desta rota:**
- 401 `TOKEN_INVALID` (refresh inválido, expirado ou reutilizado — revoga a família inteira)
- 429 `RATE_LIMITED`

---

## POST `/api/v1/auth/logout`

**Perfis:** autenticado

Revoga a sessão do refresh token informado.

**Requisição:**
```json
{
  "refreshToken": "texto"
}
```


---

## GET `/api/v1/auth/me`

**Perfis:** autenticado

Perfil autenticado, com pontos e badges atualizados.

**Resposta (200):**
```json
{
  "id": "665f00000000000000000001",
  "name": "Exemplo",
  "email": "usuario@aguiabranca.com",
  "role": "OPERADOR",
  "division": "PASSAGEIROS",
  "points": 1,
  "badges": [
    "texto"
  ]
}
```

---

## GET `/api/v1/guidelines`

**Perfis:** autenticado

Lista paginada, da orientação mais recentemente alterada para a mais antiga.

**Resposta (200):**
```json
{
  "items": [
    {
      "id": "665f00000000000000000001",
      "title": "Exemplo",
      "description": "texto",
      "pillar": "DIRECIONAMENTO",
      "campaign": "texto",
      "authorId": "665f00000000000000000001",
      "authorName": "Exemplo",
      "createdAt": "2026-09-21T14:03:11Z",
      "updatedAt": "2026-09-21T14:03:11Z"
    }
  ],
  "page": 1,
  "pageSize": 1,
  "totalItems": 1,
  "totalPages": 1
}
```

---

## GET `/api/v1/guidelines/history`

**Perfis:** autenticado

Histórico de orientações (imutável), com filtros `guidelineId`, `category`, `campaign`, `from`, `to`.

**Resposta (200):**
```json
{
  "items": [
    {
      "id": "665f00000000000000000001",
      "guidelineId": "665f00000000000000000001",
      "occurredAt": "2026-09-21T14:03:11Z",
      "category": "DIRECIONAMENTO",
      "campaign": "texto",
      "action": "CREATED",
      "title": "Exemplo",
      "snapshot": {
        "title": "Exemplo",
        "description": "texto",
        "pillar": "DIRECIONAMENTO",
        "campaign": "texto"
      },
      "changedById": "665f00000000000000000001",
      "changedByName": "Exemplo"
    }
  ],
  "page": 1,
  "pageSize": 1,
  "totalItems": 1,
  "totalPages": 1
}
```

---

## GET `/api/v1/guidelines/{id}`

**Perfis:** autenticado

Detalhe de uma orientação estratégica.

**Resposta (200):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "description": "texto",
  "pillar": "DIRECIONAMENTO",
  "campaign": "texto",
  "authorId": "665f00000000000000000001",
  "authorName": "Exemplo",
  "createdAt": "2026-09-21T14:03:11Z",
  "updatedAt": "2026-09-21T14:03:11Z"
}
```

---

## POST `/api/v1/guidelines`

**Perfis:** LIDER

Cria uma orientação estratégica.

**Requisição:**
```json
{
  "title": "Exemplo",
  "description": "texto",
  "pillar": "DIRECIONAMENTO",
  "campaign": "texto"
}
```

**Resposta (201):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "description": "texto",
  "pillar": "DIRECIONAMENTO",
  "campaign": "texto",
  "authorId": "665f00000000000000000001",
  "authorName": "Exemplo",
  "createdAt": "2026-09-21T14:03:11Z",
  "updatedAt": "2026-09-21T14:03:11Z"
}
```

---

## PUT `/api/v1/guidelines/{id}`

**Perfis:** LIDER

Edita uma orientação (grava entrada no histórico).

**Requisição:**
```json
{
  "title": "Exemplo",
  "description": "texto",
  "pillar": "DIRECIONAMENTO",
  "campaign": "texto"
}
```

**Resposta (200):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "description": "texto",
  "pillar": "DIRECIONAMENTO",
  "campaign": "texto",
  "authorId": "665f00000000000000000001",
  "authorName": "Exemplo",
  "createdAt": "2026-09-21T14:03:11Z",
  "updatedAt": "2026-09-21T14:03:11Z"
}
```

---

## DELETE `/api/v1/guidelines/{id}`

**Perfis:** LIDER

Exclui a orientação; o histórico é preservado.


---

## GET `/api/v1/ideas`

**Perfis:** autenticado (operador só vê as próprias)

Lista paginada; `scope` = `mine`\|`curation`\|`all`, filtros `status`, `guidelineId`, `division`.

**Resposta (200):**
```json
{
  "items": [
    {
      "id": "665f00000000000000000001",
      "title": "Exemplo",
      "description": "texto",
      "category": "texto",
      "division": "PASSAGEIROS",
      "guidelineId": "665f00000000000000000001",
      "guidelineTitle": "texto",
      "authorId": "665f00000000000000000001",
      "authorName": "Exemplo",
      "status": "SUBMETIDA",
      "ice": {
        "impact": 1,
        "confidence": 1,
        "ease": 1,
        "score": 1
      },
      "reviewerId": "665f00000000000000000001",
      "reviewComment": "texto",
      "createdAt": "2026-09-21T14:03:11Z",
      "updatedAt": "2026-09-21T14:03:11Z",
      "reviewedAt": "2026-09-21T14:03:11Z",
      "linkedProject": {
        "id": "665f00000000000000000001",
        "stage": "PLANEJAMENTO",
        "updatedAt": "2026-09-21T14:03:11Z"
      },
      "pointsAwarded": 1
    }
  ],
  "page": 1,
  "pageSize": 1,
  "totalItems": 1,
  "totalPages": 1
}
```

---

## GET `/api/v1/ideas/{id}`

**Perfis:** autenticado (operador só vê as próprias)

Detalhe da ideia, com `guidelineTitle`, `ice` e `linkedProject` (projeto criado na aprovação).

**Resposta (200):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "description": "texto",
  "category": "texto",
  "division": "PASSAGEIROS",
  "guidelineId": "665f00000000000000000001",
  "guidelineTitle": "texto",
  "authorId": "665f00000000000000000001",
  "authorName": "Exemplo",
  "status": "SUBMETIDA",
  "ice": {
    "impact": 1,
    "confidence": 1,
    "ease": 1,
    "score": 1
  },
  "reviewerId": "665f00000000000000000001",
  "reviewComment": "texto",
  "createdAt": "2026-09-21T14:03:11Z",
  "updatedAt": "2026-09-21T14:03:11Z",
  "reviewedAt": "2026-09-21T14:03:11Z",
  "linkedProject": {
    "id": "665f00000000000000000001",
    "stage": "PLANEJAMENTO",
    "updatedAt": "2026-09-21T14:03:11Z"
  },
  "pointsAwarded": 1
}
```

---

## POST `/api/v1/ideas`

**Perfis:** OPERADOR, GESTOR

Cadastra uma ideia; credita +10 pts (+5 com orientação vinculada) ao autor.

**Requisição:**
```json
{
  "title": "Roteirização com IA",
  "description": "Otimizar rotas de entrega com aprendizado de máquina.",
  "category": "Tecnologia",
  "division": "LOGISTICA",
  "guidelineId": "665f00000000000000000010"
}
```

**Resposta (201):**
```json
{
  "id": "666000000000000000000001",
  "title": "Roteirização com IA",
  "status": "SUBMETIDA",
  "guidelineId": "665f00000000000000000010",
  "guidelineTitle": "Eficiência operacional na logística",
  "authorId": "665f00000000000000000005",
  "authorName": "Operador INOVAGAB",
  "ice": null,
  "linkedProject": null,
  "createdAt": "2026-09-21T14:03:11Z",
  "pointsAwarded": 15
}
```

**Erros específicos desta rota:**
- 422 `GUIDELINE_NOT_FOUND` (guidelineId inexistente)

---

## PUT `/api/v1/ideas/{id}`

**Perfis:** autor da ideia (SUBMETIDA)

Edita a ideia enquanto `SUBMETIDA`.

**Requisição:**
```json
{
  "title": "Exemplo",
  "description": "texto",
  "category": "texto",
  "division": "PASSAGEIROS",
  "guidelineId": "665f00000000000000000001"
}
```

**Resposta (200):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "description": "texto",
  "category": "texto",
  "division": "PASSAGEIROS",
  "guidelineId": "665f00000000000000000001",
  "guidelineTitle": "texto",
  "authorId": "665f00000000000000000001",
  "authorName": "Exemplo",
  "status": "SUBMETIDA",
  "ice": {
    "impact": 1,
    "confidence": 1,
    "ease": 1,
    "score": 1
  },
  "reviewerId": "665f00000000000000000001",
  "reviewComment": "texto",
  "createdAt": "2026-09-21T14:03:11Z",
  "updatedAt": "2026-09-21T14:03:11Z",
  "reviewedAt": "2026-09-21T14:03:11Z",
  "linkedProject": {
    "id": "665f00000000000000000001",
    "stage": "PLANEJAMENTO",
    "updatedAt": "2026-09-21T14:03:11Z"
  },
  "pointsAwarded": 1
}
```

**Erros específicos desta rota:**
- 409 `IDEA_NOT_EDITABLE` (fora de SUBMETIDA)
- 403 (não é o autor)
- 422 `GUIDELINE_NOT_FOUND`

---

## DELETE `/api/v1/ideas/{id}`

**Perfis:** autor da ideia (SUBMETIDA)

Exclui a ideia enquanto `SUBMETIDA`; estorna os pontos concedidos na criação.


**Erros específicos desta rota:**
- 409 `IDEA_NOT_EDITABLE` (fora de SUBMETIDA)
- 403 (não é o autor)

---

## PUT `/api/v1/ideas/{id}/ice`

**Perfis:** GESTOR

Salva a matriz ICE (1–10 cada dimensão); `SUBMETIDA` → `EM_ANALISE`.

**Requisição:**
```json
{
  "impact": 8,
  "confidence": 7,
  "ease": 6
}
```

**Resposta (200):**
```json
{
  "id": "666000000000000000000001",
  "status": "EM_ANALISE",
  "ice": {
    "impact": 8,
    "confidence": 7,
    "ease": 6,
    "score": 336
  }
}
```

**Erros específicos desta rota:**
- 409 `IDEA_INVALID_STATE` (fora de SUBMETIDA/EM_ANALISE)

---

## POST `/api/v1/ideas/{id}/approve`

**Perfis:** GESTOR (nunca o próprio autor)

Aprova: cria o projeto rascunho e credita +50 pts ao autor (idempotente).

**Resposta (200):**
```json
{
  "ideaId": "666000000000000000000001",
  "projectId": "666100000000000000000001",
  "alreadyApproved": false
}
```

**Erros específicos desta rota:**
- 403 `SELF_APPROVAL_FORBIDDEN` (o autor não aprova a própria ideia)
- 409 `IDEA_INVALID_STATE`

---

## POST `/api/v1/ideas/{id}/reject`

**Perfis:** GESTOR

Rejeita a ideia com um comentário obrigatório.

**Requisição:**
```json
{
  "comment": "Fora do escopo estratégico deste trimestre."
}
```

**Resposta (200):**
```json
{
  "id": "666000000000000000000001",
  "status": "REJEITADA",
  "reviewComment": "Fora do escopo estratégico deste trimestre."
}
```

**Erros específicos desta rota:**
- 409 `IDEA_INVALID_STATE`
- 400 (comentário vazio)

---

## GET `/api/v1/projects`

**Perfis:** GESTOR, LIDER

Lista paginada; filtros `stage`, `division`, `guidelineId`.

**Resposta (200):**
```json
{
  "items": [
    {
      "id": "665f00000000000000000001",
      "title": "Exemplo",
      "description": "texto",
      "stage": "PLANEJAMENTO",
      "statusText": "texto",
      "investment": 1000.0,
      "targetDate": "2026-09-21T14:03:11Z",
      "financialReturn": 1000.0,
      "productivityGain": 1000.0,
      "costReduction": 1000.0,
      "division": "PASSAGEIROS",
      "guidelineId": "665f00000000000000000001",
      "guidelineTitle": "texto",
      "creatorManagerId": "665f00000000000000000001",
      "originatingIdeaId": "665f00000000000000000001",
      "priorityScore": 1,
      "reporterId": "665f00000000000000000001",
      "reporterName": "Exemplo",
      "responsibleId": "665f00000000000000000001",
      "responsibleName": "Exemplo",
      "netProfit": 1000.0,
      "roiPercent": 1000.0,
      "version": 1,
      "createdAt": "2026-09-21T14:03:11Z",
      "updatedAt": "2026-09-21T14:03:11Z"
    }
  ],
  "page": 1,
  "pageSize": 1,
  "totalItems": 1,
  "totalPages": 1
}
```

---

## GET `/api/v1/projects/{id}`

**Perfis:** GESTOR, LIDER

Detalhe do projeto, com `netProfit` e `roiPercent` calculados.

**Resposta (200):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "description": "texto",
  "stage": "PLANEJAMENTO",
  "statusText": "texto",
  "investment": 1000.0,
  "targetDate": "2026-09-21T14:03:11Z",
  "financialReturn": 1000.0,
  "productivityGain": 1000.0,
  "costReduction": 1000.0,
  "division": "PASSAGEIROS",
  "guidelineId": "665f00000000000000000001",
  "guidelineTitle": "texto",
  "creatorManagerId": "665f00000000000000000001",
  "originatingIdeaId": "665f00000000000000000001",
  "priorityScore": 1,
  "reporterId": "665f00000000000000000001",
  "reporterName": "Exemplo",
  "responsibleId": "665f00000000000000000001",
  "responsibleName": "Exemplo",
  "netProfit": 1000.0,
  "roiPercent": 1000.0,
  "version": 1,
  "createdAt": "2026-09-21T14:03:11Z",
  "updatedAt": "2026-09-21T14:03:11Z"
}
```

---

## GET `/api/v1/projects/{id}/updates`

**Perfis:** GESTOR, LIDER

Histórico do projeto (timeline), mais recente primeiro, com o diff dos campos alterados.

**Resposta (200):**
```json
{
  "items": [
    {
      "id": "665f00000000000000000001",
      "projectId": "665f00000000000000000001",
      "authorId": "665f00000000000000000001",
      "authorName": "Exemplo",
      "note": "texto",
      "changes": [
        {
          "field": "texto",
          "from": null,
          "to": null
        }
      ],
      "createdAt": "2026-09-21T14:03:11Z"
    }
  ],
  "page": 1,
  "pageSize": 1,
  "totalItems": 1,
  "totalPages": 1
}
```

---

## POST `/api/v1/projects`

**Perfis:** GESTOR

Cadastro direto de projeto (sem partir de uma ideia aprovada).

**Requisição:**
```json
{
  "title": "Exemplo",
  "description": "texto",
  "stage": "PLANEJAMENTO",
  "statusText": "texto",
  "investment": 1000.0,
  "targetDate": "2026-09-21T14:03:11Z",
  "financialReturn": 1000.0,
  "productivityGain": 1000.0,
  "costReduction": 1000.0,
  "division": "PASSAGEIROS",
  "guidelineId": "665f00000000000000000001",
  "responsibleId": "665f00000000000000000001",
  "note": "texto",
  "version": 1
}
```

**Resposta (201):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "description": "texto",
  "stage": "PLANEJAMENTO",
  "statusText": "texto",
  "investment": 1000.0,
  "targetDate": "2026-09-21T14:03:11Z",
  "financialReturn": 1000.0,
  "productivityGain": 1000.0,
  "costReduction": 1000.0,
  "division": "PASSAGEIROS",
  "guidelineId": "665f00000000000000000001",
  "guidelineTitle": "texto",
  "creatorManagerId": "665f00000000000000000001",
  "originatingIdeaId": "665f00000000000000000001",
  "priorityScore": 1,
  "reporterId": "665f00000000000000000001",
  "reporterName": "Exemplo",
  "responsibleId": "665f00000000000000000001",
  "responsibleName": "Exemplo",
  "netProfit": 1000.0,
  "roiPercent": 1000.0,
  "version": 1,
  "createdAt": "2026-09-21T14:03:11Z",
  "updatedAt": "2026-09-21T14:03:11Z"
}
```

**Erros específicos desta rota:**
- 422 `GUIDELINE_NOT_FOUND`
- 422 `RESPONSIBLE_NOT_FOUND`

---

## PUT `/api/v1/projects/{id}`

**Perfis:** GESTOR

Substituição completa; grava histórico com diff. `stage=CONCLUIDO` implementa a ideia de origem (+200 pts, badge "Impacto Real"). `version` opcional ativa a checagem de concorrência.

**Requisição:**
```json
{
  "title": "PROJ: Roteirização com IA",
  "description": "Piloto concluído.",
  "stage": "CONCLUIDO",
  "statusText": "Entregue",
  "investment": 120000,
  "financialReturn": 310000,
  "productivityGain": 12.5,
  "costReduction": 45000,
  "targetDate": "2026-12-01T00:00:00Z",
  "division": "LOGISTICA",
  "guidelineId": "665f00000000000000000010",
  "responsibleId": "665f00000000000000000002",
  "note": "Meta atingida",
  "version": 3
}
```

**Resposta (200):**
```json
{
  "id": "666100000000000000000001",
  "stage": "CONCLUIDO",
  "netProfit": 190000,
  "roiPercent": 158.33,
  "version": 4
}
```

**Erros específicos desta rota:**
- 409 `CONCURRENCY_CONFLICT` (versão divergente — outro gestor editou antes)
- 422 `GUIDELINE_NOT_FOUND`
- 422 `RESPONSIBLE_NOT_FOUND`

---

## DELETE `/api/v1/projects/{id}`

**Perfis:** GESTOR

Exclui o projeto e o seu histórico; a ideia de origem permanece como está.


---

## GET `/api/v1/reports/summary`

**Perfis:** LIDER

Dashboard: funil, KPIs, sparkline de 6 meses, impacto por orientação e projetos por ROI. Filtros `period` e `division`.

**Resposta (200):**
```json
{
  "period": "ALL",
  "division": "LOGISTICA",
  "generatedAt": "2026-09-21T22:15:00Z",
  "funnel": {
    "submitted": 6,
    "evaluated": 5,
    "approved": 3,
    "inExecution": 2,
    "roiPositive": 1
  },
  "kpis": {
    "roiConsolidated": 36.73,
    "netProfit": 90000,
    "totalInvestment": 245000,
    "totalReturn": 335000,
    "activeProjects": 1,
    "avgProductivityGain": 8.25,
    "totalCostReduction": 53000,
    "overdueProjects": 0
  },
  "sparkline": [
    {
      "month": "2026-04",
      "roiPercent": null
    },
    {
      "month": "2026-09",
      "roiPercent": 36.73
    }
  ],
  "guidelineImpacts": [
    {
      "guidelineId": "665f00000000000000000010",
      "title": "Eficiência operacional na logística",
      "ideasCount": 3,
      "projectsCount": 2,
      "investment": 120000,
      "financialReturn": 310000,
      "netProfit": 190000,
      "roiPercent": 158.33
    }
  ],
  "projects": [
    {
      "id": "666100000000000000000001",
      "title": "PROJ: Roteirização com IA",
      "stage": "CONCLUIDO",
      "division": "LOGISTICA",
      "guidelineId": "665f00000000000000000010",
      "guidelineTitle": "Eficiência operacional na logística",
      "investment": 120000,
      "financialReturn": 310000,
      "netProfit": 190000,
      "roiPercent": 158.33,
      "productivityGain": 12.5,
      "costReduction": 45000,
      "targetDate": "2026-12-01T00:00:00Z",
      "daysToDeadline": null,
      "overdue": false,
      "statusText": "Entregue",
      "updatedAt": "2026-09-21T14:03:11Z"
    }
  ]
}
```

---

## GET `/api/v1/reports/guidelines`

**Perfis:** LIDER

Retorno por estratégia: impacto de cada orientação (mesmos filtros do resumo).

**Resposta (200):**
```json
{
  "period": "THIS_MONTH",
  "division": "PASSAGEIROS",
  "generatedAt": "2026-09-21T14:03:11Z",
  "items": [
    {
      "guidelineId": "665f00000000000000000001",
      "title": "Exemplo",
      "ideasCount": 1,
      "projectsCount": 1,
      "investment": 1000.0,
      "financialReturn": 1000.0,
      "netProfit": 1000.0,
      "roiPercent": 1000.0
    }
  ]
}
```

---

## GET `/api/v1/reports/guidelines/{id}`

**Perfis:** LIDER

Ideias (por status), projetos, investimento, retorno, lucro e ROI de uma orientação.

**Resposta (200):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "pillar": "DIRECIONAMENTO",
  "campaign": "texto",
  "period": "THIS_MONTH",
  "division": "PASSAGEIROS",
  "generatedAt": "2026-09-21T14:03:11Z",
  "ideasCount": 1,
  "ideasByStatus": [
    {
      "status": "SUBMETIDA",
      "count": 1
    }
  ],
  "ideas": [
    {
      "id": "665f00000000000000000001",
      "title": "Exemplo",
      "status": "SUBMETIDA",
      "iceScore": 1,
      "division": "PASSAGEIROS",
      "createdAt": "2026-09-21T14:03:11Z"
    }
  ],
  "projectsCount": 1,
  "projects": [
    {
      "id": "665f00000000000000000001",
      "title": "Exemplo",
      "stage": "PLANEJAMENTO",
      "division": "PASSAGEIROS",
      "guidelineId": "665f00000000000000000001",
      "guidelineTitle": "texto",
      "investment": 1000.0,
      "financialReturn": 1000.0,
      "netProfit": 1000.0,
      "roiPercent": 1000.0,
      "productivityGain": 1000.0,
      "costReduction": 1000.0,
      "targetDate": "2026-09-21T14:03:11Z",
      "daysToDeadline": 1,
      "overdue": false,
      "statusText": "texto",
      "updatedAt": "2026-09-21T14:03:11Z"
    }
  ],
  "investment": 1000.0,
  "financialReturn": 1000.0,
  "netProfit": 1000.0,
  "roiPercent": 1000.0
}
```

**Erros específicos desta rota:**
- 404 (orientação inexistente)

---

## GET `/api/v1/reports/projects/{id}`

**Perfis:** LIDER

Investimento, retorno, lucro, ROI, produtividade, redução de custo e prazo de um projeto.

**Resposta (200):**
```json
{
  "id": "665f00000000000000000001",
  "title": "Exemplo",
  "stage": "PLANEJAMENTO",
  "division": "PASSAGEIROS",
  "guidelineId": "665f00000000000000000001",
  "guidelineTitle": "texto",
  "investment": 1000.0,
  "financialReturn": 1000.0,
  "netProfit": 1000.0,
  "roiPercent": 1000.0,
  "productivityGain": 1000.0,
  "costReduction": 1000.0,
  "targetDate": "2026-09-21T14:03:11Z",
  "daysToDeadline": 1,
  "overdue": false,
  "statusText": "texto",
  "updatedAt": "2026-09-21T14:03:11Z"
}
```

**Erros específicos desta rota:**
- 404 (projeto inexistente)

---

## POST `/api/v1/reports/insights`

**Perfis:** LIDER

Insights de IA (Google Gemini) sobre o mesmo resumo do dashboard: resumo, destaques, riscos e recomendações. Cache de 6 h por filtros+dados; `refresh=true` força nova geração.

**Requisição:**
```json
{
  "period": "ALL",
  "division": "LOGISTICA",
  "guidelineId": null,
  "refresh": false
}
```

**Resposta (200):**
```json
{
  "summary": "O portfólio de inovação apresenta ROI consolidado de 36,73%, puxado pelo projeto de roteirização com IA.",
  "highlights": [
    "ROI consolidado de 36,73% com lucro líquido de R$ 90 mil.",
    "Ganho médio de produtividade de 8,25% nas iniciativas."
  ],
  "risks": [
    "Concentração do retorno em um único projeto de alto desempenho."
  ],
  "recommendations": [
    {
      "title": "Expandir a eficiência logística",
      "detail": "Usar o piloto de roteirização como modelo para novas iniciativas.",
      "priority": "MEDIA",
      "relatedGuidelineId": "665f00000000000000000010"
    }
  ],
  "generatedAt": "2026-09-21T22:16:00Z",
  "model": "gemini-3.1-flash-lite",
  "fromCache": false
}
```

**Erros específicos desta rota:**
- 404 (guidelineId inexistente)
- 429 `RATE_LIMITED` (6/min por usuário ou teto diário — com `Retry-After`)
- 502 `AI_INVALID_RESPONSE` (resposta do modelo fora do schema)
- 503 `AI_UNAVAILABLE` (Gemini indisponível, sem chave configurada, ou cota do provedor esgotada)

---

## GET `/api/v1/users`

**Perfis:** GESTOR, LIDER

Usuários (`id`, `name`, `role`, `division`) para seleção de responsável; filtro `role`.

**Resposta (200):**
```json
[
  {
    "id": "665f00000000000000000001",
    "name": "Exemplo",
    "role": "OPERADOR",
    "division": "PASSAGEIROS"
  }
]
```

---

## GET `/api/v1/users/ranking`

**Perfis:** autenticado

Top do mês corrente (fuso `America/Sao_Paulo`), somente operadores; `limit` 1–50 (padrão 5).

**Resposta (200):**
```json
[
  {
    "id": "665f00000000000000000001",
    "name": "Exemplo",
    "monthPoints": 1
  }
]
```

---

## Formato de erro (`application/problem+json`)

```json
{
  "type": "urn:aguiabranca:error:invalid-credentials",
  "title": "Não autenticado",
  "status": 401,
  "detail": "E-mail ou senha inválidos.",
  "instance": "/api/v1/auth/login",
  "code": "INVALID_CREDENTIALS",
  "errors": [
    {
      "code": "INVALID_CREDENTIALS",
      "message": "E-mail ou senha inválidos.",
      "field": null
    }
  ],
  "traceId": "3bd1ba8876394c548be1316bfa623c68"
}
```

Catálogo completo de códigos: ver `.specs/features/sprint2/spec.md` (seção "Códigos de erro").
