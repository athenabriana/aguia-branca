using AguiaBranca.Domain.Rules;

namespace AguiaBranca.Domain.Tests;

public class PointsRulesTests
{
    [Fact] public void Creation_WithoutLink_Is10() => PointsRules.ForCreation(false).Should().Be(10);
    [Fact] public void Creation_WithLink_Is15() => PointsRules.ForCreation(true).Should().Be(15);
    [Fact] public void Deletion_WithoutLink_IsMinus10() => PointsRules.ForDeletion(false).Should().Be(-10);
    [Fact] public void Deletion_WithLink_IsMinus15() => PointsRules.ForDeletion(true).Should().Be(-15);
    [Fact] public void Fixed_Awards() => (PointsRules.IdeaApproved, PointsRules.IdeaImplemented).Should().Be((50, 200));
}
