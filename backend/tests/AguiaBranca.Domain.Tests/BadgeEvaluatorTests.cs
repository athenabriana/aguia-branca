using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Rules;

namespace AguiaBranca.Domain.Tests;

/// <summary>Porte de <c>BadgeEvaluatorTest.kt</c> (mesmos casos) + casos adicionais do servidor.</summary>
public class BadgeEvaluatorTests
{
    private static DateTime May(int day = 5) => new(2026, 5, day, 12, 0, 0, DateTimeKind.Utc);
    private static DateTime June(int day = 5) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PrimeiraIdeia_UnlocksOnFirstSubmission()
    {
        var user = TestData.User();
        BadgeEvaluator.Evaluate(user, [TestData.NewIdea(user)]).Should().Contain(Badges.PrimeiraIdeia);
    }

    [Fact]
    public void Estrategista_RequiresGuidelineAndApproval()
    {
        var user = TestData.User();
        var idea = TestData.IdeaWithStatus(IdeaStatus.APROVADA, user, TestData.ValidGuidelineId);
        BadgeEvaluator.Evaluate(user, [idea]).Should().Contain(Badges.Estrategista);
    }

    [Fact]
    public void Estrategista_BlockedWithoutGuideline()
    {
        var user = TestData.User();
        var idea = TestData.IdeaWithStatus(IdeaStatus.APROVADA, user);
        BadgeEvaluator.Evaluate(user, [idea]).Should().NotContain(Badges.Estrategista);
    }

    [Fact]
    public void Estrategista_BlockedWhileOnlySubmitted()
    {
        var user = TestData.User();
        var idea = TestData.IdeaWithStatus(IdeaStatus.SUBMETIDA, user, TestData.ValidGuidelineId);
        BadgeEvaluator.Evaluate(user, [idea]).Should().NotContain(Badges.Estrategista);
    }

    [Fact]
    public void InovadorDoMes_WithFiveInSameMonth()
    {
        var user = TestData.User();
        var ideas = Enumerable.Range(1, 5).Select(d => TestData.NewIdea(user, createdAt: May(d))).ToList();
        BadgeEvaluator.Evaluate(user, ideas).Should().Contain(Badges.InovadorDoMes);
    }

    [Fact]
    public void InovadorDoMes_BlockedWhenSplitAcrossMonths()
    {
        var user = TestData.User();
        var ideas = new[]
        {
            TestData.NewIdea(user, createdAt: May(1)), TestData.NewIdea(user, createdAt: May(2)), TestData.NewIdea(user, createdAt: May(3)),
            TestData.NewIdea(user, createdAt: June(1)), TestData.NewIdea(user, createdAt: June(2))
        };
        BadgeEvaluator.Evaluate(user, ideas).Should().NotContain(Badges.InovadorDoMes);
    }

    [Fact]
    public void InovadorDoMes_UsesConfiguredTimeZoneForMonthBoundary()
    {
        // 01/06 02:00 UTC = 31/05 23:00 em São Paulo → conta como maio no fuso local.
        var sp = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var user = TestData.User();
        var ideas = new[]
        {
            TestData.NewIdea(user, createdAt: May(10)), TestData.NewIdea(user, createdAt: May(11)),
            TestData.NewIdea(user, createdAt: May(12)), TestData.NewIdea(user, createdAt: May(13)),
            TestData.NewIdea(user, createdAt: new DateTime(2026, 6, 1, 2, 0, 0, DateTimeKind.Utc))
        };

        BadgeEvaluator.Evaluate(user, ideas, TimeZoneInfo.Utc).Should().NotContain(Badges.InovadorDoMes);
        BadgeEvaluator.Evaluate(user, ideas, sp).Should().Contain(Badges.InovadorDoMes);
    }

    [Fact]
    public void ImpactoReal_WhenAnyImplemented()
    {
        var user = TestData.User();
        var idea = TestData.IdeaWithStatus(IdeaStatus.IMPLEMENTADA, user, TestData.ValidGuidelineId);
        BadgeEvaluator.Evaluate(user, [idea]).Should().Contain(Badges.ImpactoReal);
    }

    [Fact]
    public void Visionario_NeedsThreeDistinctGuidelines()
    {
        var user = TestData.User();
        var ideas = new[]
        {
            TestData.IdeaWithStatus(IdeaStatus.APROVADA, user, "665f000000000000000000a1"),
            TestData.IdeaWithStatus(IdeaStatus.APROVADA, user, "665f000000000000000000a2"),
            TestData.IdeaWithStatus(IdeaStatus.IMPLEMENTADA, user, "665f000000000000000000a3")
        };
        BadgeEvaluator.Evaluate(user, ideas).Should().Contain(Badges.Visionario);
    }

    [Fact]
    public void Visionario_BlockedWithTwoDistinct()
    {
        var user = TestData.User();
        var ideas = new[]
        {
            TestData.IdeaWithStatus(IdeaStatus.APROVADA, user, "665f000000000000000000a1"),
            TestData.IdeaWithStatus(IdeaStatus.APROVADA, user, "665f000000000000000000a2"),
            TestData.IdeaWithStatus(IdeaStatus.APROVADA, user, "665f000000000000000000a1")
        };
        BadgeEvaluator.Evaluate(user, ideas).Should().NotContain(Badges.Visionario);
    }

    [Fact]
    public void DoesNotReUnlock_ExistingBadges()
    {
        var user = TestData.User();
        user.AddBadges([Badges.PrimeiraIdeia]);
        BadgeEvaluator.Evaluate(user, [TestData.NewIdea(user)]).Should().NotContain(Badges.PrimeiraIdeia);
    }

    [Fact]
    public void InovadorDoMes_NotRepeated_WhenAlreadyOwned()
    {
        var user = TestData.User();
        user.AddBadges([Badges.InovadorDoMes]);
        var ideas = Enumerable.Range(1, 6).Select(d => TestData.NewIdea(user, createdAt: June(d))).ToList();
        BadgeEvaluator.Evaluate(user, ideas).Should().NotContain(Badges.InovadorDoMes);
    }

    [Fact]
    public void IgnoresIdeasFromOtherAuthors()
    {
        var user = TestData.User();
        var other = TestData.User();
        var ideas = Enumerable.Range(1, 5).Select(d => TestData.NewIdea(other, createdAt: May(d))).ToList();

        BadgeEvaluator.Evaluate(user, ideas).Should().BeEmpty();
    }

    [Fact]
    public void NoIdeas_NoBadges() =>
        BadgeEvaluator.Evaluate(TestData.User(), []).Should().BeEmpty();
}
