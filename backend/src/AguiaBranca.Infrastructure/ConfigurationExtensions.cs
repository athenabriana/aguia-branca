using AguiaBranca.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AguiaBranca.Infrastructure;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Registra as options tipadas com validação no startup (<c>ValidateOnStart</c>):
    /// configuração inválida faz a aplicação falhar ao subir, com mensagem clara.
    /// </summary>
    public static IServiceCollection AddAguiaBrancaOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IValidateOptions<MongoOptions>, MongoOptionsValidator>();
        services.AddSingleton<IValidateOptions<GeminiOptions>, GeminiOptionsValidator>();
        services.AddSingleton<IValidateOptions<ReportsOptions>, ReportsOptionsValidator>();
        services.AddSingleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();
        services.AddSingleton<IValidateOptions<SecurityOptions>, SecurityOptionsValidator>();
        services.AddSingleton<IValidateOptions<CorsOptions>, CorsOptionsValidator>();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.Section)).ValidateOnStart();
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.Section))
            .Configure(o => o.ConnectionString = configuration.GetConnectionString(MongoOptions.ConnectionStringName) ?? string.Empty)
            .ValidateOnStart();
        services.AddOptions<GeminiOptions>().Bind(configuration.GetSection(GeminiOptions.Section)).ValidateOnStart();
        services.AddOptions<ReportsOptions>().Bind(configuration.GetSection(ReportsOptions.Section)).ValidateOnStart();
        services.AddOptions<RateLimitingOptions>().Bind(configuration.GetSection(RateLimitingOptions.Section)).ValidateOnStart();
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.Section));
        services.AddOptions<CorsOptions>().Bind(configuration.GetSection(CorsOptions.Section)).ValidateOnStart();
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.Section)).ValidateOnStart();

        return services;
    }
}
