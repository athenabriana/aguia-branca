using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure.Configuration;

public sealed class MongoOptions
{
    public const string Section = "Mongo";
    public const string ConnectionStringName = "Mongo";

    /// <summary>Vem de <c>ConnectionStrings:Mongo</c> (env <c>ConnectionStrings__Mongo</c>).</summary>
    public string ConnectionString { get; set; } = string.Empty;
    public string Database { get; set; } = "aguiabranca";
}

public sealed class MongoOptionsValidator : IValidateOptions<MongoOptions>
{
    public ValidateOptionsResult Validate(string? name, MongoOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            errors.Add("ConnectionStrings:Mongo é obrigatório (env ConnectionStrings__Mongo).");
        else if (!options.ConnectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase)
                 && !options.ConnectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
            errors.Add("ConnectionStrings:Mongo deve começar com mongodb:// ou mongodb+srv://.");

        if (string.IsNullOrWhiteSpace(options.Database))
            errors.Add("Mongo:Database é obrigatório.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
