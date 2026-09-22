using AguiaBranca.Domain.Entities;
using AguiaBranca.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AguiaBranca.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Guideline> Guidelines => Set<Guideline>();
    public DbSet<GuidelineHistoryEntry> GuidelineHistory => Set<GuidelineHistoryEntry>();
    public DbSet<Idea> Ideas => Set<Idea>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectUpdate> ProjectUpdates => Set<ProjectUpdate>();
    public DbSet<PointEvent> PointEvents => Set<PointEvent>();
    public DbSet<InsightCacheEntry> AiInsights => Set<InsightCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.UseCamelCaseElementNames();
    }
}
