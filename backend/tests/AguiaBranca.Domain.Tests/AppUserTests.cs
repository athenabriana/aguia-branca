using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.Entities;

namespace AguiaBranca.Domain.Tests;

public class AppUserTests
{
    [Fact]
    public void Create_SetsIdentityAndDefaults()
    {
        var user = AppUser.Create("  Ana  ", " ana@x.com ", Role.GESTOR, Division.COMERCIO, TestData.Now);

        EntityId.IsValid(user.Id).Should().BeTrue();
        user.Name.Should().Be("Ana");
        user.Email.Should().Be("ana@x.com");
        user.UserName.Should().Be("ana@x.com");
        user.Points.Should().Be(0);
        user.Badges.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "a@x.com")]
    [InlineData("Ana", " ")]
    public void Create_RequiresNameAndEmail(string name, string email)
    {
        var act = () => AppUser.Create(name, email, Role.OPERADOR, Division.LOGISTICA, TestData.Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ApplyPoints_Positive_ReturnsFullDelta()
    {
        var user = TestData.User();
        user.ApplyPoints(15).Should().Be(15);
        user.Points.Should().Be(15);
    }

    [Fact]
    public void ApplyPoints_NegativeBelowZero_ClampsAndReturnsEffectiveDelta()
    {
        var user = TestData.User();
        user.ApplyPoints(10);

        var effective = user.ApplyPoints(-15);

        user.Points.Should().Be(0);
        effective.Should().Be(-10); // só havia 10 pontos para tirar
    }

    [Fact]
    public void ApplyPoints_AtZero_NegativeIsNoOp()
    {
        var user = TestData.User();
        user.ApplyPoints(-10).Should().Be(0);
        user.Points.Should().Be(0);
    }

    [Fact]
    public void AddBadges_ReturnsOnlyNew_AndNeverDuplicates()
    {
        var user = TestData.User();
        user.AddBadges(["A", "B"]).Should().BeEquivalentTo(["A", "B"]);

        user.AddBadges(["B", "C"]).Should().BeEquivalentTo(["C"]);

        user.Badges.Should().BeEquivalentTo(["A", "B", "C"]);
    }

    [Fact]
    public void Version_StartsAt1_AndBumpsOnlyOnRealChanges()
    {
        var user = TestData.User();
        user.Version.Should().Be(1);

        user.ApplyPoints(10);           // mudou
        user.Version.Should().Be(2);
        user.ApplyPoints(-100);         // clamp: efetivo -10 → mudou
        user.Version.Should().Be(3);
        user.ApplyPoints(-5);           // já em 0 → sem efeito
        user.Version.Should().Be(3);

        user.AddBadges(["A"]);
        user.Version.Should().Be(4);
        user.AddBadges(["A"]);          // duplicada → sem efeito
        user.Version.Should().Be(4);

        user.MarkModified();
        user.Version.Should().Be(5);
    }
}
