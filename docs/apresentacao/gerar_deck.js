/**
 * Gera docs/apresentacao/Sprint2_AguiaBranca.pptx (D03) — as capturas de tela do app ficam em ./assets
 * (geradas manualmente no emulador; não são recapturadas por este script) e o diagrama de arquitetura vem
 * de docs/arquitetura/backend-arquitetura.png (D02).
 *
 * Uso: node docs/apresentacao/gerar_deck.js   (exige `npm install pptxgenjs` — não é dependência do projeto)
 *
 * IMPORTANTE: preencher nome completo e RM de cada integrante no slide de capa antes da entrega
 * (hoje com placeholders "[ A PREENCHER PELO GRUPO ]").
 */
const pptxgen = require("pptxgenjs");
const path = require("path");

const ROOT = path.join(__dirname, "..", "..");
const IMG = (...p) => path.join(ROOT, ...p);
const SHOT = (name) => path.join(__dirname, "assets", name);

// ── Paleta: "Midnight Executive" — combina com o azul já usado no app/backend ─────────────────
const NAVY = "1E2761";
const NAVY_DARK = "141A4A";
const ICE = "CADCFC";
const WHITE = "FFFFFF";
const GOLD = "F5A623";
const INK = "1B1F3B";
const MUTED = "5B6482";
const CARD = "F4F6FC";
const GREEN = "1B873F";
const RED = "C0392B";

const FONT_HEAD = "Cambria";
const FONT_BODY = "Calibri";

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.333 x 7.5
const W = 13.333, H = 7.5;

pres.defineSlideMaster({
  title: "CONTENT",
  background: { color: WHITE },
  objects: [
    { rect: { x: 0, y: 0, w: W, h: 0.9, fill: { color: NAVY } } },
    { text: {
        text: "INOVAGAB · Sprint 2",
        options: { x: 0.5, y: 0, w: 6, h: 0.9, fontFace: FONT_BODY, fontSize: 11, color: ICE, valign: "middle", charSpacing: 1 }
    } },
    { text: {
        text: "Grupo Águia Branca — Challenge FIAP",
        options: { x: W - 5.5, y: 0, w: 5, h: 0.9, align: "right", fontFace: FONT_BODY, fontSize: 11, color: ICE, valign: "middle" }
    } },
  ],
  slideNumber: { x: W - 0.7, y: H - 0.45, fontFace: FONT_BODY, fontSize: 10, color: MUTED },
});

// Eyebrow (kicker) ACIMA do título, título ACIMA do corpo — cada bloco com folga real para o próximo
// (a barra superior do master termina em y=0.9).
function kicker(slide, text) {
  slide.addText(text.toUpperCase(), {
    x: 0.5, y: 1.0, w: W - 1, h: 0.32,
    fontFace: FONT_BODY, fontSize: 13, color: GOLD, bold: true, charSpacing: 1.2, isTextBox: true,
  });
}
function title(slide, text, opts = {}) {
  slide.addText(text, {
    x: 0.5, y: 1.32, w: W - 1, h: 0.62,
    fontFace: FONT_HEAD, fontSize: 30, bold: true, color: INK, isTextBox: true, ...opts,
  });
}
// y inicial padrão para o parágrafo de apoio logo abaixo do título (título termina em 1.94).
const BODY_Y = 2.1;
function pill(slide, text, x, y, w, color) {
  slide.addShape(pres.ShapeType.roundRect, { x, y, w, h: 0.42, rectRadius: 0.08, fill: { color, transparency: 85 }, line: { type: "none" } });
  slide.addText(text, { x, y, w, h: 0.42, align: "center", valign: "middle", fontFace: FONT_BODY, fontSize: 11.5, bold: true, color, isTextBox: true });
}

// ── 1) Capa ──────────────────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: NAVY };
  s.addShape(pres.ShapeType.rect, { x: 0, y: 0, w: W, h: H, fill: { color: NAVY } });
  s.addShape(pres.ShapeType.ellipse, { x: 9.6, y: -2.3, w: 6.5, h: 6.5, fill: { color: NAVY_DARK }, line: { type: "none" } });
  s.addShape(pres.ShapeType.ellipse, { x: -2.2, y: 4.6, w: 5.2, h: 5.2, fill: { color: NAVY_DARK }, line: { type: "none" } });

  s.addText("INOVAGAB", { x: 0.9, y: 2.15, w: 10, h: 1.1, fontFace: FONT_HEAD, fontSize: 54, bold: true, color: WHITE, isTextBox: true });
  s.addText("Plataforma de inovação corporativa — Grupo Águia Branca", {
    x: 0.95, y: 3.15, w: 10.5, h: 0.55, fontFace: FONT_BODY, fontSize: 18, color: ICE, isTextBox: true,
  });
  pill(s, "Sprint 2 — Challenge FIAP 2026", 0.95, 3.85, 3.7, GOLD);
  s.addText(
    "Backend .NET 8 + MongoDB · migração do Firebase · insights de IA (Google Gemini) · app Android integrado",
    { x: 0.95, y: 4.55, w: 9.5, h: 0.7, fontFace: FONT_BODY, fontSize: 13, color: ICE, isTextBox: true }
  );

  s.addShape(pres.ShapeType.line, { x: 0.95, y: 5.55, w: 6.5, h: 0, line: { color: "3B4380", width: 1 } });
  s.addText("Integrantes (nome completo — RM)", { x: 0.95, y: 5.68, w: 6, h: 0.32, fontFace: FONT_BODY, fontSize: 11, color: GOLD, bold: true, isTextBox: true });
  const team = ["◻ Nome completo do integrante 1 — RM 000000", "◻ Nome completo do integrante 2 — RM 000000",
                "◻ Nome completo do integrante 3 — RM 000000", "◻ Nome completo do integrante 4 — RM 000000"];
  s.addText(team.map((t, i) => ({ text: t, options: { breakLine: i < team.length - 1 } })), {
    x: 0.95, y: 6.0, w: 6.5, h: 1.2, fontFace: FONT_BODY, fontSize: 13, color: WHITE, isTextBox: true, paraSpaceAfter: 6,
  });
  s.addText("[ A PREENCHER PELO GRUPO ]", { x: 7.7, y: 6.0, w: 4.8, h: 0.4, fontFace: FONT_BODY, fontSize: 11, italic: true, color: "8891C4", isTextBox: true });

  s.addNotes("Slide de abertura. Preencher nome completo e RM de cada integrante antes da entrega.");
}

// ── 2) Agenda ────────────────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  title(s, "Agenda");
  const items = [
    ["01", "O que foi entregue", "Do backend em Firebase para uma plataforma própria, ponta a ponta"],
    ["02", "Arquitetura do backend", "Clean Architecture, .NET 8, MongoDB"],
    ["03", "Especificação da API", "31 endpoints, contrato versionado"],
    ["04", "Segurança", "JWT, perfis, rate limiting, hardening"],
    ["05", "Migração Firebase → MongoDB", "Ferramenta própria, idempotente"],
    ["06", "Insights de IA (Gemini)", "Modelo, guardrails, demonstração"],
    ["07", "App ↔ backend integrado", "Fluxo real, de ponta a ponta"],
    ["08", "Qualidade e o que falta", "Testes, cobertura, próximos passos"],
  ];
  const cols = 4, colW = 2.783, rowH = 2.3, gx = 0.5, gy = 2.05, gapX = 0.4, gapY = 0.35;
  items.forEach((it, i) => {
    const col = i % cols, row = Math.floor(i / cols);
    const x = gx + col * (colW + gapX), y = gy + row * (rowH + gapY);
    s.addShape(pres.ShapeType.roundRect, { x, y, w: colW, h: rowH, rectRadius: 0.06, fill: { color: CARD }, line: { type: "none" } });
    s.addText(it[0], { x: x + 0.18, y: y + 0.16, w: colW - 0.36, h: 0.65, fontFace: FONT_HEAD, fontSize: 26, bold: true, color: "B9C6E8", isTextBox: true });
    s.addText(it[1], { x: x + 0.18, y: y + 0.92, w: colW - 0.36, h: 0.65, fontFace: FONT_BODY, fontSize: 13.5, bold: true, color: INK, isTextBox: true });
    s.addText(it[2], { x: x + 0.18, y: y + 1.5, w: colW - 0.36, h: 0.7, fontFace: FONT_BODY, fontSize: 10, color: MUTED, isTextBox: true });
  });
}

// ── 3) O que foi entregue ───────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Visão geral");
  title(s, "De um app com Firebase a uma plataforma própria");
  s.addText(
    "A Sprint 1 rodava direto contra o Firebase (Auth + Firestore), com regras de negócio no próprio app. " +
    "A Sprint 2 substitui isso por um backend real — com autenticação, regras de negócio, relatórios e IA no servidor — " +
    "e migra os dados existentes, sem quebrar o app.",
    { x: 0.5, y: BODY_Y, w: 12.3, h: 0.55, fontFace: FONT_BODY, fontSize: 14, color: MUTED, isTextBox: true }
  );

  const stats = [
    ["31", "endpoints REST", GOLD],
    ["1.158", "testes automatizados (backend)", NAVY],
    ["101", "testes automatizados (app)", NAVY],
    ["~95%", "cobertura Domain/Application", GREEN],
  ];
  const sw = 2.85, sx = 0.5, sy = 2.85, gap = 0.2;
  stats.forEach((st, i) => {
    const x = sx + i * (sw + gap);
    s.addShape(pres.ShapeType.roundRect, { x, y: sy, w: sw, h: 1.55, rectRadius: 0.07, fill: { color: CARD }, line: { type: "none" } });
    s.addText(st[0], { x, y: sy + 0.15, w: sw, h: 0.75, align: "center", fontFace: FONT_HEAD, fontSize: 34, bold: true, color: st[2], isTextBox: true });
    s.addText(st[1], { x: x + 0.15, y: sy + 0.95, w: sw - 0.3, h: 0.5, align: "center", fontFace: FONT_BODY, fontSize: 11.5, color: MUTED, isTextBox: true });
  });

  const rows = [
    ["Backend", "C# / .NET 8 · ASP.NET Identity + JWT · EF Core · MongoDB (transações, réplica)"],
    ["IA", "Google Gemini (generateContent) — insights sobre os resultados do dashboard"],
    ["Migração", "Firestore → MongoDB, ferramenta própria idempotente com relatório de conciliação"],
    ["App", "Kotlin + Jetpack Compose, agora 100% integrado à API própria (Firebase Auth/Firestore removidos)"],
  ];
  let ry = 4.75;
  rows.forEach((r) => {
    s.addText(r[0], { x: 0.5, y: ry, w: 2.0, h: 0.42, fontFace: FONT_BODY, fontSize: 12.5, bold: true, color: NAVY, isTextBox: true });
    s.addText(r[1], { x: 2.6, y: ry, w: 10.2, h: 0.42, fontFace: FONT_BODY, fontSize: 12.5, color: INK, isTextBox: true });
    ry += 0.52;
  });
}

// ── 4) Arquitetura ───────────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Arquitetura");
  title(s, "Api → Application → Domain ← Infrastructure");

  // PNG original 1960x1800 (~1.089:1) — ajusta pela altura disponível e centraliza a largura resultante.
  const diagH = 5.2, diagW = diagH * (1960 / 1800);
  s.addImage({ path: IMG("docs/arquitetura/backend-arquitetura.png"), x: 0.5, y: BODY_Y, w: diagW, h: diagH });

  const facts = [
    ["App fala só com a API", "Nunca acessa Mongo ou Gemini diretamente — HTTPS + Bearer JWT"],
    ["Regra de dependência", "Api → Application → Domain ← Infrastructure (inversão de dependência)"],
    ["Domain é puro", "Sem ASP.NET, EF Core, MongoDB ou HTTP — verificado por teste de arquitetura"],
    ["Sem MediatR", "Um handler por caso de uso, injetado direto — menos indireção"],
    ["MongoDB com transações", "Réplica set (rs0); índices e TTL criados por um initializer no startup"],
  ];
  let fy = BODY_Y + 0.1;
  const fx = 0.5 + diagW + 0.4;
  facts.forEach((f) => {
    s.addText(f[0], { x: fx, y: fy, w: 12.8 - fx, h: 0.32, fontFace: FONT_BODY, fontSize: 12.5, bold: true, color: NAVY, isTextBox: true });
    s.addText(f[1], { x: fx, y: fy + 0.32, w: 12.8 - fx, h: 0.5, fontFace: FONT_BODY, fontSize: 11, color: MUTED, isTextBox: true });
    fy += 0.98;
  });
}

// ── 5) Camadas em detalhe ───────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Arquitetura");
  title(s, "Por que essa organização");
  const cols = [
    [NAVY, "Domain", "Entidades, regras (BadgeEvaluator, PointsRules), enums. Zero dependência de ASP.NET, EF Core, MongoDB ou HTTP — verificado por teste de arquitetura."],
    [GOLD, "Application", "Um handler por caso de uso, sem MediatR — rastro direto do controller até a regra de negócio. FluentValidation + Result<T>/Error."],
    ["3B7DDB", "Infrastructure", "Implementa as portas da Application: EF Core + MongoDB, transações com retry, JWT/Identity, cliente do Gemini resiliente."],
  ];
  const cw = 3.95, gap = 0.2, cx0 = 0.5, cy = 2.0, ch = 4.3;
  cols.forEach((c, i) => {
    const x = cx0 + i * (cw + gap);
    s.addShape(pres.ShapeType.roundRect, { x, y: cy, w: cw, h: ch, rectRadius: 0.08, fill: { color: WHITE }, line: { color: c[0], width: 1.25 } });
    s.addShape(pres.ShapeType.roundRect, { x, y: cy, w: cw, h: 0.62, rectRadius: 0.08, fill: { color: c[0] }, line: { type: "none" } });
    s.addShape(pres.ShapeType.rect, { x, y: cy + 0.3, w: cw, h: 0.32, fill: { color: c[0] }, line: { type: "none" } });
    s.addText(c[1], { x, y: cy, w: cw, h: 0.62, align: "center", valign: "middle", fontFace: FONT_HEAD, fontSize: 17, bold: true, color: WHITE, isTextBox: true });
    s.addText(c[2], { x: x + 0.28, y: cy + 0.85, w: cw - 0.56, h: ch - 1.1, fontFace: FONT_BODY, fontSize: 12.5, color: INK, isTextBox: true, valign: "top" });
  });
  s.addText(
    "Sem MediatR: um time pequeno ganha mais navegando direto do controller até a regra do que com a indireção extra de pipeline behaviors.",
    { x: 0.5, y: 6.45, w: 11.9, h: 0.5, fontFace: FONT_BODY, fontSize: 12, italic: true, color: MUTED, isTextBox: true }
  );
}

// ── 6) Especificação da API ─────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Contrato de API");
  title(s, "31 endpoints, documentados a partir do OpenAPI real");
  s.addText(
    "docs/api/ENDPOINTS.md é gerado por script a partir de docs/api/openapi.json (exportado do Swagger da API rodando) " +
    "— rota, perfis, payload e resposta de exemplo, erros específicos. Perfis vêm da matriz de autorização testada no backend.",
    { x: 0.5, y: BODY_Y, w: 12.3, h: 0.5, fontFace: FONT_BODY, fontSize: 12.5, color: MUTED, isTextBox: true }
  );

  const hCell = { bold: true, color: WHITE, fill: { color: NAVY }, fontFace: FONT_BODY, fontSize: 11 };
  const bCell = { color: INK, fontFace: FONT_BODY, fontSize: 10.5, fill: { color: WHITE } };

  const rawRows = [
    ["Recurso", "Rotas", "Perfis", "Exemplo"],
    ["Auth", "login · refresh · logout · me", "público / autenticado", "POST /auth/login → accessToken + refreshToken"],
    ["Orientações", "list · get · create · update · delete · history", "autenticado / LIDER escreve", "POST /guidelines (título, pilar, campanha)"],
    ["Ideias", "list · get · create · update · delete · ice · approve · reject", "OPERADOR/GESTOR cria · GESTOR avalia", "POST /ideas/{id}/approve → cria projeto, +50 pts"],
    ["Projetos", "list · get · updates · create · update · delete", "GESTOR escreve · GESTOR/LIDER lê", "PUT /projects/{id} → diff + version (409 se divergir)"],
    ["Relatórios", "summary · guidelines · guidelines/{id} · projects/{id}", "LIDER", "GET /reports/summary?period=&division="],
    ["IA", "insights", "LIDER", "POST /reports/insights {period, refresh}"],
    ["Usuários", "list · ranking", "GESTOR/LIDER / autenticado", "GET /users/ranking → top do mês"],
  ];
  const table = rawRows.map((row, ri) => row.map((c) => ({ text: c, options: ri === 0 ? hCell : bCell })));

  s.addTable(table, {
    x: 0.5, y: 2.85, w: 12.3, h: 3.9,
    colW: [2.0, 3.9, 2.7, 3.7],
    border: { type: "solid", color: "E2E6F2", pt: 0.75 },
    autoPage: false,
  });
}

// ── 7) Segurança ─────────────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Segurança");
  title(s, "Autenticação, perfis e endurecimento");

  const left = [
    ["Autenticação", "JWT HS256 (30 min) + refresh token opaco rotativo (7 dias). Reuso de um refresh já trocado revoga a família inteira."],
    ["Perfis (policies)", "GestorOnly · LiderOnly · CanCreateIdea · ProjectsRead · UsersRead — testados numa matriz de 31 rotas × 4 identidades (124 casos)."],
    ["Senhas e força bruta", "PBKDF2 (ASP.NET Identity); 5 tentativas erradas bloqueiam a conta por 15 min; rate limit de 10 req/min por IP em /auth/*."],
  ];
  const right = [
    ["Hardening (B21)", "CORS restrito a origens configuradas · limite de payload (413) · headers de segurança em toda resposta · HSTS em produção."],
    ["Segredos", "Chave JWT e chave do Gemini só por variável de ambiente; nunca em log; scan-secrets.sh varre o repositório antes de cada commit."],
    ["Erros padronizados", "application/problem+json com code estável e traceId; nenhuma resposta expõe stack trace."],
  ];
  function block(items, x) {
    let y = 2.0;
    items.forEach((it) => {
      s.addShape(pres.ShapeType.roundRect, { x, y, w: 5.9, h: 1.55, rectRadius: 0.07, fill: { color: CARD }, line: { type: "none" } });
      s.addText(it[0], { x: x + 0.22, y: y + 0.14, w: 5.5, h: 0.35, fontFace: FONT_BODY, fontSize: 13, bold: true, color: NAVY, isTextBox: true });
      s.addText(it[1], { x: x + 0.22, y: y + 0.5, w: 5.5, h: 0.95, fontFace: FONT_BODY, fontSize: 11, color: INK, isTextBox: true });
      y += 1.72;
    });
  }
  block(left, 0.5);
  block(right, 6.9);
}

// ── 8) Migração Firebase -> MongoDB ─────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Migração de dados");
  title(s, "Firestore → MongoDB, sem perder referências");
  const steps = [
    ["1", "Ler", "Lê users, orientações, ideias, projetos e atualizações do Firestore (service account ou emulador)"],
    ["2", "Transformar", "Valida e tipa cada item; enum/número inválido é reportado, nunca aborta a migração inteira"],
    ["3", "Remapear IDs", "Todo _id novo é ObjectId; legacyId guarda o id do Firestore; toda referência é remapeada (autor, orientação, projeto de origem…)"],
    ["4", "Gravar", "Upsert por _id, mapa legado→novo salvo antes das gravações — reexecutar não duplica nada"],
    ["5", "Conciliar", "Relatório: contagem origem × destino por coleção, órfãos, inválidos — sempre gerado, mesmo em --dry-run"],
  ];
  let x = 0.5;
  const w = 2.32, gap = 0.14;
  steps.forEach((st, i) => {
    s.addShape(pres.ShapeType.roundRect, { x, y: 2.05, w, h: 2.5, rectRadius: 0.08, fill: { color: i % 2 === 0 ? NAVY : GOLD }, line: { type: "none" } });
    s.addText(st[0], { x: x + 0.15, y: 2.15, w: 0.8, h: 0.6, fontFace: FONT_HEAD, fontSize: 26, bold: true, color: WHITE, isTextBox: true });
    s.addText(st[1], { x: x + 0.15, y: 2.72, w: w - 0.3, h: 0.4, fontFace: FONT_BODY, fontSize: 13.5, bold: true, color: WHITE, isTextBox: true });
    s.addText(st[2], { x: x + 0.15, y: 3.12, w: w - 0.3, h: 1.35, fontFace: FONT_BODY, fontSize: 10, color: WHITE, isTextBox: true });
    if (i < steps.length - 1) {
      s.addText("→", { x: x + w, y: 2.9, w: gap + 0.05, h: 0.6, align: "center", fontFace: FONT_BODY, fontSize: 18, bold: true, color: MUTED, isTextBox: true });
    }
    x += w + gap + 0.05;
  });

  const notes = [
    "Senhas do Firebase Auth (scrypt) não migram por design — usuários recriados com senha temporária, e-mail preservado.",
    "Pontos migram como evento de abertura (reason=MIGRATION); badges são recalculadas pelo avaliador do servidor.",
    "45 testes unitários (parsers, regras de dado ruim, idempotência) + 7 testes de integração com MongoDB real, provando que a API funciona sobre os dados migrados.",
  ];
  s.addText(notes.map((n, i) => ({ text: "•  " + n, options: { breakLine: i < notes.length - 1 } })), {
    x: 0.5, y: 4.85, w: 12.3, h: 1.7, fontFace: FONT_BODY, fontSize: 12.5, color: INK, isTextBox: true, paraSpaceAfter: 8,
  });
}

// ── 9) IA — modelo e guardrails ─────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Funcionalidade de IA (Plus)");
  title(s, "Insights sobre os resultados do dashboard");
  s.addText(
    "Funcionalidade escolhida: gerar, sob demanda, uma leitura executiva dos mesmos números do dashboard — resumo, " +
    "destaques, riscos e recomendações priorizadas — para a liderança decidir mais rápido.",
    { x: 0.5, y: BODY_Y, w: 12.3, h: 0.5, fontFace: FONT_BODY, fontSize: 13, color: MUTED, isTextBox: true }
  );

  pill(s, "Modelo: gemini-3.1-flash-lite (configurável)", 0.5, 2.75, 5.0, NAVY);
  pill(s, "POST /reports/insights · LIDER", 5.7, 2.75, 3.6, GOLD);

  const guard = [
    ["Sem PII", "Só agregados e títulos truncados (80 car.) — nunca nome, e-mail ou id de usuário. Testado capturando o payload real."],
    ["Anti-injeção", "Textos livres viram dado dentro de um bloco delimitado; orientações vão por referência (G1…), nunca por id."],
    ["Resiliente", "Timeout 20s por tentativa, 1 retry, circuit breaker. Falha → 503, nunca um insight inventado."],
    ["Cache + cota", "6h por hash dos dados; refresh=true força novo. 6/min por usuário + teto diário atômico (aiUsage)."],
  ];
  let gx = 0.5;
  const gw = 2.95, ggap = 0.17;
  guard.forEach((g) => {
    s.addShape(pres.ShapeType.roundRect, { x: gx, y: 3.45, w: gw, h: 1.85, rectRadius: 0.07, fill: { color: CARD }, line: { type: "none" } });
    s.addText(g[0], { x: gx + 0.18, y: 3.6, w: gw - 0.36, h: 0.35, fontFace: FONT_BODY, fontSize: 13, bold: true, color: NAVY, isTextBox: true });
    s.addText(g[1], { x: gx + 0.18, y: 3.98, w: gw - 0.36, h: 1.25, fontFace: FONT_BODY, fontSize: 10.5, color: INK, isTextBox: true });
    gx += gw + ggap;
  });

  s.addText(
    "Limitações assumidas: free tier pequeno e instável (créditos podem se esgotar) · só português, tom executivo · a IA pode " +
    "errar dentro do próprio schema — por isso o app sempre mostra “Gerado por IA — valide antes de decidir”.",
    { x: 0.5, y: 5.65, w: 12.3, h: 0.7, fontFace: FONT_BODY, fontSize: 11.5, italic: true, color: MUTED, isTextBox: true }
  );
}

// ── 10) IA — demonstração ────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Funcionalidade de IA (Plus)");
  title(s, "Demonstração — resposta real do Gemini");
  s.addText(
    'Chamada real, ambiente de desenvolvimento (dados do seed), ~2,7 s, modelo gemini-3.1-flash-lite:',
    { x: 0.5, y: BODY_Y, w: 12.3, h: 0.35, fontFace: FONT_BODY, fontSize: 12.5, color: MUTED, isTextBox: true }
  );
  s.addShape(pres.ShapeType.roundRect, { x: 0.5, y: 2.55, w: 7.6, h: 4.3, rectRadius: 0.07, fill: { color: NAVY_DARK }, line: { type: "none" } });
  s.addText(
    [
      { text: '"summary": ', options: { color: "8FA6E8" } },
      { text: '"O portfólio de inovação apresenta um ROI consolidado de 36,73%, com um lucro líquido de 90 mil reais gerado a partir de um investimento total de 245 mil reais. A eficiência operacional na logística destaca-se como o principal motor de retorno financeiro."', options: { color: WHITE, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: '"risks": ', options: { color: "8FA6E8" } },
      { text: '["ROI negativo de 68,75% no projeto de sustentabilidade (G2)", "Alta concentração de valor em um único projeto de alta performance"]', options: { color: "FFD9A0", breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: '"recommendations[0]": ', options: { color: "8FA6E8" } },
      { text: '{ "title": "Revisão do projeto de sustentabilidade", "priority": "ALTA", "relatedGuidelineId": "665f…" }', options: { color: "B7F5C8", breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: '"model": "gemini-3.1-flash-lite", "fromCache": false', options: { color: "8FA6E8" } },
    ],
    { x: 0.75, y: 2.75, w: 7.1, h: 3.95, fontFace: "Courier New", fontSize: 11, isTextBox: true, valign: "top", lineSpacingMultiple: 1.2 }
  );

  s.addImage({ path: SHOT("mobile-04-story-recomendacao.png"), x: 8.35, y: 2.55, w: 1.95, h: 4.33 });
  s.addImage({ path: SHOT("mobile-03-story-resumo.png"), x: 10.45, y: 2.55, w: 1.95, h: 4.33 });
  s.addText("Apresentação em stories: resumo (esq.) e recomendação com prioridade (dir.)", {
    x: 8.35, y: 6.95, w: 4.2, h: 0.4, align: "center", fontFace: FONT_BODY, fontSize: 9, italic: true, color: MUTED, isTextBox: true,
  });
}

// ── 11) Fluxo integrado app <-> backend ─────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "App ↔ backend");
  title(s, "Fluxo integrado, de ponta a ponta");
  s.addText(
    "App Android (Kotlin + Compose) → API .NET 8 real, no emulador, contra o backend em Docker. Sem mocks.",
    { x: 0.5, y: BODY_Y, w: 12.3, h: 0.35, fontFace: FONT_BODY, fontSize: 12.5, color: MUTED, isTextBox: true }
  );

  const shots = [
    [SHOT("mobile-01-dashboard.png"), "1. Dashboard — dados de /reports/summary, card de IA no topo"],
    [SHOT("mobile-02-apresentacao.png"), "2. Apresentação — botão gerar insights"],
    [SHOT("mobile-05-ultimo-salvo.png"), "3. Reabrir mostra o último salvo (cache do servidor)"],
  ];
  // Screenshots 1080x2400 (proporção 0.45); altura limitada pelo espaço vertical disponível (não pela largura).
  const ih = 4.1, iw = ih * (1080 / 2400), gap = 0.35;
  const totalW = shots.length * iw + (shots.length - 1) * gap;
  const x0 = 0.5, y0 = 2.5;
  shots.forEach((sh, i) => {
    const x = x0 + i * (iw + gap);
    s.addImage({ path: sh[0], x, y: y0, w: iw, h: ih });
    s.addText(sh[1], { x: x - 0.1, y: y0 + ih + 0.08, w: iw + 0.2, h: 0.55, align: "center", fontFace: FONT_BODY, fontSize: 10, color: INK, isTextBox: true });
  });

  const factsX = x0 + totalW + 0.5;
  s.addText(
    [
      { text: "Sessão: ", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "tokens JWT cifrados (Keystore) · refresh automático em 401", options: { color: INK, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Dados: ", options: { bold: true, color: NAVY, breakLine: true } },
      { text: '"Tempo real" por polling de 15s + invalidação imediata após cada escrita', options: { color: INK, breakLine: false } },
    ],
    { x: factsX, y: 2.5, w: 12.83 - factsX, h: 4.1, fontFace: FONT_BODY, fontSize: 11, isTextBox: true, paraSpaceAfter: 4 }
  );
}

// ── 12) Qualidade ────────────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Qualidade");
  title(s, "Testado em cada camada");

  s.addChart(
    pres.ChartType.bar,
    [{
      name: "Testes",
      labels: ["Domain", "Application", "Infrastructure", "Api", "Migrador"],
      values: [117, 308, 169, 519, 45],
    }],
    {
      x: 0.5, y: 2.0, w: 6.3, h: 4.7,
      barDir: "col",
      showTitle: true, title: "Testes automatizados por camada (backend)", titleFontSize: 12, titleColor: INK,
      showValue: true, dataLabelPosition: "outEnd", dataLabelFontSize: 10, dataLabelColor: INK,
      chartColors: [NAVY],
      showLegend: false,
      catAxisLabelColor: MUTED, catAxisLabelFontSize: 10,
      valAxisLabelColor: MUTED, valAxisLabelFontSize: 10,
      valGridLine: { color: "E2E6F2", size: 1 },
      catGridLine: { style: "none" },
    }
  );

  const right = [
    ["1.158 testes no backend", "unitários + integração (Testcontainers e Mongo local) — os dois modos ficam verdes"],
    ["101 testes no app", "MockWebServer/Turbine — refresh de token, mapeamento de erro, repositórios, ViewModels"],
    ["Cobertura Domain/Application", "94,6% / 94,9% de linhas (meta era ≥ 80%)"],
    ["Golden tests do dashboard", "3 conjuntos calculados à mão a partir das regras do Kotlin original"],
    ["Concorrência testada de verdade", "aprovações e conclusões simultâneas, teto diário de IA sob 10 requisições paralelas"],
  ];
  let ry = 2.0;
  right.forEach((r) => {
    s.addText("✓", { x: 7.1, y: ry, w: 0.4, h: 0.5, fontFace: FONT_BODY, fontSize: 15, bold: true, color: GREEN, isTextBox: true });
    s.addText(r[0], { x: 7.5, y: ry, w: 5.3, h: 0.32, fontFace: FONT_BODY, fontSize: 12.5, bold: true, color: INK, isTextBox: true });
    s.addText(r[1], { x: 7.5, y: ry + 0.32, w: 5.3, h: 0.55, fontFace: FONT_BODY, fontSize: 10.5, color: MUTED, isTextBox: true });
    ry += 0.94;
  });
}

// ── 13) Status e próximos passos ────────────────────────────────────────────────────────────
{
  const s = pres.addSlide({ masterName: "CONTENT" });
  kicker(s, "Status");
  title(s, "O que está pronto e o que falta");

  const done = [
    "Backend: fases 0–7 (auth, orientações, ideias, projetos, relatórios, IA, hardening, matriz de autorização, migrador)",
    "App: M01–M10 + M09b (insights no topo, apresentação em stories) — Firebase Auth/Firestore removidos",
    "Docker compose local validado de ponta a ponta (Mongo + API + seed)",
  ];
  const pending = [
    "Deploy público da API (B24/OP-5) — fallback documentado: docker compose + IP da LAN",
    "APK de release (M11) — depende da URL HTTPS do deploy",
    "Validação do migrador contra um Firestore real/emulador (hoje só testado com dados fictícios)",
  ];

  s.addShape(pres.ShapeType.roundRect, { x: 0.5, y: 2.0, w: 5.9, h: 4.3, rectRadius: 0.08, fill: { color: "EAF7EE" }, line: { color: GREEN, width: 1 } });
  s.addText("Pronto", { x: 0.75, y: 2.15, w: 5, h: 0.4, fontFace: FONT_HEAD, fontSize: 16, bold: true, color: GREEN, isTextBox: true });
  s.addText(done.map((t, i) => ({ text: "•  " + t, options: { breakLine: i < done.length - 1 } })), {
    x: 0.75, y: 2.65, w: 5.4, h: 3.5, fontFace: FONT_BODY, fontSize: 12, color: INK, isTextBox: true, paraSpaceAfter: 14,
  });

  s.addShape(pres.ShapeType.roundRect, { x: 6.9, y: 2.0, w: 5.9, h: 4.3, rectRadius: 0.08, fill: { color: "FDF3E8" }, line: { color: GOLD, width: 1 } });
  s.addText("Pendente", { x: 7.15, y: 2.15, w: 5, h: 0.4, fontFace: FONT_HEAD, fontSize: 16, bold: true, color: "B8720A", isTextBox: true });
  s.addText(pending.map((t, i) => ({ text: "•  " + t, options: { breakLine: i < pending.length - 1 } })), {
    x: 7.15, y: 2.65, w: 5.4, h: 3.5, fontFace: FONT_BODY, fontSize: 12, color: INK, isTextBox: true, paraSpaceAfter: 14,
  });
}

// ── 14) Encerramento ─────────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: NAVY };
  s.addShape(pres.ShapeType.ellipse, { x: -2.5, y: -2.5, w: 6, h: 6, fill: { color: NAVY_DARK }, line: { type: "none" } });
  s.addText("Obrigado.", { x: 0.9, y: 2.7, w: 8, h: 1.1, fontFace: FONT_HEAD, fontSize: 46, bold: true, color: WHITE, isTextBox: true });
  s.addText("INOVAGAB — Grupo Águia Branca · Sprint 2 · Challenge FIAP 2026", {
    x: 0.95, y: 3.75, w: 10, h: 0.5, fontFace: FONT_BODY, fontSize: 15, color: ICE, isTextBox: true,
  });
  s.addText(
    [
      { text: "Repositório: ", options: { color: GOLD, bold: true, breakLine: false } },
      { text: "athenabriana/aguia-branca", options: { color: WHITE, breakLine: true } },
      { text: "Documentação: ", options: { color: GOLD, bold: true, breakLine: false } },
      { text: ".specs/features/sprint2/ · docs/api/ENDPOINTS.md · docs/arquitetura/", options: { color: WHITE, breakLine: false } },
    ],
    { x: 0.95, y: 4.7, w: 10.5, h: 1.0, fontFace: FONT_BODY, fontSize: 12.5, isTextBox: true, paraSpaceAfter: 6 }
  );
}

pres.writeFile({ fileName: path.join(__dirname, "Sprint2_AguiaBranca.pptx") })
  .then((f) => console.log("wrote", f))
  .catch((e) => { console.error(e); process.exit(1); });
