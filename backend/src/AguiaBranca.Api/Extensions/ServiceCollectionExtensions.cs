using System.Text.Json;
using System.Text.Json.Serialization;
using AguiaBranca.Api.Http;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Infrastructure.Health;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

namespace AguiaBranca.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.Configure<RouteOptions>(o =>
        {
            o.ConstraintMap[ObjectIdRouteConstraint.Name] = typeof(ObjectIdRouteConstraint);
            o.LowercaseUrls = true;
        });

        services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));

        services.AddControllers()
            .AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions))
            .ConfigureApiBehaviorOptions(o =>
            {
                // Respostas 4xx sem corpo (404/415/406...) passam pelo StatusCodePages, com o formato de erro da API.
                o.SuppressMapClientErrors = true;

                // Falhas de binding/validação de modelo → mesmo formato de erro do restante da API.
                o.InvalidModelStateResponseFactory = ctx =>
                {
                    var errors = ctx.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .SelectMany(e => e.Value!.Errors.Select(err => new ApiError(
                            "VALIDATION_ERROR",
                            string.IsNullOrWhiteSpace(err.ErrorMessage) || err.ErrorMessage.StartsWith("The JSON", StringComparison.Ordinal)
                                ? "Valor inválido."
                                : err.ErrorMessage,
                            NormalizeField(e.Key))))
                        .ToArray();
                    var problem = ApiProblems.Create(ctx.HttpContext, 400, "VALIDATION_ERROR", errors.FirstOrDefault()?.Message, errors);
                    return new ObjectResult(problem) { StatusCode = 400, ContentTypes = { ApiProblems.ContentType } };
                };
            });

        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);

        if (IsSwaggerEnabled(configuration, environment))
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "INOVAGAB — Águia Branca API",
                    Version = "v1",
                    Description = "Plataforma de inovação corporativa: orientações estratégicas, ideias, projetos, relatórios e insights de IA."
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Informe o accessToken JWT obtido em /api/v1/auth/login."
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
                });
            });
        }

        return services;
    }

    /// <summary><c>Swagger:Enabled</c> explícito vence; sem ele, só em Development (política por ambiente, design §14).</summary>
    public static bool IsSwaggerEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue<bool?>("Swagger:Enabled") ?? environment.IsDevelopment();

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // Enums já são UPPER_SNAKE (SUBMETIDA, EM_ANALISE...): serializados pelo nome, sem tradução.
        options.Converters.Add(new JsonStringEnumConverter());
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    }

    private static string? NormalizeField(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        key = key.TrimStart('$', '.');
        return string.Join('.', key.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
    }
}
