using System.Text;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public const int MinKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 7;
}

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer)) errors.Add("Jwt:Issuer é obrigatório.");
        if (string.IsNullOrWhiteSpace(options.Audience)) errors.Add("Jwt:Audience é obrigatório.");

        if (string.IsNullOrWhiteSpace(options.Key))
            errors.Add("Jwt:Key é obrigatório (defina Jwt__Key por variável de ambiente ou user-secrets).");
        else if (Encoding.UTF8.GetByteCount(options.Key) < JwtOptions.MinKeyBytes)
            errors.Add($"Jwt:Key deve ter ao menos {JwtOptions.MinKeyBytes} bytes (256 bits).");

        if (options.AccessMinutes is < 1 or > 1440) errors.Add("Jwt:AccessMinutes deve estar entre 1 e 1440.");
        if (options.RefreshDays is < 1 or > 90) errors.Add("Jwt:RefreshDays deve estar entre 1 e 90.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
