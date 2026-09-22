namespace AguiaBranca.Api.Middleware;

/// <summary>
/// Headers de segurança em todas as respostas (inclusive erros): sem sniffing de tipo, sem referer, sem iframe e CSP
/// restritiva (é uma API JSON; o Swagger UI fica de fora, pois precisa carregar scripts). Respostas de <c>/auth/*</c>
/// (tokens) nunca são armazenadas em cache. Não sobrescreve headers já definidos.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("X-Frame-Options", "DENY");

            var path = context.Request.Path;
            if (!path.StartsWithSegments("/swagger"))
                headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");

            if (path.StartsWithSegments("/api/v1/auth"))
            {
                headers["Cache-Control"] = "no-store";
                headers.TryAdd("Pragma", "no-cache");
            }
            return Task.CompletedTask;
        });

        return next(context);
    }
}
