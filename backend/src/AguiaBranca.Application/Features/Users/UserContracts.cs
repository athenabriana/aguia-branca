using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Users;

/// <summary>Resumo para seleção de responsáveis. Sem e-mail (minimização de dados).</summary>
public sealed record UserSummaryResponse(string Id, string Name, Role Role, Division Division)
{
    public static UserSummaryResponse From(AppUser u) => new(u.Id, u.Name, u.Role, u.Division);
}

public sealed record RankingEntryResponse(string Id, string Name, int MonthPoints);

public sealed record ListUsersQuery(Role? Role = null);
public sealed record RankingQuery(int Limit = 5);
