using AguiaBranca.Application.Common.Results;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Application.Common.Validation;

public interface IValidationService
{
    /// <summary>Valida com o <c>IValidator&lt;T&gt;</c> registrado (se houver). Lista vazia = válido.</summary>
    Task<IReadOnlyList<Error>> ValidateAsync<T>(T instance, CancellationToken ct);
}

internal sealed class ValidationService(IServiceProvider services) : IValidationService
{
    public async Task<IReadOnlyList<Error>> ValidateAsync<T>(T instance, CancellationToken ct)
    {
        var validator = services.GetService<IValidator<T>>();
        if (validator is null) return [];

        var result = await validator.ValidateAsync(instance, ct);
        return result.IsValid
            ? []
            : result.Errors
                .Select(f => Error.Validation(f.ErrorMessage, ToCamelPath(f.PropertyName)))
                .ToArray();
    }

    /// <summary>"Ice.Impact" → "ice.impact" (padrão JSON da API).</summary>
    private static string? ToCamelPath(string? path) =>
        string.IsNullOrEmpty(path)
            ? null
            : string.Join('.', path.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
}
