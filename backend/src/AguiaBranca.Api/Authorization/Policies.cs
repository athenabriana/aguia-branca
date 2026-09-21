namespace AguiaBranca.Api.Authorization;

/// <summary>Nomes das policies (design §7.4). Regras por recurso (dono, status, auto-aprovação) ficam no domínio.</summary>
public static class Policies
{
    public const string GestorOnly = nameof(GestorOnly);
    public const string LiderOnly = nameof(LiderOnly);
    public const string CanCreateIdea = nameof(CanCreateIdea);
    public const string ProjectsRead = nameof(ProjectsRead);
    public const string UsersRead = nameof(UsersRead);
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Insights = "insights";
}
