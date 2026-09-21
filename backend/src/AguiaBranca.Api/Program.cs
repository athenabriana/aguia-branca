using AguiaBranca.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAguiaBrancaOptions(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { name = "AguiaBranca.Api" }));

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes de integração.
public partial class Program;
