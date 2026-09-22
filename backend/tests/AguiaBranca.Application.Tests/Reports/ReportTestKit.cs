using AguiaBranca.Application.Features.Reports;
using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Application.Tests.Reports;

/// <summary>Construtores de dados para os testes de relatório (as entidades só nascem por fábricas do domínio).</summary>
internal static class ReportTestKit
{
    public static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static readonly DateTime Now = new(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc); // 12:00 em São Paulo
    private const string Reviewer = "665f00000000000000000001";

    public static DateTime Utc(int y, int m, int d, int h = 12, int min = 0, int s = 0) => new(y, m, d, h, min, s, DateTimeKind.Utc);

    public static Guideline Guideline(string title) =>
        Domain.Entities.Guideline.Create(title, "Descrição", Pillar.DIRECIONAMENTO, null, EntityId.New(), "Autor", Now);

    public static Idea Idea(
        Division division, IdeaStatus status, DateTime createdAt, string? guidelineId = null, string title = "Ideia de teste")
    {
        var idea = Domain.Entities.Idea.Create(title, "Descrição", "tecnologia", division, guidelineId, EntityId.New(), "Autor", createdAt);
        switch (status)
        {
            case IdeaStatus.SUBMETIDA: break;
            case IdeaStatus.EM_ANALISE: idea.SaveIce(new Ice(5, 5, 5), Reviewer, createdAt); break;
            case IdeaStatus.APROVADA: idea.Approve(Reviewer, createdAt); break;
            case IdeaStatus.IMPLEMENTADA: idea.Approve(Reviewer, createdAt); idea.MarkImplemented(createdAt); break;
            case IdeaStatus.REJEITADA: idea.Reject(Reviewer, "não agora", createdAt); break;
        }
        return idea;
    }

    public static Project Project(
        string title = "Projeto", ProjectStage stage = ProjectStage.EM_EXECUCAO, Division division = Division.LOGISTICA,
        string? guidelineId = null, decimal investment = 0m, decimal financialReturn = 0m, decimal productivityGain = 0m,
        decimal costReduction = 0m, DateTime? targetDate = null, DateTime? updatedAt = null) =>
        Domain.Entities.Project.Create(
            new ProjectData(title, "Desc", stage, "Status", investment, targetDate, financialReturn, productivityGain,
                costReduction, division, guidelineId),
            Reviewer, "Gestor", updatedAt ?? Now);

    public static ReportSummary Compute(
        IReadOnlyCollection<Idea>? ideas = null, IReadOnlyCollection<Project>? projects = null,
        IReadOnlyCollection<Guideline>? guidelines = null, Period period = Period.ALL, Division? division = null, DateTime? now = null) =>
        ReportCalculator.Compute(ideas ?? [], projects ?? [], guidelines ?? [], new ReportFilters(period, division), now ?? Now, SaoPaulo);
}
