using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Api.Contracts;

/// <summary>Filtros do dashboard (query string). Período padrão: <c>ALL</c>.</summary>
public sealed record ReportFilterRequest(Period? Period = null, Division? Division = null);

/// <summary>Corpo de <c>POST /reports/insights</c>. Tudo opcional: <c>{}</c> gera para o período <c>ALL</c>, todas as divisões.</summary>
public sealed record InsightsRequest(Period? Period = null, Division? Division = null, string? GuidelineId = null, bool Refresh = false);
