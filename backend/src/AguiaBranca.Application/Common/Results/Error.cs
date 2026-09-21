namespace AguiaBranca.Application.Common.Results;

public enum ErrorType { Validation, Unauthorized, Forbidden, NotFound, Conflict, Unprocessable, TooManyRequests, External, Unexpected }

/// <summary>Erro de negócio estruturado. <see cref="Code"/> é estável (contrato da API).</summary>
public sealed record Error(string Code, string Message, ErrorType Type, string? Field = null)
{
    public static Error Validation(string message, string? field = null, string code = "VALIDATION_ERROR") =>
        new(code, message, ErrorType.Validation, field);

    public static Error Unauthorized(string message, string code = "TOKEN_INVALID") => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string message, string code = "FORBIDDEN") => new(code, message, ErrorType.Forbidden);
    public static Error NotFound(string message, string code = "RESOURCE_NOT_FOUND") => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unprocessable(string code, string message, string? field = null) => new(code, message, ErrorType.Unprocessable, field);
    public static Error TooManyRequests(string message, string code = "RATE_LIMITED") => new(code, message, ErrorType.TooManyRequests);
    public static Error External(string code, string message) => new(code, message, ErrorType.External);
    public static Error Unexpected(string message = "Erro interno.", string code = "INTERNAL_ERROR") => new(code, message, ErrorType.Unexpected);
}

public static class ErrorTypeExtensions
{
    /// <summary>Status HTTP correspondente (design §10). <c>External</c> depende do código: 502 para resposta inválida, 503 para indisponível.</summary>
    public static int ToStatusCode(this Error error) => error.Type switch
    {
        ErrorType.Validation => 400,
        ErrorType.Unauthorized => 401,
        ErrorType.Forbidden => 403,
        ErrorType.NotFound => 404,
        ErrorType.Conflict => 409,
        ErrorType.Unprocessable => 422,
        ErrorType.TooManyRequests => 429,
        ErrorType.External => error.Code == "AI_INVALID_RESPONSE" ? 502 : 503,
        _ => 500
    };
}
