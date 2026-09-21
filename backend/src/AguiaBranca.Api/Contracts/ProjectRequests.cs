using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Api.Contracts;

/// <summary>
/// Corpo de criação/edição. Autor, ids de origem, versão do servidor e datas de auditoria NÃO existem aqui (sem mass assignment).
/// <c>Note</c> e <c>Version</c> só valem no PUT: a nota entra no histórico; <c>Version</c> (opcional) ativa a checagem de concorrência.
/// </summary>
public sealed record ProjectRequest(
    string? Title = null, string? Description = null, ProjectStage? Stage = null, string? StatusText = null,
    decimal? Investment = null, DateTime? TargetDate = null, decimal? FinancialReturn = null,
    decimal? ProductivityGain = null, decimal? CostReduction = null,
    Division? Division = null, string? GuidelineId = null, string? ResponsibleId = null,
    string? Note = null, int? Version = null);

public sealed record ProjectListRequest(
    ProjectStage? Stage = null, Division? Division = null, string? GuidelineId = null, int Page = 1, int PageSize = 50);
