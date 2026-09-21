#!/usr/bin/env bash
# Smoke test da API publicada (local via docker compose ou deploy): saúde, login demo, dashboard e (opcional) insights de IA.
# Uso:  backend/scripts/smoke.sh [BASE_URL]        (padrão: http://localhost:5080)
#       SMOKE_INSIGHTS=1 backend/scripts/smoke.sh  (também chama a IA: consome 1 geração da cota)
# Requer: curl e python3. Usuários de demonstração: ver README (senha padrão do seed).
set -u
BASE="${1:-http://localhost:5080}"
PASS="${SMOKE_PASSWORD:-aguiabranca123}"
fail=0

ok()   { echo "✓ $1"; }
bad()  { echo "✗ $1"; fail=1; }
json() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)" 2>/dev/null; }

echo "Smoke em $BASE"

code=$(curl -s -o /dev/null -w "%{http_code}" -m 15 "$BASE/health/ready")
[ "$code" = "200" ] && ok "GET /health/ready = 200" || bad "GET /health/ready = $code (esperado 200)"

login() { curl -s -m 15 -X POST "$BASE/api/v1/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$1\",\"password\":\"$PASS\"}"; }

LIDER=$(login lider@aguiabranca.com | json "d['accessToken']")
[ -n "$LIDER" ] && ok "login do líder (seed)" || bad "login do líder falhou (seed desligado ou senha diferente?)"

if [ -n "$LIDER" ]; then
  resp=$(curl -s -m 20 -w "\n%{http_code}" "$BASE/api/v1/reports/summary" -H "Authorization: Bearer $LIDER")
  status=${resp##*$'\n'}; body=${resp%$'\n'*}
  if [ "$status" = "200" ]; then
    ok "GET /reports/summary = 200 ($(echo "$body" | json "'ROI consolidado ' + str(d['kpis']['roiConsolidated']) + '%, ' + str(d['funnel']['submitted']) + ' ideias, ' + str(len(d['projects'])) + ' projetos'"))"
  else bad "GET /reports/summary = $status"; fi

  OP=$(login operador@aguiabranca.com | json "d['accessToken']")
  code=$(curl -s -o /dev/null -w "%{http_code}" -m 15 "$BASE/api/v1/reports/summary" -H "Authorization: Bearer $OP")
  [ "$code" = "403" ] && ok "operador no dashboard = 403" || bad "operador no dashboard = $code (esperado 403)"

  code=$(curl -s -o /dev/null -w "%{http_code}" -m 15 "$BASE/api/v1/guidelines")
  [ "$code" = "401" ] && ok "sem token = 401" || bad "sem token = $code (esperado 401)"

  if [ "${SMOKE_INSIGHTS:-0}" = "1" ]; then
    resp=$(curl -s -m 60 -w "\n%{http_code}" -X POST "$BASE/api/v1/reports/insights" -H "Authorization: Bearer $LIDER" -H 'Content-Type: application/json' -d '{"period":"ALL"}')
    status=${resp##*$'\n'}; body=${resp%$'\n'*}
    [ "$status" = "200" ] && ok "POST /reports/insights = 200 (modelo $(echo "$body" | json "d['model']"), fromCache=$(echo "$body" | json "d['fromCache']"))" || bad "POST /reports/insights = $status: $(echo "$body" | json "d.get('code')")"
  fi
fi

headers=$(curl -sI -m 15 "$BASE/api/v1/auth/me")
echo "$headers" | grep -qi "x-content-type-options: nosniff" && ok "headers de segurança presentes" || bad "headers de segurança ausentes"

[ $fail -eq 0 ] && echo "SMOKE OK" || echo "SMOKE COM FALHAS"
exit $fail
