using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Infrastructure.Configuration;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Persistence.Repositories;
using AguiaBranca.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
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

        // Conexão é preguiçosa: o driver só conecta no primeiro uso.
        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(MongoClientSettings.FromConnectionString(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString)));
        services.AddSingleton(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseMongoDB(sp.GetRequiredService<IMongoClient>(), sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));

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

        services.AddHostedService<MongoInitializer>();

        return services;
    }
}
