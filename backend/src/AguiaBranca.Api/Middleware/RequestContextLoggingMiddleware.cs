using Serilog.Context;

namespace AguiaBranca.Api.Middleware;

/// <summary>Depois da autenticação: acrescenta <c>UserId</c> e <c>Endpoint</c> aos logs da requisição (design §11.2).</summary>
public sealed class RequestContextLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        var endpoint = context.GetEndpoint()?.DisplayName;

        using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
        using (LogContext.PushProperty("Endpoint", endpoint))
            await next(context);
    }
}
