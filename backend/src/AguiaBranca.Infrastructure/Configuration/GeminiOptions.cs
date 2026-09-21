using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Configuration;

public sealed class GeminiOptions
{
    public const string Section = "Gemini";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    /// <summary>Modelo do free tier disponível na conta (OP-6). Nunca fixado no código.</summary>
    public string Model { get; set; } = string.Empty;
    /// <summary>Env <c>Gemini__ApiKey</c>. Vazia = IA desabilitada (endpoint responde AI_UNAVAILABLE).</summary>
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;
    public int CacheHours { get; set; } = 6;
    public int DailyLimit { get; set; } = 100;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}

public sealed class GeminiOptionsValidator : IValidateOptions<GeminiOptions>
{
    public ValidateOptionsResult Validate(string? name, GeminiOptions options)
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            errors.Add("Gemini:BaseUrl deve ser uma URL https absoluta.");

        // A chave é opcional (IA desligada), mas se houver chave o modelo precisa ser informado.
        if (!string.IsNullOrWhiteSpace(options.ApiKey) && string.IsNullOrWhiteSpace(options.Model))
            errors.Add("Gemini:Model é obrigatório quando Gemini:ApiKey está configurada.");

        if (options.TimeoutSeconds is < 1 or > 120) errors.Add("Gemini:TimeoutSeconds deve estar entre 1 e 120.");
        if (options.CacheHours is < 0 or > 168) errors.Add("Gemini:CacheHours deve estar entre 0 e 168.");
        if (options.DailyLimit < 1) errors.Add("Gemini:DailyLimit deve ser >= 1.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
