using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Common;

namespace AguiaBranca.Application.Features.Reports.Projects;

/// <summary>Retorno de um projeto: investimento, retorno, lucro, ROI, produtividade, redução de custo e prazo.</summary>
public sealed class GetProjectReportHandler(IProjectRepository projects, IGuidelineRepository guidelines, IClock clock, ITimeZoneProvider timeZone)
    : IHandler<GetProjectReportQuery, ProjectReport>
{
    public async Task<Result<ProjectReport>> HandleAsync(GetProjectReportQuery request, CancellationToken ct)
    {
        if (!EntityId.IsValid(request.Id)) return ReportErrors.ProjectNotFound;
        var project = await projects.GetByIdAsync(request.Id, ct);
        if (project is null) return ReportErrors.ProjectNotFound;

        string? guidelineTitle = null;
        if (project.GuidelineId is not null)
        {
            var titles = await guidelines.GetTitlesAsync([project.GuidelineId], ct);
            titles.TryGetValue(project.GuidelineId, out guidelineTitle);
        }

        return ReportCalculator.ToProjectReport(project, guidelineTitle, clock.UtcNow, timeZone.ReportTimeZone);
    }
}
