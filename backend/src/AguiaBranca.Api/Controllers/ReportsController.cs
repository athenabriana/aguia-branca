using AguiaBranca.Api.Authorization;
using AguiaBranca.Api.Contracts;
using AguiaBranca.Application.Features.Reports;
using AguiaBranca.Application.Features.Reports.Guidelines;
using AguiaBranca.Application.Features.Reports.Insights;
using AguiaBranca.Application.Features.Reports.Projects;
using AguiaBranca.Application.Features.Reports.Summary;
using AguiaBranca.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AguiaBranca.Api.Controllers;

/// <summary>Relatórios do dashboard e insights de IA. Somente líder.</summary>
[Authorize(Policy = Policies.LiderOnly)]
public sealed class ReportsController : ApiControllerBase
{
    /// <summary>
    /// Resumo geral: funil, KPIs (ROI, lucro, investimento, projetos ativos/atrasados, produtividade, redução de custo),
    /// sparkline de 6 meses, impacto por orientação e projetos por ROI. <c>period</c> e <c>division</c> filtram tudo,
    /// exceto o período na sparkline.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType<ReportSummary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] ReportFilterRequest filter, [FromServices] GetReportSummaryHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new GetReportSummaryQuery(filter.Period ?? Period.ALL, filter.Division), ct));

    /// <summary>Retorno por estratégia: impacto de cada orientação (com projeto primeiro, ROI decrescente).</summary>
    [HttpGet("guidelines")]
    [ProducesResponseType<GuidelineReportList>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Guidelines([FromQuery] ReportFilterRequest filter, [FromServices] ListGuidelineReportsHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new ListGuidelineReportsQuery(filter.Period ?? Period.ALL, filter.Division), ct));

    /// <summary>Retorno de uma orientação: ideias, projetos, investimento, retorno, lucro e ROI.</summary>
    [HttpGet("guidelines/{id:objectid}")]
    [ProducesResponseType<GuidelineReportDetail>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Guideline(
        string id, [FromQuery] ReportFilterRequest filter, [FromServices] GetGuidelineReportHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new GetGuidelineReportQuery(id, filter.Period ?? Period.ALL, filter.Division), ct));

    /// <summary>Retorno de um projeto: investimento, retorno, lucro, ROI, produtividade, redução de custo e prazo.</summary>
    [HttpGet("projects/{id:objectid}")]
    [ProducesResponseType<ProjectReport>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Project(string id, [FromServices] GetProjectReportHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new GetProjectReportQuery(id), ct));

    /// <summary>
    /// Insights gerados por IA (Google Gemini) sobre o mesmo resumo do dashboard, para a liderança: resumo, destaques,
    /// riscos e recomendações priorizadas. Só dados agregados e títulos truncados vão ao modelo (sem nomes/e-mails/ids de
    /// usuário). Resultado em cache por 6 h (<c>refresh=true</c> força nova geração). Limites: 6 requisições/min por usuário
    /// e teto diário global (429). IA indisponível → 503 <c>AI_UNAVAILABLE</c>; resposta inválida → 502 <c>AI_INVALID_RESPONSE</c>.
    /// </summary>
    [HttpPost("insights")]
    [EnableRateLimiting(RateLimitPolicies.Insights)]
    [ProducesResponseType<InsightResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Insights([FromBody] InsightsRequest? request, [FromServices] GenerateInsightsHandler handler, CancellationToken ct)
    {
        request ??= new InsightsRequest();
        return FromResult(await handler.HandleAsync(
            new GenerateInsightsCommand(request.Period ?? Period.ALL, request.Division, request.GuidelineId, request.Refresh), ct));
    }
}
