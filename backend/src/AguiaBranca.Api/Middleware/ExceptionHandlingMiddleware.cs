using AguiaBranca.Api.Http;
using AguiaBranca.Application.Common.Exceptions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AguiaBranca.Api.Middleware;

/// <summary>Converte exceções em <c>ProblemDetails</c> (design §10). Nunca expõe stack trace nem detalhes internos.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente desistiu da requisição: nada a responder.
            logger.LogDebug("Requisição cancelada pelo cliente: {Path}", context.Request.Path);
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(ex, "Exceção após o início da resposta; a conexão será abortada.");
                throw;
            }

            var problem = Map(context, ex);
            if (problem.Status >= 500)
                logger.LogError(ex, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                logger.LogInformation("Requisição rejeitada ({Status} {Code}): {Message}", problem.Status, problem.Extensions["code"], ex.Message);

            context.Response.Clear();
            await ApiProblems.WriteAsync(context, problem);
        }
    }

    internal static ProblemDetails Map(HttpContext context, Exception ex) => ex switch
    {
        ValidationException v => ApiProblems.FromErrors(context, v.Errors
            .Select(f => Error.Validation(f.ErrorMessage, ToCamel(f.PropertyName))).DefaultIfEmpty(Error.Validation(v.Message)).ToArray()),
        DomainException d => ApiProblems.FromErrors(context, [d.ToError()]),
        DuplicateKeyException => ApiProblems.Create(context, 409, "CONFLICT", "Já existe um registro com estes dados."),
        ConcurrencyConflictException c => ApiProblems.Create(context, 409, DomainErrorCodes.ConcurrencyConflict, c.Message),
        BadHttpRequestException b => ApiProblems.Create(context, b.StatusCode, ApiProblems.CodeForStatus(b.StatusCode),
            b.StatusCode == 413 ? "O corpo da requisição excede o limite permitido." : "Requisição malformada."),
        UnauthorizedAccessException => ApiProblems.Create(context, 403, "FORBIDDEN", "Você não tem permissão para esta ação."),
        _ => ApiProblems.Create(context, 500, "INTERNAL_ERROR", "Ocorreu um erro inesperado. Informe o traceId ao suporte.")
    };

    private static string? ToCamel(string? path) =>
        string.IsNullOrEmpty(path) ? null : string.Join('.', path.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
}
