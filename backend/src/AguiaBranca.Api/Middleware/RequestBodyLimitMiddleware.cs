using AguiaBranca.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Api.Middleware;

/// <summary>
/// Limite de corpo (<c>Security:MaxRequestBodyBytes</c>, padrão 1 MB) → 413. Rejeita cedo pelo <c>Content-Length</c> e
/// arma o limite do servidor para corpos sem tamanho declarado (chunked). O Kestrel também recebe o mesmo teto.
/// </summary>
public sealed class RequestBodyLimitMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
{
    private readonly long _max = options.Value.MaxRequestBodyBytes;

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > _max)
            throw new BadHttpRequestException("Corpo da requisição acima do limite.", StatusCodes.Status413PayloadTooLarge);

        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = _max;

        return next(context);
    }
}
