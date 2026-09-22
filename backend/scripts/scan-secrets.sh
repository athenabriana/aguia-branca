#!/usr/bin/env bash
# Varredura de segredos nos arquivos versionados (B21). Falha (exit 1) se encontrar padrões suspeitos.
# Uso: backend/scripts/scan-secrets.sh   (na raiz do repositório ou em qualquer subpasta)
set -u
cd "$(git rev-parse --show-toplevel)" || exit 2

fail=0
check() { # descrição, regex (ERE), [pathspecs a excluir...]
  local desc="$1" regex="$2"; shift 2
  local excludes=(); for e in "$@"; do excludes+=(":(exclude)$e"); done
  local hits; hits=$(git grep -nIE -e "$regex" -- . "${excludes[@]}" 2>/dev/null)
  if [ -n "$hits" ]; then echo "✗ $desc"; echo "$hits" | sed -E 's/^/    /' | cut -c1-200; fail=1; else echo "✓ $desc"; fi
}

# Exceções aceitas (não são segredos):
#  - mobile/app/google-services.json: chave Android do Firebase, embutida no APK por design (restrita por pacote/SHA-1);
#  - backend/tests/*: valores falsos usados pelos testes (ex.: chave fictícia para provar que não vaza em log).
check "chaves do Google (AIza…)"                   'AIza[0-9A-Za-z_-]{30,}' 'mobile/app/google-services.json' 'backend/tests/*'
check "chaves privadas PEM"                        '-----BEGIN (RSA |EC |OPENSSH |)PRIVATE KEY-----'
check "connection string com credenciais (mongodb+srv / user:senha@)" 'mongodb(\+srv)?://[^/ "@:]+:[^/ "@]+@' 'backend/README.md' '.specs/*' 'backend/tests/*'
check "JWT_KEY / Jwt:Key preenchidos em arquivos de config" '("Key"|JWT_KEY|Jwt__Key)[":= ]+[A-Za-z0-9+/=_-]{24,}' 'backend/README.md' '.specs/*' 'backend/tests/*'
check "GEMINI_API_KEY / ApiKey preenchidos"        '(GEMINI_API_KEY|Gemini__ApiKey|"ApiKey")[":= ]+[A-Za-z0-9_-]{20,}' 'backend/tests/*'
check "service accounts / client secrets"          '"private_key_id"|"client_secret"'

if git ls-files --error-unmatch backend/.env >/dev/null 2>&1; then echo "✗ backend/.env está versionado"; fail=1; else echo "✓ backend/.env fora do git"; fi
git check-ignore -q backend/.env && echo "✓ backend/.env no .gitignore" || { echo "✗ backend/.env não está no .gitignore"; fail=1; }

[ $fail -eq 0 ] && echo "Nenhum segredo encontrado." || echo "ATENÇÃO: revise os achados acima."
exit $fail
