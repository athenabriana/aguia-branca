namespace AguiaBranca.Domain.Rules;

/// <summary>Regras de pontuação (R-06 / R2-05.1).</summary>
public static class PointsRules
{
    public const int IdeaCreated = 10;
    public const int StrategicLinkBonus = 5;
    public const int IdeaApproved = 50;
    public const int IdeaImplemented = 200;

    /// <summary>+10 base, +5 se a ideia está vinculada a uma orientação.</summary>
    public static int ForCreation(bool hasStrategicLink) =>
        IdeaCreated + (hasStrategicLink ? StrategicLinkBonus : 0);

    /// <summary>Estorno na exclusão de ideia SUBMETIDA (valor negativo).</summary>
    public static int ForDeletion(bool hasStrategicLink) => -ForCreation(hasStrategicLink);
}
