using System.Text.Json;
using AguiaBranca.Api.Http;
using AguiaBranca.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

namespace AguiaBranca.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseAguiaBrancaPipeline(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
            ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
            : ctx.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information);
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseStatusCodePages(ApiProblems.WriteStatusCodeAsync);

        if (ServiceCollectionExtensions.IsSwaggerEnabled(app.Configuration, app.Environment))
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "INOVAGAB API v1"));
        }

        app.UseRouting();
        return app;
    }

    public static WebApplication MapAguiaBrancaEndpoints(this WebApplication app)
    {
        app.MapControllers();

        app.MapGet("/", () => Results.Ok(new { name = "INOVAGAB — Águia Branca API", version = "v1" })).ExcludeFromDescription();

        // Liveness: só o processo. Readiness: dependências (Mongo). /health: tudo.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteHealthAsync });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready"), ResponseWriter = WriteHealthAsync });
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthAsync });
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
