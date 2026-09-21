using AguiaBranca.Api.Extensions;
using AguiaBranca.Application;
using AguiaBranca.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// preserveStaticLogger: cada host tem seu próprio logger (não sobrescreve o Log.Logger global) — necessário
// para hosts paralelos nos testes de integração; em produção não altera o comportamento.
builder.Host.UseSerilog(preserveStaticLogger: true, configureLogger: (context, services, logger) =>
{
    logger
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();

    // Sinks adicionais registrados no DI (usado pelos testes para capturar eventos).
    foreach (var sink in services.GetServices<Serilog.Core.ILogEventSink>()) logger.WriteTo.Sink(sink);

    // Produção: JSON compacto (uma linha por evento). Desenvolvimento: texto legível.
    if (context.HostingEnvironment.IsProduction()) logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    else logger.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseAguiaBrancaPipeline();
app.MapAguiaBrancaEndpoints();

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes de integração.
public partial class Program;
