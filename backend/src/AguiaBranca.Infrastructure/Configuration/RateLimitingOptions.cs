using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Configuration;

public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    /// <summary>Tentativas por IP na janela para <c>/auth/*</c> (R2-01.9: 10/min).</summary>
    public int AuthPermitLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;
    /// <summary>Gerações de insights por usuário na janela (R2-07.6: 6/min).</summary>
    public int InsightsPermitLimit { get; set; } = 6;
    public int InsightsWindowSeconds { get; set; } = 60;
}

public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions o)
    {
        var errors = new List<string>();
        if (o.AuthPermitLimit < 1) errors.Add("RateLimiting:AuthPermitLimit deve ser >= 1.");
        if (o.AuthWindowSeconds < 1) errors.Add("RateLimiting:AuthWindowSeconds deve ser >= 1.");
        if (o.InsightsPermitLimit < 1) errors.Add("RateLimiting:InsightsPermitLimit deve ser >= 1.");
        if (o.InsightsWindowSeconds < 1) errors.Add("RateLimiting:InsightsWindowSeconds deve ser >= 1.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
