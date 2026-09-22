# Checklist E2E — app ⇄ backend real (Sprint 2)

Roteiro de validação **no emulador**, com o backend real (API .NET + MongoDB em `docker compose`) e os dados de demonstração.
Legenda: ✅ executado e conferido · ⏳ não executado ainda · ⚠️ executado com ressalva (ver nota).

## Ambiente

| Item | Valor |
|---|---|
| Backend | `cd backend && docker compose --profile api up --build -d` → `http://localhost:5080` (seed ligado) |
| App | build **debug** (`API_BASE_URL = http://10.0.2.2:5080/api/v1/`) instalado num emulador Android |
| Usuários (senha `aguiabranca123`) | `lider@`, `gestor@`, `operador@aguiabranca.com` |
| Smoke do backend antes do roteiro | `backend/scripts/smoke.sh` |

## Execução (preencher durante o teste)

_Ver a seção "Resultado da execução" ao final._

## Roteiro

### 1. Sessão e autenticação
- [x] Login com os 3 perfis (botões de login rápido) e abertura da home de cada perfil
- [x] Senha errada → mensagem "E-mail ou senha inválidos."
- [x] Reabrir o app mantém a sessão (splash espera `/auth/me`)
- [x] Logout volta ao Login e limpa a sessão
- [ ] Token expirado → refresh silencioso (sem tela de login) ⚠️ Validar no backend 
- [ ] Sem rede → erro tratado ("Sem conexão…"), sem crash ⚠️ Simular sem rede 

### 2. Líder — orientações
- [x] Criar orientação (com campanha opcional) → aparece no topo imediatamente
- [x] Editar → reposiciona; excluir → some
- [x] Gestor/operador não veem ações de escrita

### 3. Operador — ideias
- [x] Cadastrar ideia **sem** orientação → toast **+10 pts** (vindo do servidor)
- [x] Cadastrar ideia **com** orientação → toast **+15 pts**
- [x] Detalhe da ideia: stepper da jornada; badge da orientação (ou "Orientação removida")
- [x] Excluir ideia `SUBMETIDA` estorna pontos

### 4. Gestor — curadoria e projetos
- [x] Salvar ICE (SUBMETIDA → EM_ANALISE) e aprovar → projeto rascunho criado, autor **+50**
- [ ] Gestor não aprova a própria ideia (mensagem do servidor) ⚠️ Validar no backend (frontend não deixa gestor criar ideias).
- [ ] Rejeitar com comentário ⚠️ Validar no backend
- [x] Editar projeto → timeline com o diff (números, textos, prazo)
- [x] Concluir projeto → ideia **IMPLEMENTADA**, autor **+200**, badge **Impacto Real**
- [ ] Edição concorrente → "projeto alterado por outro gestor" 
- [x] Operador não acessa projetos; líder só lê

### 5. Líder — dashboard e IA
- [x] Dashboard: funil, KPIs (inclui projetos atrasados), sparkline, impacto por orientação, lista por ROI
- [x] Filtros de período e divisão refazem a chamada; ROI nulo aparece como "—"
- [ ] Editar um projeto e voltar → KPIs atualizados (invalidação imediata) ❌ Atualiza, mas dados do ranking estão incorretos.
- [x] Modo apresentação
- [x] **✨ Gerar insights** → loading → resumo, destaques, riscos, recomendações (chips de prioridade), selo "Gerado por IA — valide antes de decidir", data e cache
- [ ] "Atualizar" gera de novo (`refresh=true`); mudar filtro avisa que o resultado é de outro recorte
- [ ] IA indisponível/limite → mensagem amigável + "Tentar novamente" (nenhum conteúdo falso)

### 6. Perfil e ranking
- [ ] Ranking mostra **pontos do mês** ❌ Pontos errados
- [x] Perfil mostra pontos e badges reais do servidor

### 7. Autorização
- [ ] Operador tentando rota de gestor/líder → bloqueado (tela/redirecionamento e/ou 403 tratado) ⚠️ Validar no backend 
## Resultado da execução

- É um bug ou regra de negócio? A tela de dashboard não tem opção de recarregar dados. (ranking fica desatualizado). Mesmo com o gatilho de alteração de projetos (+200 pontos para o autor operador, o ranking não apresenta os mesmos pontos que a collection de usuário). (ex: valor de pontos no dashboard para o operador "Operador INOVAGAB" = 540pts. Valor na collection = 605pts).





- Após algumas tentativas de geração de insights, foi possível gerar um insight. As duas primeiras tentativas falharam com mensagem "Tente novamente".
- Insights gerados não são recuperados do banco 