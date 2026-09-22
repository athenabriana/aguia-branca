using System.Text.RegularExpressions;
using Serilog.Context;

namespace AguiaBranca.Api.Middleware;

/// <summary>
/// Garante um identificador de rastreamento por requisição (<c>X-Correlation-ID</c>): aceita o enviado pelo cliente
/// (se for seguro), senão gera. Vira o <c>TraceIdentifier</c> (usado como <c>traceId</c> nos erros) e é propagado aos logs.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var id = incoming is not null && SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", id))
            await next(context);
    }

    // Só caracteres seguros (sem quebra de linha => sem log/header injection), 8–64 chars.
    [GeneratedRegex(@"^[A-Za-z0-9\-_.:]{8,64}$")]
    private static partial Regex SafeId();
}
