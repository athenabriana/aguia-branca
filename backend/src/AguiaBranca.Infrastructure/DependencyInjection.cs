using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Infrastructure.Authentication;
using AguiaBranca.Infrastructure.Configuration;
using AguiaBranca.Infrastructure.Identity;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Persistence.Repositories;
using AguiaBranca.Infrastructure.Seed;
using AguiaBranca.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

namespace AguiaBranca.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAguiaBrancaOptions(configuration);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITimeZoneProvider, ReportTimeZoneProvider>();

        // Conexão é preguiçosa: o driver só conecta no primeiro uso.
        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(MongoClientSettings.FromConnectionString(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString)));
        services.AddSingleton(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseMongoDB(sp.GetRequiredService<IMongoClient>(), sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database)
                // Em produção há um único host/banco; o aviso só dispara com vários hosts no mesmo processo (testes).
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGuidelineRepository, GuidelineRepository>();
        services.AddScoped<IGuidelineHistoryRepository, GuidelineHistoryRepository>();
        services.AddScoped<IIdeaRepository, IdeaRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectUpdateRepository, ProjectUpdateRepository>();
        services.AddScoped<IPointEventRepository, PointEventRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IInsightCache, InsightCache>();

        AddIdentity(services);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();

        // Ordem importa: primeiro replica set + índices, depois o seed.
        services.AddHostedService<MongoInitializer>();
        services.AddHostedService<DatabaseSeeder>();

        return services;
    }

    internal static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<AppUser>(o =>
        {
            // Política mínima (R2-01.6): 8+ caracteres; sem exigir composição (senhas longas > regras de composição).
            o.Password.RequiredLength = 8;
            o.Password.RequireDigit = false;
            o.Password.RequireLowercase = false;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = false;

            o.Lockout.AllowedForNewUsers = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

            o.User.RequireUniqueEmail = true;
        }).AddUserStore<MongoUserStore>();
    }
}
