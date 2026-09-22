using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Domain.Tests;

public class GuidelineTests
{
    private static Guideline New(string title = "Eficiência operacional", string? campaign = "Campanha 2026") =>
        Guideline.Create(title, "Descrição", Pillar.IDEIAS, campaign, "665f00000000000000000009", "Líder", TestData.Now);

    [Fact]
    public void Create_TrimsAndStoresCampaign()
    {
        var g = New("  Título válido  ", "  Campanha  ");
        g.Title.Should().Be("Título válido");
        g.Campaign.Should().Be("Campanha");
        g.CreatedAt.Should().Be(TestData.Now);
        g.UpdatedAt.Should().Be(TestData.Now);
    }

    [Fact]
    public void Create_BlankCampaign_BecomesNull() => New(campaign: "   ").Campaign.Should().BeNull();

    [Theory]
    [InlineData("ab")]
    [InlineData("")]
    public void Create_TitleOutOfRange_Throws(string title) =>
        FluentActions.Invoking(() => New(title)).Should().Throw<DomainException>();

    [Fact]
    public void Create_TitleTooLong_Throws() =>
        FluentActions.Invoking(() => New(new string('x', 121))).Should().Throw<DomainException>();

    [Fact]
    public void Update_ChangesFieldsAndUpdatedAt()
    {
        var g = New();
        var later = TestData.Now.AddHours(2);

        g.Update("Novo título", "Nova", Pillar.PROJETOS, null, later);

        g.Title.Should().Be("Novo título");
        g.Pillar.Should().Be(Pillar.PROJETOS);
        g.Campaign.Should().BeNull();
        g.UpdatedAt.Should().Be(later);
        g.CreatedAt.Should().Be(TestData.Now);
    }

    [Fact]
    public void History_CapturesCategoryCampaignAndSnapshot()
    {
        var g = New();

        var entry = GuidelineHistoryEntry.From(g, GuidelineAction.UPDATED, "665f0000000000000000000a", "Líder", TestData.Now);

        entry.GuidelineId.Should().Be(g.Id);
        entry.Category.Should().Be(Pillar.IDEIAS);
        entry.Campaign.Should().Be("Campanha 2026");
        entry.Snapshot.Title.Should().Be(g.Title);
        entry.Action.Should().Be(GuidelineAction.UPDATED);
        entry.OccurredAt.Should().Be(TestData.Now);
    }
}
