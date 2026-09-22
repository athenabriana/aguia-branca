#!/usr/bin/env python3
"""
Gera `docs/api/ENDPOINTS.md` a partir de `docs/api/openapi.json` (D01).

O OpenAPI do Swashbuckle não carrega os perfis exigidos por rota (as policies do ASP.NET não geram uma extensão
`x-roles` automaticamente), então este script combina o schema exportado com uma tabela de perfis mantida a mão —
a mesma fonte usada pela matriz de autorização testada em `backend/tests/AguiaBranca.Api.Tests/Authorization/
AuthorizationMatrixTests.cs` (o teste falha se um perfil aqui divergir do que o servidor realmente aplica).

Uso:
    python3 docs/api/generate_endpoints.py
    (opcional) --openapi <caminho>  --out <caminho>

Não edite `docs/api/ENDPOINTS.md` a mão: rode este script de novo depois de exportar um `openapi.json` atualizado
(`curl http://localhost:5080/swagger/v1/swagger.json -o docs/api/openapi.json`, com a API no ar).
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

# ── Perfis por rota (fonte: AuthorizationMatrixTests.cs — a mesma matriz que o backend testa) ──────────────────
ROLES: dict[str, str] = {
    "POST /api/v1/auth/login": "público",
    "POST /api/v1/auth/refresh": "público",
    "POST /api/v1/auth/logout": "autenticado",
    "GET /api/v1/auth/me": "autenticado",
    "GET /api/v1/guidelines": "autenticado",
    "GET /api/v1/guidelines/history": "autenticado",
    "GET /api/v1/guidelines/{id}": "autenticado",
    "POST /api/v1/guidelines": "LIDER",
    "PUT /api/v1/guidelines/{id}": "LIDER",
    "DELETE /api/v1/guidelines/{id}": "LIDER",
    "GET /api/v1/ideas": "autenticado (operador só vê as próprias)",
    "GET /api/v1/ideas/{id}": "autenticado (operador só vê as próprias)",
    "POST /api/v1/ideas": "OPERADOR, GESTOR",
    "PUT /api/v1/ideas/{id}": "autor da ideia (SUBMETIDA)",
    "DELETE /api/v1/ideas/{id}": "autor da ideia (SUBMETIDA)",
    "PUT /api/v1/ideas/{id}/ice": "GESTOR",
    "POST /api/v1/ideas/{id}/approve": "GESTOR (nunca o próprio autor)",
    "POST /api/v1/ideas/{id}/reject": "GESTOR",
    "GET /api/v1/projects": "GESTOR, LIDER",
    "GET /api/v1/projects/{id}": "GESTOR, LIDER",
    "GET /api/v1/projects/{id}/updates": "GESTOR, LIDER",
    "POST /api/v1/projects": "GESTOR",
    "PUT /api/v1/projects/{id}": "GESTOR",
    "DELETE /api/v1/projects/{id}": "GESTOR",
    "GET /api/v1/reports/summary": "LIDER",
    "GET /api/v1/reports/guidelines": "LIDER",
    "GET /api/v1/reports/guidelines/{id}": "LIDER",
    "GET /api/v1/reports/projects/{id}": "LIDER",
    "POST /api/v1/reports/insights": "LIDER",
    "GET /api/v1/users": "GESTOR, LIDER",
    "GET /api/v1/users/ranking": "autenticado",
}

DESCRIPTIONS: dict[str, str] = {
    "POST /api/v1/auth/login": "E-mail + senha → `accessToken` (30 min), `refreshToken` (7 dias) e o perfil.",
    "POST /api/v1/auth/refresh": "Troca o refresh token por um novo par; o anterior deixa de valer (rotação).",
    "POST /api/v1/auth/logout": "Revoga a sessão do refresh token informado.",
    "GET /api/v1/auth/me": "Perfil autenticado, com pontos e badges atualizados.",
    "GET /api/v1/guidelines": "Lista paginada, da orientação mais recentemente alterada para a mais antiga.",
    "GET /api/v1/guidelines/history": "Histórico de orientações (imutável), com filtros `guidelineId`, `category`, `campaign`, `from`, `to`.",
    "GET /api/v1/guidelines/{id}": "Detalhe de uma orientação estratégica.",
    "POST /api/v1/guidelines": "Cria uma orientação estratégica.",
    "PUT /api/v1/guidelines/{id}": "Edita uma orientação (grava entrada no histórico).",
    "DELETE /api/v1/guidelines/{id}": "Exclui a orientação; o histórico é preservado.",
    "GET /api/v1/ideas": "Lista paginada; `scope` = `mine`\\|`curation`\\|`all`, filtros `status`, `guidelineId`, `division`.",
    "GET /api/v1/ideas/{id}": "Detalhe da ideia, com `guidelineTitle`, `ice` e `linkedProject` (projeto criado na aprovação).",
    "POST /api/v1/ideas": "Cadastra uma ideia; credita +10 pts (+5 com orientação vinculada) ao autor.",
    "PUT /api/v1/ideas/{id}": "Edita a ideia enquanto `SUBMETIDA`.",
    "DELETE /api/v1/ideas/{id}": "Exclui a ideia enquanto `SUBMETIDA`; estorna os pontos concedidos na criação.",
    "PUT /api/v1/ideas/{id}/ice": "Salva a matriz ICE (1–10 cada dimensão); `SUBMETIDA` → `EM_ANALISE`.",
    "POST /api/v1/ideas/{id}/approve": "Aprova: cria o projeto rascunho e credita +50 pts ao autor (idempotente).",
    "POST /api/v1/ideas/{id}/reject": "Rejeita a ideia com um comentário obrigatório.",
    "GET /api/v1/projects": "Lista paginada; filtros `stage`, `division`, `guidelineId`.",
    "GET /api/v1/projects/{id}": "Detalhe do projeto, com `netProfit` e `roiPercent` calculados.",
    "GET /api/v1/projects/{id}/updates": "Histórico do projeto (timeline), mais recente primeiro, com o diff dos campos alterados.",
    "POST /api/v1/projects": "Cadastro direto de projeto (sem partir de uma ideia aprovada).",
    "PUT /api/v1/projects/{id}": "Substituição completa; grava histórico com diff. `stage=CONCLUIDO` implementa a ideia de origem (+200 pts, badge \"Impacto Real\"). `version` opcional ativa a checagem de concorrência.",
    "DELETE /api/v1/projects/{id}": "Exclui o projeto e o seu histórico; a ideia de origem permanece como está.",
    "GET /api/v1/reports/summary": "Dashboard: funil, KPIs, sparkline de 6 meses, impacto por orientação e projetos por ROI. Filtros `period` e `division`.",
    "GET /api/v1/reports/guidelines": "Retorno por estratégia: impacto de cada orientação (mesmos filtros do resumo).",
    "GET /api/v1/reports/guidelines/{id}": "Ideias (por status), projetos, investimento, retorno, lucro e ROI de uma orientação.",
    "GET /api/v1/reports/projects/{id}": "Investimento, retorno, lucro, ROI, produtividade, redução de custo e prazo de um projeto.",
    "POST /api/v1/reports/insights": "Insights de IA (Google Gemini) sobre o mesmo resumo do dashboard: resumo, destaques, riscos e recomendações. Cache de 6 h por filtros+dados; `refresh=true` força nova geração.",
    "GET /api/v1/users": "Usuários (`id`, `name`, `role`, `division`) para seleção de responsável; filtro `role`.",
    "GET /api/v1/users/ranking": "Top do mês corrente (fuso `America/Sao_Paulo`), somente operadores; `limit` 1–50 (padrão 5).",
}

# Erros de negócio específicos, além do catálogo comum (ver a nota de rodapé do documento gerado).
SPECIFIC_ERRORS: dict[str, list[str]] = {
    "POST /api/v1/auth/login": ["400 `VALIDATION_ERROR`", "401 `INVALID_CREDENTIALS`", "429 `RATE_LIMITED` (5ª tentativa errada bloqueia a conta por 15 min; 10 chamadas/min por IP)"],
    "POST /api/v1/auth/refresh": ["401 `TOKEN_INVALID` (refresh inválido, expirado ou reutilizado — revoga a família inteira)", "429 `RATE_LIMITED`"],
    "POST /api/v1/ideas": ["422 `GUIDELINE_NOT_FOUND` (guidelineId inexistente)"],
    "PUT /api/v1/ideas/{id}": ["409 `IDEA_NOT_EDITABLE` (fora de SUBMETIDA)", "403 (não é o autor)", "422 `GUIDELINE_NOT_FOUND`"],
    "DELETE /api/v1/ideas/{id}": ["409 `IDEA_NOT_EDITABLE` (fora de SUBMETIDA)", "403 (não é o autor)"],
    "PUT /api/v1/ideas/{id}/ice": ["409 `IDEA_INVALID_STATE` (fora de SUBMETIDA/EM_ANALISE)"],
    "POST /api/v1/ideas/{id}/approve": ["403 `SELF_APPROVAL_FORBIDDEN` (o autor não aprova a própria ideia)", "409 `IDEA_INVALID_STATE`"],
    "POST /api/v1/ideas/{id}/reject": ["409 `IDEA_INVALID_STATE`", "400 (comentário vazio)"],
    "POST /api/v1/projects": ["422 `GUIDELINE_NOT_FOUND`", "422 `RESPONSIBLE_NOT_FOUND`"],
    "PUT /api/v1/projects/{id}": ["409 `CONCURRENCY_CONFLICT` (versão divergente — outro gestor editou antes)", "422 `GUIDELINE_NOT_FOUND`", "422 `RESPONSIBLE_NOT_FOUND`"],
    "GET /api/v1/reports/guidelines/{id}": ["404 (orientação inexistente)"],
    "GET /api/v1/reports/projects/{id}": ["404 (projeto inexistente)"],
    "POST /api/v1/reports/insights": [
        "404 (guidelineId inexistente)",
        "429 `RATE_LIMITED` (6/min por usuário ou teto diário — com `Retry-After`)",
        "502 `AI_INVALID_RESPONSE` (resposta do modelo fora do schema)",
        "503 `AI_UNAVAILABLE` (Gemini indisponível, sem chave configurada, ou cota do provedor esgotada)",
    ],
}

# Exemplos reais/curados (batem com `spec.md` e com chamadas de smoke feitas contra a API real).
CURATED: dict[str, dict] = {
    "POST /api/v1/auth/login": {
        "request": {"email": "lider@aguiabranca.com", "password": "aguiabranca123"},
        "response": (200, {
            "accessToken": "eyJhbGciOiJIUzI1NiIs...", "refreshToken": "q8f2c1e6b9...", "expiresIn": 1800,
            "user": {"id": "665f00000000000000000001", "name": "Líder INOVAGAB", "email": "lider@aguiabranca.com",
                     "role": "LIDER", "division": "CORPORATIVO", "points": 0, "badges": []}
        }),
    },
    "POST /api/v1/ideas": {
        "request": {"title": "Roteirização com IA", "description": "Otimizar rotas de entrega com aprendizado de máquina.",
                    "category": "Tecnologia", "division": "LOGISTICA", "guidelineId": "665f00000000000000000010"},
        "response": (201, {
            "id": "666000000000000000000001", "title": "Roteirização com IA", "status": "SUBMETIDA",
            "guidelineId": "665f00000000000000000010", "guidelineTitle": "Eficiência operacional na logística",
            "authorId": "665f00000000000000000005", "authorName": "Operador INOVAGAB",
            "ice": None, "linkedProject": None, "createdAt": "2026-09-21T14:03:11Z", "pointsAwarded": 15
        }),
    },
    "PUT /api/v1/ideas/{id}/ice": {
        "request": {"impact": 8, "confidence": 7, "ease": 6},
        "response": (200, {"id": "666000000000000000000001", "status": "EM_ANALISE",
                            "ice": {"impact": 8, "confidence": 7, "ease": 6, "score": 336}}),
    },
    "POST /api/v1/ideas/{id}/approve": {
        "request": None,
        "response": (200, {"ideaId": "666000000000000000000001", "projectId": "666100000000000000000001", "alreadyApproved": False}),
    },
    "POST /api/v1/ideas/{id}/reject": {
        "request": {"comment": "Fora do escopo estratégico deste trimestre."},
        "response": (200, {"id": "666000000000000000000001", "status": "REJEITADA", "reviewComment": "Fora do escopo estratégico deste trimestre."}),
    },
    "PUT /api/v1/projects/{id}": {
        "request": {"title": "PROJ: Roteirização com IA", "description": "Piloto concluído.", "stage": "CONCLUIDO",
                    "statusText": "Entregue", "investment": 120000, "financialReturn": 310000, "productivityGain": 12.5,
                    "costReduction": 45000, "targetDate": "2026-12-01T00:00:00Z", "division": "LOGISTICA",
                    "guidelineId": "665f00000000000000000010", "responsibleId": "665f00000000000000000002",
                    "note": "Meta atingida", "version": 3},
        "response": (200, {"id": "666100000000000000000001", "stage": "CONCLUIDO", "netProfit": 190000, "roiPercent": 158.33, "version": 4}),
    },
    "GET /api/v1/reports/summary": {
        "request": None,
        "response": (200, {
            "period": "ALL", "division": "LOGISTICA", "generatedAt": "2026-09-21T22:15:00Z",
            "funnel": {"submitted": 6, "evaluated": 5, "approved": 3, "inExecution": 2, "roiPositive": 1},
            "kpis": {"roiConsolidated": 36.73, "netProfit": 90000, "totalInvestment": 245000, "totalReturn": 335000,
                     "activeProjects": 1, "avgProductivityGain": 8.25, "totalCostReduction": 53000, "overdueProjects": 0},
            "sparkline": [{"month": "2026-04", "roiPercent": None}, {"month": "2026-09", "roiPercent": 36.73}],
            "guidelineImpacts": [{"guidelineId": "665f00000000000000000010", "title": "Eficiência operacional na logística",
                                   "ideasCount": 3, "projectsCount": 2, "investment": 120000, "financialReturn": 310000,
                                   "netProfit": 190000, "roiPercent": 158.33}],
            "projects": [{"id": "666100000000000000000001", "title": "PROJ: Roteirização com IA", "stage": "CONCLUIDO",
                          "division": "LOGISTICA", "guidelineId": "665f00000000000000000010",
                          "guidelineTitle": "Eficiência operacional na logística", "investment": 120000,
                          "financialReturn": 310000, "netProfit": 190000, "roiPercent": 158.33, "productivityGain": 12.5,
                          "costReduction": 45000, "targetDate": "2026-12-01T00:00:00Z", "daysToDeadline": None,
                          "overdue": False, "statusText": "Entregue", "updatedAt": "2026-09-21T14:03:11Z"}]
        }),
    },
    "POST /api/v1/reports/insights": {
        "request": {"period": "ALL", "division": "LOGISTICA", "guidelineId": None, "refresh": False},
        "response": (200, {
            "summary": "O portfólio de inovação apresenta ROI consolidado de 36,73%, puxado pelo projeto de roteirização com IA.",
            "highlights": ["ROI consolidado de 36,73% com lucro líquido de R$ 90 mil.", "Ganho médio de produtividade de 8,25% nas iniciativas."],
            "risks": ["Concentração do retorno em um único projeto de alto desempenho."],
            "recommendations": [{"title": "Expandir a eficiência logística", "detail": "Usar o piloto de roteirização como modelo para novas iniciativas.",
                                  "priority": "MEDIA", "relatedGuidelineId": "665f00000000000000000010"}],
            "generatedAt": "2026-09-21T22:16:00Z", "model": "gemini-3.1-flash-lite", "fromCache": False
        }),
    },
}

COMMON_ERRORS_NOTE = (
    "Além dos erros específicos listados por rota, toda a API pode responder (formato comum, ver rodapé): "
    "`400 VALIDATION_ERROR` (corpo/query inválidos), `401 TOKEN_INVALID` (sem token ou expirado — rotas autenticadas), "
    "`403 FORBIDDEN` (perfil sem permissão), `404 RESOURCE_NOT_FOUND` (id inexistente ou malformado), "
    "`429 RATE_LIMITED` (limite de requisições) e `500 INTERNAL_ERROR`."
)


def load_openapi(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def string_placeholder(prop_name: str) -> str:
    """Placeholder mais legível para strings, baseado no nome do campo (id, e-mail, nome…)."""
    name = (prop_name or "").lower()
    if name == "id" or name.endswith("id"):
        return "665f00000000000000000001"
    if "email" in name:
        return "usuario@aguiabranca.com"
    if "name" in name or name == "title":
        return "Exemplo"
    return "texto"


def resolve(schema: dict, components: dict, depth: int = 0, prop_name: str = ""):
    """Resolve um `$ref` e gera um valor de exemplo representativo a partir do tipo declarado."""
    if depth > 14 or schema is None:
        return None
    if "$ref" in schema:
        name = schema["$ref"].rsplit("/", 1)[-1]
        return resolve(components["schemas"][name], components, depth + 1, prop_name)

    if "enum" in schema:
        return schema["enum"][0]

    t = schema.get("type")
    if t == "object" or "properties" in schema:
        return {k: resolve(v, components, depth + 1, k) for k, v in schema.get("properties", {}).items()}
    if t == "array":
        item = resolve(schema.get("items", {}), components, depth + 1, prop_name)
        return [item] if item is not None else []
    if t == "integer":
        return 1
    if t == "number":
        return 1000.0
    if t == "boolean":
        return False
    if t == "string":
        if schema.get("format") == "date-time":
            return "2026-09-21T14:03:11Z"
        return string_placeholder(prop_name)
    return None


def response_examples(op: dict, components: dict) -> list[tuple[str, dict]]:
    out = []
    for status, resp in op.get("responses", {}).items():
        content = resp.get("content", {}).get("application/json")
        if content and "schema" in content:
            out.append((status, resolve(content["schema"], components)))
        elif status not in ("204",):
            out.append((status, None))
    return out


def request_example(op: dict, components: dict):
    body = op.get("requestBody")
    if not body:
        return None
    content = body.get("content", {}).get("application/json")
    if not content or "schema" not in content:
        return None
    return resolve(content["schema"], components)


def dump(value) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--openapi", type=Path, default=Path(__file__).parent / "openapi.json")
    parser.add_argument("--out", type=Path, default=Path(__file__).parent / "ENDPOINTS.md")
    args = parser.parse_args()

    spec = load_openapi(args.openapi)
    components = spec["components"]
    methods_order = ["get", "post", "put", "delete", "patch"]

    # Ordem lógica (igual à tabela de contrato do spec), não a ordem alfabética do JSON.
    order = list(ROLES.keys())
    ops_by_key: dict[str, tuple[str, str, dict]] = {}
    for path, methods in spec["paths"].items():
        for m in methods_order:
            if m in methods:
                key = f"{m.upper()} {path}"
                ops_by_key[key] = (m, path, methods[m])

    missing = [k for k in order if k not in ops_by_key]
    extra = [k for k in ops_by_key if k not in order]
    if missing or extra:
        raise SystemExit(f"Divergência entre a tabela de perfis e o openapi.json — faltando: {missing} · sobrando: {extra}")

    lines: list[str] = []
    lines.append(f"# {spec['info']['title']} — Especificação de endpoints")
    lines.append("")
    lines.append(
        "> Gerado automaticamente por `docs/api/generate_endpoints.py` a partir de `docs/api/openapi.json` "
        "(exportado de `/swagger/v1/swagger.json` da API rodando). **Não edite este arquivo a mão** — rode o "
        "script de novo depois de atualizar o `openapi.json`. Perfis e erros de negócio vêm de uma tabela mantida "
        "no próprio script, sincronizada com a matriz de autorização testada no backend "
        "(`AuthorizationMatrixTests.cs`, B22)."
    )
    lines.append("")
    lines.append(f"Base URL: `/api/v1` · Autenticação: header `Authorization: Bearer <accessToken>` (schema `{list(components.get('securitySchemes', {}).keys())[0]}`, JWT).")
    lines.append("")
    lines.append(COMMON_ERRORS_NOTE)
    lines.append("")

    # Sumário
    lines.append("## Sumário")
    lines.append("")
    lines.append("| Método | Rota | Perfis | Sucesso |")
    lines.append("|---|---|---|---|")
    for key in order:
        method, path, op = ops_by_key[key]
        ok_status = next((s for s in op.get("responses", {}) if s.startswith("2")), "?")
        lines.append(f"| {method.upper()} | `{path}` | {ROLES[key]} | {ok_status} |")
    lines.append("")
    lines.append("---")
    lines.append("")

    for key in order:
        method, path, op = ops_by_key[key]
        lines.append(f"## {method.upper()} `{path}`")
        lines.append("")
        lines.append(f"**Perfis:** {ROLES[key]}")
        lines.append("")
        lines.append(DESCRIPTIONS.get(key, op.get("summary") or ""))
        lines.append("")

        curated = CURATED.get(key)
        req_example = curated["request"] if curated else request_example(op, components)
        if req_example is not None:
            lines.append("**Requisição:**")
            lines.append("```json")
            lines.append(dump(req_example))
            lines.append("```")
            lines.append("")

        if curated and "response" in curated:
            status, body = curated["response"]
            lines.append(f"**Resposta ({status}):**")
            lines.append("```json")
            lines.append(dump(body))
            lines.append("```")
        else:
            for status, body in response_examples(op, components):
                lines.append(f"**Resposta ({status}):**")
                if body is not None:
                    lines.append("```json")
                    lines.append(dump(body))
                    lines.append("```")
                else:
                    lines.append("_(sem corpo)_")
        lines.append("")

        specific = SPECIFIC_ERRORS.get(key)
        if specific:
            lines.append("**Erros específicos desta rota:**")
            for e in specific:
                lines.append(f"- {e}")
            lines.append("")

        lines.append("---")
        lines.append("")

    lines.append("## Formato de erro (`application/problem+json`)")
    lines.append("")
    lines.append("```json")
    lines.append(dump({
        "type": "urn:aguiabranca:error:invalid-credentials", "title": "Não autenticado", "status": 401,
        "detail": "E-mail ou senha inválidos.", "instance": "/api/v1/auth/login", "code": "INVALID_CREDENTIALS",
        "errors": [{"code": "INVALID_CREDENTIALS", "message": "E-mail ou senha inválidos.", "field": None}],
        "traceId": "3bd1ba8876394c548be1316bfa623c68"
    }))
    lines.append("```")
    lines.append("")
    lines.append("Catálogo completo de códigos: ver `.specs/features/sprint2/spec.md` (seção \"Códigos de erro\").")
    lines.append("")

    args.out.write_text("\n".join(lines), encoding="utf-8")
    print(f"Gerado {args.out} a partir de {args.openapi} ({len(order)} endpoints).")


if __name__ == "__main__":
    main()
