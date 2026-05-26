# Roadmap INOVAGAB

## Sprint 1 — App nativo (deadline 26/05/2026)

| ID | Feature | Perfis envolvidos | Status |
|---|---|---|---|
| F1 | Autenticação por perfil + sessão (R-01) | Todos | em planejamento |
| F2 | Orientações estratégicas — CRUD líder, read demais (R-02) | Todos | em planejamento |
| F3 | Ideias com ICE + aprovação + jornada (R-03) | Operador, Gestor | em planejamento |
| F4 | Projetos com histórico de atualizações (R-04) | Gestor, Líder | em planejamento |
| F5 | Dashboard com funil, KPIs, sparkline, "Impacto por Orientação" e modo apresentação (R-05) | Líder | em planejamento |
| F6 | Gamificação por marcos + badges + ranking (R-06) | Operador, Gestor | em planejamento |
| F7 | Conexão Estratégica — eixo central (R-03.14, R-05.10) | Operador, Gestor, Líder | em planejamento |

## Sprint 2 — Backend (segundo semestre 2026)

- API REST robusta em Java ou C# substituindo o Firebase
- Autenticação JWT com restrições por nível de acesso
- Camada de governança: auditoria, logs, métricas
- Migração de dados Firestore → banco SQL próprio

## Pós-entrega (ideias para evolução)

- Engajamento social: curtidas + comentários entre operadores (cortado do Sprint 1 por foco em "integrar estratégia, execução e mensuração")
- Inovação aberta: integração com ecossistema externo (parceiros, startups)
- Notificações push (OneSignal/FCM) para mudanças de status de ideia/projeto
- Modo offline avançado com sincronização em fila
- Versão iOS espelhada
- Sugestão automática de orientação estratégica por similaridade (NLP) ao cadastrar ideia
