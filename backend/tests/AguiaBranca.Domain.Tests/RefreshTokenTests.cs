using AguiaBranca.Domain.Entities;

namespace AguiaBranca.Domain.Tests;

public class RefreshTokenTests
{
    private static RefreshToken Issue(TimeSpan? life = null) =>
        RefreshToken.Issue("665f00000000000000000001", "hash-1", TestData.Now, life ?? TimeSpan.FromDays(7));

    [Fact]
    public void Issue_IsActive_UntilExpiry()
    {
        var t = Issue();
        t.IsActive(TestData.Now.AddDays(6)).Should().BeTrue();
        t.IsActive(TestData.Now.AddDays(7)).Should().BeFalse();
        t.IsExpired(TestData.Now.AddDays(8)).Should().BeTrue();
    }

    [Fact]
    public void Issue_SameFamilyIsPreserved_WhenProvided()
    {
        var first = Issue();
        var next = RefreshToken.Issue(first.UserId, "hash-2", TestData.Now, TimeSpan.FromDays(7), first.FamilyId);
        next.FamilyId.Should().Be(first.FamilyId);
    }

    [Fact]
    public void Rotate_RevokesAndLinksSuccessor()
    {
        var t = Issue();
        t.Rotate("hash-2", TestData.Now.AddMinutes(1));

        t.IsRevoked.Should().BeTrue();
        t.WasRotated.Should().BeTrue();
        t.ReplacedByHash.Should().Be("hash-2");
        t.IsActive(TestData.Now.AddMinutes(2)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_IsIdempotent_KeepsFirstTimestamp()
    {
        var t = Issue();
        t.Revoke(TestData.Now.AddMinutes(1));
        t.Revoke(TestData.Now.AddMinutes(9));
        t.RevokedAt.Should().Be(TestData.Now.AddMinutes(1));
        t.WasRotated.Should().BeFalse();
    }
}
