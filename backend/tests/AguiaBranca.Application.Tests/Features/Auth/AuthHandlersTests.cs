using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Features.Auth;
using AguiaBranca.Application.Features.Auth.Login;
using AguiaBranca.Application.Features.Auth.Logout;
using AguiaBranca.Application.Features.Auth.Me;
using AguiaBranca.Application.Features.Auth.Refresh;
using AguiaBranca.Application.Tests.Support;
using AguiaBranca.Domain.Entities;

namespace AguiaBranca.Application.Tests.Features.Auth;

public class LoginHandlerTests
{
    private readonly FakeIdentityService _identity = new();
    private readonly FakeTokenService _tokens = new();
    private readonly InMemoryRefreshTokens _refresh = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeClock _clock = new();

    private LoginHandler Handler() =>
        new(TestServices.Validation(), _identity, _tokens, _refresh, _uow, _clock);

    [Fact]
    public async Task Success_ReturnsTokens_Profile_AndPersistsOnlyTheHash()
    {
        var user = UserFactory.Operator();
        user.ApplyPoints(30);
        user.AddBadges(["Primeira Ideia"]);
        _identity.Result = CredentialCheckResult.Ok(user);

        var result = await Handler().HandleAsync(new LoginCommand("  a@b.com  ", "senha-boa-123"), default);

        result.IsSuccess.Should().BeTrue();
        var body = result.Value;
        body.AccessToken.Should().Be($"jwt-for-{user.Id}");
        body.ExpiresIn.Should().Be(1800);
        body.RefreshToken.Should().Be("raw-1");
        body.User.Should().BeEquivalentTo(new { user.Id, Points = 30, Badges = new[] { "Primeira Ideia" } });

        _identity.Calls.Should().ContainSingle().Which.Should().Be(("a@b.com", "senha-boa-123")); // e-mail aparado
        var stored = _refresh.Items.Should().ContainSingle().Subject;
        stored.TokenHash.Should().Be(_tokens.HashRefreshToken("raw-1"));
        stored.TokenHash.Should().NotContain("raw-1", "somente o hash é persistido");
        stored.UserId.Should().Be(user.Id);
        stored.ExpiresAt.Should().Be(_clock.UtcNow.AddDays(7));
        _uow.Transactions.Should().Be(1);
    }

    [Fact]
    public async Task InvalidCredentials_Returns401_WithNeutralMessage_AndPersistsNothing()
    {
        _identity.Result = CredentialCheckResult.Invalid();

        var result = await Handler().HandleAsync(new LoginCommand("a@b.com", "errada"), default);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Should().Be(LoginHandler.InvalidCredentials);
        result.FirstError.ToStatusCode().Should().Be(401);
        result.FirstError.Code.Should().Be("INVALID_CREDENTIALS");
        _refresh.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task LockedOut_Returns429_WithRetryAfter_RoundedUp()
    {
        _identity.Result = CredentialCheckResult.Locked(TimeSpan.FromSeconds(899.2));

        var result = await Handler().HandleAsync(new LoginCommand("a@b.com", "x"), default);

        var error = result.FirstError;
        error.Code.Should().Be("ACCOUNT_LOCKED");
        error.ToStatusCode().Should().Be(429);
        error.RetryAfterSeconds.Should().Be(900);
        _refresh.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "senha", "email")]
    [InlineData("nao-e-email", "senha", "email")]
    [InlineData("a@b.com", "", "password")]
    public async Task InvalidInput_Returns400_WithoutTouchingIdentity(string email, string password, string field)
    {
        var result = await Handler().HandleAsync(new LoginCommand(email, password), default);

        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.Errors.Should().Contain(e => e.Field == field);
        _identity.Calls.Should().BeEmpty();
    }
}

public class RefreshHandlerTests
{
    private readonly FakeTokenService _tokens = new();
    private readonly InMemoryRefreshTokens _refresh = new();
    private readonly InMemoryUsers _users = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeClock _clock = new();

    private RefreshHandler Handler() => new(TestServices.Validation(), _tokens, _refresh, _users, _uow, _clock);

    private (AppUser User, RefreshToken Token, string Raw) Seed(TimeSpan? life = null)
    {
        var user = UserFactory.Operator();
        _users.Items.Add(user);
        var value = _tokens.GenerateRefreshToken();
        var token = RefreshToken.Issue(user.Id, value.Hash, _clock.UtcNow, life ?? TimeSpan.FromDays(7));
        _refresh.Items.Add(token);
        return (user, token, value.Raw);
    }

    [Fact]
    public async Task ActiveToken_IsRotated_KeepsFamily_AndReturnsNewPair()
    {
        var (user, old, raw) = Seed();
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await Handler().HandleAsync(new RefreshCommand(raw), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshToken.Should().NotBe(raw);
        result.Value.AccessToken.Should().Be($"jwt-for-{user.Id}");
        old.WasRotated.Should().BeTrue();
        old.IsRevoked.Should().BeTrue();
        var next = _refresh.Items.Single(t => t != old);
        next.FamilyId.Should().Be(old.FamilyId);
        next.TokenHash.Should().Be(_tokens.HashRefreshToken(result.Value.RefreshToken));
        old.ReplacedByHash.Should().Be(next.TokenHash);
        next.IsActive(_clock.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task UnknownToken_IsRejected() =>
        (await Handler().HandleAsync(new RefreshCommand("desconhecido"), default))
            .FirstError.Code.Should().Be("TOKEN_INVALID");

    [Fact]
    public async Task ReusedToken_RevokesTheWholeFamily_IncludingTheSuccessor()
    {
        var (_, _, raw) = Seed();
        var first = await Handler().HandleAsync(new RefreshCommand(raw), default);
        first.IsSuccess.Should().BeTrue();

        // reapresenta o token antigo (já rotacionado) → indício de roubo
        var replay = await Handler().HandleAsync(new RefreshCommand(raw), default);

        replay.FirstError.Code.Should().Be("TOKEN_INVALID");
        _refresh.Items.Should().OnlyContain(t => t.IsRevoked, "toda a família deve ser revogada");
        // e o sucessor legítimo também deixa de funcionar
        (await Handler().HandleAsync(new RefreshCommand(first.Value.RefreshToken), default)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RevokedToken_IsRejected_AndFamilyStaysRevoked()
    {
        var (_, token, raw) = Seed();
        token.Revoke(_clock.UtcNow);

        var result = await Handler().HandleAsync(new RefreshCommand(raw), default);

        result.IsFailure.Should().BeTrue();
        _refresh.Items.Should().ContainSingle("nenhum sucessor deve ser emitido");
    }

    [Fact]
    public async Task ExpiredToken_IsRejected_WithoutRotation()
    {
        var (_, token, raw) = Seed(TimeSpan.FromDays(1));
        _clock.Advance(TimeSpan.FromDays(2));

        var result = await Handler().HandleAsync(new RefreshCommand(raw), default);

        result.IsFailure.Should().BeTrue();
        token.WasRotated.Should().BeFalse();
        _refresh.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task TokenOfDeletedUser_IsRejected_AndFamilyRevoked()
    {
        var (user, token, raw) = Seed();
        _users.Items.Remove(user);

        var result = await Handler().HandleAsync(new RefreshCommand(raw), default);

        result.IsFailure.Should().BeTrue();
        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyToken_Returns400()
    {
        var result = await Handler().HandleAsync(new RefreshCommand(""), default);
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }
}

public class LogoutHandlerTests
{
    private readonly FakeTokenService _tokens = new();
    private readonly InMemoryRefreshTokens _refresh = new();
    private readonly FakeCurrentUser _current = new();
    private readonly FakeClock _clock = new();

    private LogoutHandler Handler() =>
        new(TestServices.Validation(), _tokens, _refresh, _current, new FakeUnitOfWork(), _clock);

    private (RefreshToken Token, string Raw) IssueFor(string userId)
    {
        var value = _tokens.GenerateRefreshToken();
        var token = RefreshToken.Issue(userId, value.Hash, _clock.UtcNow, TimeSpan.FromDays(7));
        _refresh.Items.Add(token);
        return (token, value.Raw);
    }

    [Fact]
    public async Task Logout_RevokesTheCallersTokenFamily()
    {
        _current.Id = "665f00000000000000000001";
        var (first, raw) = IssueFor(_current.Id);
        var successor = RefreshToken.Issue(_current.Id, "h2", _clock.UtcNow, TimeSpan.FromDays(7), first.FamilyId);
        _refresh.Items.Add(successor);

        var result = await Handler().HandleAsync(new LogoutCommand(raw), default);

        result.IsSuccess.Should().BeTrue();
        first.IsRevoked.Should().BeTrue();
        successor.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_WithAnotherUsersToken_IsANeutralNoOp()
    {
        _current.Id = "665f00000000000000000001";
        var (token, raw) = IssueFor("665f00000000000000000002");

        var result = await Handler().HandleAsync(new LogoutCommand(raw), default);

        result.IsSuccess.Should().BeTrue();
        token.IsRevoked.Should().BeFalse("não pode revogar sessão de outra pessoa nem revelar que ela existe");
    }

    [Fact]
    public async Task Logout_WithUnknownToken_Succeeds() =>
        (await Handler().HandleAsync(new LogoutCommand("nada"), default)).IsSuccess.Should().BeTrue();

    [Fact]
    public async Task Logout_WithoutToken_Returns400() =>
        (await Handler().HandleAsync(new LogoutCommand(""), default)).FirstError.Type.Should().Be(ErrorType.Validation);
}

public class MeHandlerTests
{
    [Fact]
    public async Task ReturnsFreshProfile_WithPointsAndBadges()
    {
        var users = new InMemoryUsers();
        var user = UserFactory.Operator("Ana");
        user.ApplyPoints(15);
        user.AddBadges(["Primeira Ideia"]);
        users.Items.Add(user);

        var result = await new MeHandler(new FakeCurrentUser { Id = user.Id }, users).HandleAsync(new MeQuery(), default);

        result.Value.Should().BeEquivalentTo(new { Name = "Ana", Points = 15, Badges = new[] { "Primeira Ideia" } });
    }

    [Fact]
    public async Task UnknownUser_IsUnauthorized()
    {
        var result = await new MeHandler(new FakeCurrentUser { Id = "665f00000000000000000009" }, new InMemoryUsers())
            .HandleAsync(new MeQuery(), default);

        result.FirstError.ToStatusCode().Should().Be(401);
    }
}
