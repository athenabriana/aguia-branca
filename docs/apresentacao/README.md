# Apresentação — Sprint 2 (D03)

`Sprint2_AguiaBranca.pptx`: 14 slides — agenda, visão geral, arquitetura do backend (com o diagrama de D02),
especificação da API (D01), segurança, migração Firebase → MongoDB, a funcionalidade de IA (modelo, guardrails,
demonstração com uma resposta real do Gemini), o fluxo integrado app ↔ backend (capturas de tela reais do
emulador) e qualidade/status.

## Antes de entregar

**Preencher nome completo e RM de cada integrante no slide de capa** — hoje com os placeholders
`[ A PREENCHER PELO GRUPO ]` / `Nome completo do integrante N — RM 000000`.

## Gerar um PDF

Este ambiente não tem PowerPoint nem LibreOffice instalados, então o `.pptx` não foi convertido para `.pdf` aqui.
Depois de editar o slide de capa, exporte com o que tiver à mão:

- **PowerPoint** (Mac/Windows): Arquivo → Exportar → PDF.
- **LibreOffice**: `soffice --headless --convert-to pdf Sprint2_AguiaBranca.pptx`
- **Google Slides**: importe o `.pptx`, depois Arquivo → Fazer download → PDF.

## Regenerar o `.pptx` (opcional)

O deck é gerado por script (`gerar_deck.js`, com [`pptxgenjs`](https://www.npmjs.com/package/pptxgenjs)), a partir
do diagrama em `../arquitetura/backend-arquitetura.png` e das capturas de tela em `assets/` (tiradas manualmente no
emulador durante a validação do M09b — não são recapturadas pelo script). Útil se algum número mudar (contagem de
testes, por exemplo) antes da entrega.

```bash
npm install pptxgenjs   # não é dependência do projeto; só desta geração pontual
node docs/apresentacao/gerar_deck.js
```
