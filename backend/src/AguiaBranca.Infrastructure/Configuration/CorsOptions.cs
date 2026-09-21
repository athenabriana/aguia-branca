using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Configuration;

public sealed class CorsOptions
{
    public const string Section = "Cors";
    /// <summary>Origens permitidas (ex.: <c>https://painel.exemplo.com</c>). Vazio = nenhuma origem de navegador. Sem curinga.</summary>
    public string[] Origins { get; set; } = [];
}

public sealed class CorsOptionsValidator : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        var invalid = options.Origins.Where(o =>
            !Uri.TryCreate(o, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.PathAndQuery != "/"
            || o.EndsWith('/')).ToList();

        return invalid.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Cors:Origins inválidas ({string.Join(", ", invalid)}): use origens exatas 'https://host[:porta]', sem barra final, caminho ou curinga.");
    }
}
