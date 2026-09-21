using System.Diagnostics;
using AguiaBranca.Application.Common.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Api.Http;

public sealed record ApiError(string Code, string Message, string? Field = null);

/// <summary>Fábrica única de respostas de erro <c>application/problem+json</c> (design §10).</summary>
public static class ApiProblems
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(HttpContext context, int status, string code, string? detail = null, IEnumerable<ApiError>? errors = null)
    {
        var errorList = errors?.ToArray() ?? [];
        var problem = new ProblemDetails
        {
            Type = $"urn:aguiabranca:error:{code.ToLowerInvariant().Replace('_', '-')}",
            Title = TitleFor(status),
            Status = status,
            Detail = detail ?? errorList.FirstOrDefault()?.Message,
            Instance = context.Request.Path.Value
        };
        problem.Extensions["code"] = code;
        problem.Extensions["errors"] = errorList.Length > 0 ? errorList : new[] { new ApiError(code, problem.Detail ?? problem.Title!) };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }

    public static ProblemDetails FromErrors(HttpContext context, IReadOnlyList<Error> errors)
    {
        var first = errors[0];
        var status = errors.All(e => e.Type == ErrorType.Validation) ? 400 : first.ToStatusCode();
        var code = status == 400 ? "VALIDATION_ERROR" : first.Code;
        return Create(context, status, code, first.Message, errors.Select(e => new ApiError(e.Code, e.Message, e.Field)));
    }

    public static async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? 500;
        var options = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        await context.Response.WriteAsJsonAsync(problem, options, ContentType, context.RequestAborted);
    }

    /// <summary>Corpo padronizado para respostas vazias (401/403/404/405/413/415/429...).</summary>
    public static Task WriteStatusCodeAsync(StatusCodeContext context) =>
        WriteAsync(context.HttpContext, Create(context.HttpContext, context.HttpContext.Response.StatusCode,
            CodeForStatus(context.HttpContext.Response.StatusCode)));

    public static string CodeForStatus(int status) => status switch
    {
        400 => "VALIDATION_ERROR",
        401 => "TOKEN_INVALID",
        403 => "FORBIDDEN",
        404 => "RESOURCE_NOT_FOUND",
        405 => "METHOD_NOT_ALLOWED",
        409 => "CONFLICT",
        413 => "PAYLOAD_TOO_LARGE",
        415 => "UNSUPPORTED_MEDIA_TYPE",
        429 => "RATE_LIMITED",
        502 => "AI_INVALID_RESPONSE",
        503 => "SERVICE_UNAVAILABLE",
        >= 500 => "INTERNAL_ERROR",
        _ => $"HTTP_{status}"
    };

    private static string TitleFor(int status) => status switch
    {
        400 => "Requisição inválida",
        401 => "Não autenticado",
        403 => "Acesso negado",
        404 => "Recurso não encontrado",
        405 => "Método não permitido",
        409 => "Conflito",
        413 => "Conteúdo muito grande",
        415 => "Tipo de mídia não suportado",
        422 => "Não foi possível processar a requisição",
        429 => "Muitas requisições",
        502 => "Resposta inválida de serviço externo",
        503 => "Serviço indisponível",
        _ => "Erro interno"
    };

    internal static string CurrentTraceId(HttpContext context) => Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}
