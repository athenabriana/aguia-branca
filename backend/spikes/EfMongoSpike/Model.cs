using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

namespace EfMongoSpike;

public enum Status { Submetida, EmAnalise, Aprovada }

public class Ice
{
    public int Impact { get; set; }
    public int Confidence { get; set; }
    public int Ease { get; set; }
    public int Score => Impact * Confidence * Ease;
}

public class Change
{
    public string Field { get; set; } = "";
    public string? From { get; set; }
    public string? To { get; set; }
}

public class Idea
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string Title { get; set; } = "";
    public Status Status { get; set; } = Status.Submetida;
    public string? GuidelineId { get; set; }
    public Ice? Ice { get; set; }
    public List<Change> Changes { get; set; } = [];
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SpikeUser
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string? UserName { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? PasswordHash { get; set; }
    public string? SecurityStamp { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public string Role { get; set; } = "OPERADOR";
    public int Points { get; set; }
}

public class SpikeContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Idea> Ideas => Set<Idea>();
    public DbSet<SpikeUser> Users => Set<SpikeUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Idea>(e =>
        {
            e.ToCollection("ideas");
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.OwnsOne(x => x.Ice).HasElementName("ice");
            e.OwnsMany(x => x.Changes).HasElementName("changes");
        });
        b.Entity<SpikeUser>().ToCollection("users");
    }
}

public class CamelContext(DbContextOptions options) : SpikeContext(options)
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        foreach (var et in b.Model.GetEntityTypes())
            foreach (var p in et.GetProperties())
                if (p.Name != "Id" && !p.IsPrimaryKey())
                    p.SetElementName(char.ToLowerInvariant(p.Name[0]) + p.Name[1..]);

    }
}

/// <summary>Entidade "pura" (sem atributos do driver) — como será o Domain.</summary>
public class PlainIdea
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string Title { get; set; } = "";
    public Status Status { get; set; }
    public string? GuidelineId { get; set; }
}

public class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options)
{
    public DbSet<PlainIdea> Ideas => Set<PlainIdea>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PlainIdea>(e =>
        {
            e.ToCollection("plain_ideas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion(v => ObjectId.Parse(v), v => v.ToString()).HasElementName("_id");
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.GuidelineId)
                .HasConversion(v => v == null ? (ObjectId?)null : ObjectId.Parse(v), v => v == null ? null : v.ToString());
        });
    }
}
