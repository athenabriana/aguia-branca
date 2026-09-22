using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Configuration;

public sealed class ReportsOptions
{
    public const string Section = "Reports";
    public string TimeZone { get; set; } = "America/Sao_Paulo";
}

public sealed class ReportsOptionsValidator : IValidateOptions<ReportsOptions>
{
    public ValidateOptionsResult Validate(string? name, ReportsOptions options)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return ValidateOptionsResult.Fail($"Reports:TimeZone '{options.TimeZone}' não é um fuso válido.");
        }
    }
}
