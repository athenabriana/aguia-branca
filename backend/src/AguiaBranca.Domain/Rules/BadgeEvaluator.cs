using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Domain.Rules;

public static class Badges
{
    public const string PrimeiraIdeia = "Primeira Ideia";
    public const string Estrategista = "Estrategista";
    public const string InovadorDoMes = "Inovador do Mês";
    public const string ImpactoReal = "Impacto Real";
    public const string Visionario = "Visionário";

    public static readonly IReadOnlyList<string> All =
        [PrimeiraIdeia, Estrategista, InovadorDoMes, ImpactoReal, Visionario];
}

/// <summary>Porte de <c>BadgeEvaluator.kt</c> (R-06.4). Retorna apenas badges <b>novas</b> (que o usuário ainda não tem).</summary>
public static class BadgeEvaluator
{
    private const int IdeasForInovadorDoMes = 5;
    private const int DistinctGuidelinesForVisionario = 3;

    /// <param name="ideas">Ideias do usuário (as de outros autores são ignoradas).</param>
    /// <param name="monthZone">Fuso usado para agrupar "mês calendário" (padrão: UTC).</param>
    public static IReadOnlySet<string> Evaluate(AppUser user, IEnumerable<Idea> ideas, TimeZoneInfo? monthZone = null)
    {
        monthZone ??= TimeZoneInfo.Utc;
        var authored = ideas.Where(i => i.AuthorId == user.Id).ToList();
        var already = user.Badges.ToHashSet();
        var unlocked = new HashSet<string>();

        if (authored.Count > 0 && !already.Contains(Badges.PrimeiraIdeia))
            unlocked.Add(Badges.PrimeiraIdeia);

        if (!already.Contains(Badges.Estrategista) && authored.Any(i => i.HasStrategicLink && i.IsApprovedOrLater))
            unlocked.Add(Badges.Estrategista);

        if (!already.Contains(Badges.InovadorDoMes) &&
            authored.GroupBy(i => YearMonth(i.CreatedAt, monthZone)).Any(g => g.Count() >= IdeasForInovadorDoMes))
            unlocked.Add(Badges.InovadorDoMes);

        if (!already.Contains(Badges.ImpactoReal) && authored.Any(i => i.Status == IdeaStatus.IMPLEMENTADA))
            unlocked.Add(Badges.ImpactoReal);

        if (!already.Contains(Badges.Visionario))
        {
            var distinct = authored
                .Where(i => i.HasStrategicLink && i.IsApprovedOrLater)
                .Select(i => i.GuidelineId!)
                .Distinct()
                .Count();
            if (distinct >= DistinctGuidelinesForVisionario) unlocked.Add(Badges.Visionario);
        }

        return unlocked;
    }

    private static int YearMonth(DateTime utc, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        return local.Year * 100 + local.Month;
    }
}
