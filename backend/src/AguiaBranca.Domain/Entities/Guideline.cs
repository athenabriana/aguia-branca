using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Domain.Entities;

public sealed class Guideline
{
    public const int TitleMin = 3, TitleMax = 120, DescriptionMax = 2000, CampaignMax = 80;

    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Pillar Pillar { get; private set; }
    public string? Campaign { get; private set; }
    public string AuthorId { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string? LegacyId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Guideline() { }

    public static Guideline Create(
        string title, string description, Pillar pillar, string? campaign,
        string authorId, string authorName, DateTime now, string? legacyId = null)
    {
        var g = new Guideline
        {
            Id = EntityId.New(),
            AuthorId = authorId,
            AuthorName = authorName,
            LegacyId = legacyId,
            CreatedAt = now
        };
        g.Apply(title, description, pillar, campaign, now);
        return g;
    }

    public void Update(string title, string description, Pillar pillar, string? campaign, DateTime now) =>
        Apply(title, description, pillar, campaign, now);

    public GuidelineSnapshot ToSnapshot() => new(Title, Description, Pillar, Campaign);

    private void Apply(string title, string description, Pillar pillar, string? campaign, DateTime now)
    {
        title = (title ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();
        campaign = string.IsNullOrWhiteSpace(campaign) ? null : campaign.Trim();

        if (title.Length is < TitleMin or > TitleMax)
            throw DomainException.Validation(nameof(title), $"Título deve ter de {TitleMin} a {TitleMax} caracteres.");
        if (description.Length > DescriptionMax)
            throw DomainException.Validation(nameof(description), $"Descrição deve ter no máximo {DescriptionMax} caracteres.");
        if (campaign is { Length: > CampaignMax })
            throw DomainException.Validation(nameof(campaign), $"Campanha deve ter no máximo {CampaignMax} caracteres.");

        Title = title;
        Description = description;
        Pillar = pillar;
        Campaign = campaign;
        UpdatedAt = now;
    }
}

/// <summary>Foto imutável dos campos de uma orientação (usada no histórico).</summary>
public sealed class GuidelineSnapshot
{
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Pillar Pillar { get; private set; }
    public string? Campaign { get; private set; }

    private GuidelineSnapshot() { }

    public GuidelineSnapshot(string title, string description, Pillar pillar, string? campaign)
    {
        Title = title;
        Description = description;
        Pillar = pillar;
        Campaign = campaign;
    }
}
