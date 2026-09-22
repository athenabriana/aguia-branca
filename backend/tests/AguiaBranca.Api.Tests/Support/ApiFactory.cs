using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace AguiaBranca.Api.Tests.Support;

public sealed class CapturingSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];
    public IReadOnlyList<LogEvent> Events { get { lock (_events) return _events.ToList(); } }
    public void Emit(LogEvent logEvent) { lock (_events) _events.Add(logEvent); }
}

/// <summary>Host de teste. Por padrão não toca o MongoDB (<c>Mongo:InitializeOnStartup=false</c>); use <see cref="WithMongo"/> quando precisar.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string Environment { get; init; } = "Testing";
    public CapturingSink Logs { get; } = new();
    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["ConnectionStrings:Mongo"] = "mongodb://localhost:27017/?directConnection=true",
        ["Jwt:Key"] = "test-key-test-key-test-key-test-key-0123456789",
        ["Mongo:InitializeOnStartup"] = "false"
    };

    public ApiFactory With(string key, string? value) { Settings[key] = value; return this; }

    public ApiFactory WithMongo(string connectionString, string database)
    {
        Settings["ConnectionStrings:Mongo"] = connectionString;
        Settings["Mongo:Database"] = database;
        Settings["Mongo:InitializeOnStartup"] = "true";
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        foreach (var (key, value) in Settings) builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogEventSink>(Logs);
            services.AddControllers().AddApplicationPart(typeof(TestProbeController).Assembly);
        });
    }
}
