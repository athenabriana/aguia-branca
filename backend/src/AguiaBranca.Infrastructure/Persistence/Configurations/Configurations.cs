using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.EntityFrameworkCore.Extensions;

namespace AguiaBranca.Infrastructure.Persistence.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToCollection(Collections.Users);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.Role).HasConversion<string>();
        b.Property(x => x.Division).HasConversion<string>();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToCollection(Collections.RefreshTokens);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.UserId).AsObjectId();
        b.Property(x => x.FamilyId).AsObjectId();
    }
}

internal sealed class GuidelineConfiguration : IEntityTypeConfiguration<Guideline>
{
    public void Configure(EntityTypeBuilder<Guideline> b)
    {
        b.ToCollection(Collections.Guidelines);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.AuthorId).AsObjectId();
        b.Property(x => x.Pillar).HasConversion<string>();
    }
}

internal sealed class GuidelineHistoryEntryConfiguration : IEntityTypeConfiguration<GuidelineHistoryEntry>
{
    public void Configure(EntityTypeBuilder<GuidelineHistoryEntry> b)
    {
        b.ToCollection(Collections.GuidelineHistory);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.GuidelineId).AsObjectId();
        b.Property(x => x.ChangedById).AsObjectId();
        b.Property(x => x.Action).HasConversion<string>();
        b.Property(x => x.Category).HasConversion<string>();
        var snapshot = b.OwnsOne(x => x.Snapshot);
        snapshot.HasElementName("snapshot");
        snapshot.Property(s => s.Pillar).HasConversion<string>();
        b.Navigation(x => x.Snapshot).IsRequired();
    }
}

internal sealed class IdeaConfiguration : IEntityTypeConfiguration<Idea>
{
    public void Configure(EntityTypeBuilder<Idea> b)
    {
        b.ToCollection(Collections.Ideas);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.GuidelineId).AsNullableObjectId();
        b.Property(x => x.AuthorId).AsObjectId();
        b.Property(x => x.ReviewerId).AsNullableObjectId();
        b.Property(x => x.Division).HasConversion<string>();
        b.Property(x => x.Status).HasConversion<string>();
        b.OwnsOne(x => x.Ice).HasElementName("ice");
    }
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToCollection(Collections.Projects);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.GuidelineId).AsNullableObjectId();
        b.Property(x => x.CreatorManagerId).AsObjectId();
        b.Property(x => x.OriginatingIdeaId).AsNullableObjectId();
        b.Property(x => x.ReporterId).AsNullableObjectId();
        b.Property(x => x.ResponsibleId).AsNullableObjectId();
        b.Property(x => x.Stage).HasConversion<string>();
        b.Property(x => x.Division).HasConversion<string>();
        b.Property(x => x.Version).IsConcurrencyToken();
    }
}

internal sealed class ProjectUpdateConfiguration : IEntityTypeConfiguration<ProjectUpdate>
{
    public void Configure(EntityTypeBuilder<ProjectUpdate> b)
    {
        b.ToCollection(Collections.ProjectUpdates);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.ProjectId).AsObjectId();
        b.Property(x => x.AuthorId).AsObjectId();
        var changes = b.OwnsMany(x => x.Changes);
        changes.HasElementName("changes");
        changes.Property(c => c.Kind).HasConversion<string>();
    }
}

internal sealed class PointEventConfiguration : IEntityTypeConfiguration<PointEvent>
{
    public void Configure(EntityTypeBuilder<PointEvent> b)
    {
        b.ToCollection(Collections.PointEvents);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.UserId).AsObjectId();
        b.Property(x => x.RefId).AsNullableObjectId();
        b.Property(x => x.Reason).HasConversion<string>();
    }
}

internal sealed class InsightCacheEntryConfiguration : IEntityTypeConfiguration<InsightCacheEntry>
{
    public void Configure(EntityTypeBuilder<InsightCacheEntry> b)
    {
        b.ToCollection(Collections.AiInsights);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).AsDocumentId();
        b.Property(x => x.UserId).AsObjectId();
    }
}
