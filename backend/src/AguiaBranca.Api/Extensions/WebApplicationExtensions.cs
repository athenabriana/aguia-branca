using System.Text.Json;
using AguiaBranca.Api.Http;
using AguiaBranca.Api.Middleware;
using AguiaBranca.Infrastructure.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

namespace AguiaBranca.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseAguiaBrancaPipeline(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<SecurityOptions>>().Value.ForwardedHeaders) app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment()) app.UseHsts(); // só emite o header em requisições HTTPS (direto ou via X-Forwarded-Proto)

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
            ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
            : ctx.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information);
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestBodyLimitMiddleware>();
        app.UseStatusCodePages(ApiProblems.WriteStatusCodeAsync);

        if (ServiceCollectionExtensions.IsSwaggerEnabled(app.Configuration, app.Environment))
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "INOVAGAB API v1"));
        }

        app.UseRouting();
        app.UseCors(); // antes da autenticação: o preflight (OPTIONS) não carrega credenciais
        app.UseAuthentication();
        app.UseAuthorization();
        // Depois da autenticação: o limite dos insights é por usuário (claim "sub") e só conta requisições já autorizadas.
        // Depois do routing: usa os metadados do endpoint ([EnableRateLimiting]).
        app.UseRateLimiter();
        app.UseMiddleware<RequestContextLoggingMiddleware>();
        return app;
    }

    public static WebApplication MapAguiaBrancaEndpoints(this WebApplication app)
    {
        app.MapControllers();

        app.MapGet("/", () => Results.Ok(new { name = "INOVAGAB — Águia Branca API", version = "v1" })).ExcludeFromDescription().AllowAnonymous();

        // Liveness: só o processo. Readiness: dependências (Mongo). /health: tudo.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteHealthAsync }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready"), ResponseWriter = WriteHealthAsync }).AllowAnonymous();
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthAsync }).AllowAnonymous();
        return app;
    }

    private static Task WriteHealthAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        // Nunca inclui a exceção/detalhes da dependência (podem conter host/credencial).
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), durationMs = (int)e.Value.Duration.TotalMilliseconds })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
