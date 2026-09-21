using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Configuration;

/// <summary>Endurecimento da API (B21): limite de payload, proxy reverso e HSTS.</summary>
public sealed class SecurityOptions
{
    public const string Section = "Security";

    /// <summary>Tamanho máximo do corpo de qualquer requisição (acima disso: 413). Padrão 1 MB.</summary>
    public long MaxRequestBodyBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Confia em <c>X-Forwarded-For/Proto</c> (API atrás de proxy/balanceador que termina o HTTPS: Render, Azure, Nginx).
    /// Ligue <b>somente</b> atrás de um proxy confiável: sem ele, um cliente forjaria o IP (e burlaria o rate limit).
    /// </summary>
    public bool ForwardedHeaders { get; set; }

    public int HstsMaxAgeDays { get; set; } = 365;
}

public sealed class SecurityOptionsValidator : IValidateOptions<SecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityOptions o)
    {
        var errors = new List<string>();
        if (o.MaxRequestBodyBytes is < 1_024 or > 10_485_760) errors.Add("Security:MaxRequestBodyBytes deve estar entre 1024 e 10485760.");
        if (o.HstsMaxAgeDays is < 1 or > 730) errors.Add("Security:HstsMaxAgeDays deve estar entre 1 e 730.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
