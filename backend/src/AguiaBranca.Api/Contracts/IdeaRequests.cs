using AguiaBranca.Application.Features.Ideas;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Api.Contracts;

// Autor, status, ICE, datas e pontos NÃO existem aqui (sem mass assignment): o servidor os define.
public sealed record IdeaRequest(string? Title = null, string? Description = null, string? Category = null, Division? Division = null, string? GuidelineId = null);

public sealed record IdeaListRequest(
    IdeaScope? Scope = null, IdeaStatus? Status = null, string? GuidelineId = null, Division? Division = null,
    int Page = 1, int PageSize = 50);

public sealed record IceRequest(int? Impact = null, int? Confidence = null, int? Ease = null);
public sealed record RejectRequest(string? Comment = null);
