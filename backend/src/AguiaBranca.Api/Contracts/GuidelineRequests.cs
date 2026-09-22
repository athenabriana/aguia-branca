using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Api.Contracts;

// Autor, datas e id NÃO existem aqui de propósito (sem mass assignment): o servidor os define.
public sealed record GuidelineRequest(string? Title = null, string? Description = null, Pillar? Pillar = null, string? Campaign = null);

public sealed record GuidelineHistoryRequest(
    string? GuidelineId = null, Pillar? Category = null, string? Campaign = null,
    DateTime? From = null, DateTime? To = null, int Page = 1, int PageSize = 50);
