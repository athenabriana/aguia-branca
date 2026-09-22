using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Users.Ranking;

/// <summary>
/// Top do <b>mês corrente</b> (fuso configurado), somente operadores, pela soma dos eventos de pontos do mês (R2-05.4).
/// Desempate: pontos totais e nome. Quem não pontuou no mês não aparece.
/// </summary>
public sealed class RankingHandler(
    IPointEventRepository events, IUserRepository users, IClock clock, ITimeZoneProvider timeZone)
    : IHandler<RankingQuery, IReadOnlyList<RankingEntryResponse>>
{
    public const int MaxLimit = 50;

    public async Task<Result<IReadOnlyList<RankingEntryResponse>>> HandleAsync(RankingQuery request, CancellationToken ct)
    {
        if (request.Limit is < 1 or > MaxLimit)
            return Error.Validation($"limit deve estar entre 1 e {MaxLimit}.", "limit");

        var (from, to) = MonthWindow.Containing(clock.UtcNow, timeZone.ReportTimeZone);
        var monthEvents = await events.ListBetweenAsync(from, to, ct);
        var operators = (await users.ListAsync(Role.OPERADOR, ct)).ToDictionary(u => u.Id);

        var sums = monthEvents
            .Where(e => operators.ContainsKey(e.UserId))
            .GroupBy(e => e.UserId)
            .Select(g => (User: operators[g.Key], Points: Math.Max(0, g.Sum(e => e.Delta))))
            .Where(x => x.Points > 0)
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.User.Points)
            .ThenBy(x => x.User.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(request.Limit)
            .Select(x => new RankingEntryResponse(x.User.Id, x.User.Name, x.Points))
            .ToList();

        return Result.Ok<IReadOnlyList<RankingEntryResponse>>(sums);
    }
}

/// <summary>Janela [início do mês, início do próximo mês) no fuso informado, convertida para UTC.</summary>
public static class MonthWindow
{
    public static (DateTime FromUtc, DateTime ToUtc) Containing(DateTime utcNow, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var startLocal = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddMonths(1);
        return (TimeZoneInfo.ConvertTimeToUtc(startLocal, zone), TimeZoneInfo.ConvertTimeToUtc(endLocal, zone));
    }
}
