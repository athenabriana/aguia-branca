using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Features.Gamification;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;

namespace AguiaBranca.Application.Tests.Features.Gamification;

public class GamificationServiceTests
{
    private readonly InMemoryPointEvents _events = new();
    private readonly InMemoryIdeas _ideas = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 5, 20, 15, 0, 0, DateTimeKind.Utc));

    private GamificationService Service() => new(_events, _ideas, _uow, _clock, new FakeTimeZone());

    private Idea IdeaOf(AppUser author, DateTime at, IdeaStatus status = IdeaStatus.SUBMETIDA, string? guideline = null)
    {
        var idea = Idea.Create("Ideia de teste", "d", "tecnologia", Division.LOGISTICA, guideline, author.Id, author.Name, at);
        switch (status)
        {
            case IdeaStatus.APROVADA: idea.Approve("665f00000000000000000001", at); break;
            case IdeaStatus.IMPLEMENTADA: idea.Approve("665f00000000000000000001", at); idea.MarkImplemented(at); break;
        }
        _ideas.Items.Add(idea);
        return idea;
    }

    [Fact]
    public async Task Award_AppliesTheDelta_AndRecordsTheEffectiveEvent()
    {
        var user = UserFactory.Operator();

        var effective = await Service().AwardAsync(user, PointReason.IDEA_CREATED, 15, "665f00000000000000000aaa", default);

        effective.Should().Be(15);
        user.Points.Should().Be(15);
        var evt = _events.Items.Should().ContainSingle().Subject;
        (evt.UserId, evt.Delta, evt.Reason, evt.RefId, evt.CreatedAt)
            .Should().Be((user.Id, 15, PointReason.IDEA_CREATED, "665f00000000000000000aaa", _clock.UtcNow));
    }

    [Fact]
    public async Task Award_NegativeBeyondTheBalance_ClampsAtZero_AndStoresTheEffectiveDelta()
    {
        var user = UserFactory.Operator();
        user.ApplyPoints(10);

        var effective = await Service().AwardAsync(user, PointReason.IDEA_DELETED, -15, null, default);

        effective.Should().Be(-10);
        user.Points.Should().Be(0);
        _events.Items.Should().ContainSingle().Which.Delta.Should().Be(-10, "o evento guarda o valor efetivo, não o pedido");
    }

    [Fact]
    public async Task Award_WhenNothingChanges_RecordsNoEvent()
    {
        var user = UserFactory.Operator();
        (await Service().AwardAsync(user, PointReason.IDEA_DELETED, -10, null, default)).Should().Be(0);
        _events.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GrantBadges_FlushesFirst_AddsNewBadgesOnly_AndIsIdempotent()
    {
        var user = UserFactory.Operator();
        IdeaOf(user, _clock.UtcNow);

        var first = await Service().GrantEarnedBadgesAsync(user, default);
        var second = await Service().GrantEarnedBadgesAsync(user, default);

        first.Should().Equal(Badges.PrimeiraIdeia);
        second.Should().BeEmpty();
        user.Badges.Should().Equal(Badges.PrimeiraIdeia);
        _uow.Saves.Should().Be(2, "cada avaliação confirma antes as alterações pendentes");
    }

    [Fact]
    public async Task GrantBadges_StrategistAndImpact_FollowIdeaState()
    {
        var user = UserFactory.Operator();
        IdeaOf(user, _clock.UtcNow, IdeaStatus.IMPLEMENTADA, "665f000000000000000000a1");

        var granted = await Service().GrantEarnedBadgesAsync(user, default);

        granted.Should().BeEquivalentTo([Badges.PrimeiraIdeia, Badges.Estrategista, Badges.ImpactoReal]);
    }

    [Fact]
    public async Task GrantBadges_InovadorDoMes_UsesTheConfiguredTimeZoneForTheMonth()
    {
        var user = UserFactory.Operator();
        // 4 ideias em maio + 1 em 01/06 02:00 UTC (= 31/05 23:00 em São Paulo) → 5 no mês de maio local.
        for (var d = 10; d < 14; d++) IdeaOf(user, new DateTime(2026, 5, d, 12, 0, 0, DateTimeKind.Utc));
        IdeaOf(user, new DateTime(2026, 6, 1, 2, 0, 0, DateTimeKind.Utc));

        var granted = await Service().GrantEarnedBadgesAsync(user, default);

        granted.Should().Contain(Badges.InovadorDoMes);
    }

    [Fact]
    public async Task GrantBadges_IgnoresOtherAuthorsIdeas()
    {
        var user = UserFactory.Operator();
        IdeaOf(UserFactory.Operator("Outro"), _clock.UtcNow);

        (await Service().GrantEarnedBadgesAsync(user, default)).Should().BeEmpty();
    }
}
