using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Infrastructure.Identity;
using AguiaBranca.Infrastructure.Persistence;
using AguiaBranca.Infrastructure.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Tests.Identity;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public sealed class IdentityTests(MongoFixture fixture) : IAsyncLifetime
{
    private const string Password = "aguiabranca123";
    private TestDatabase _db = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _db = await fixture.CreateDatabaseAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _db.CreateContext());
        AguiaBranca.Infrastructure.DependencyInjection.AddIdentity(services);
        services.AddScoped<IIdentityService, IdentityService>();
        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _db.DisposeAsync();
    }

    private static AppUser NewUser(string email = "lider@aguiabranca.com", Role role = Role.LIDER) =>
        AppUser.Create("Líder", email, role, Division.CORPORATIVO, DateTime.UtcNow);

    private async Task<AppUser> CreateAsync(string email = "lider@aguiabranca.com", Role role = Role.LIDER)
    {
        using var scope = _provider.CreateScope();
        var user = NewUser(email, role);
        var result = await scope.ServiceProvider.GetRequiredService<IIdentityService>().CreateUserAsync(user, Password, default);
        result.IsSuccess.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
        return user;
    }

    private async Task<CredentialCheckResult> CheckAsync(string email, string password)
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityService>().CheckCredentialsAsync(email, password, default);
    }

    private async Task<BsonDocument> RawUser(string id) =>
        await _db.Raw(Collections.Users).Find(new BsonDocument("_id", new ObjectId(id))).SingleAsync();

    [Fact]
    public async Task Create_StoresOnlyAHash_NeverThePlainPassword()
    {
        var user = await CreateAsync();

        var raw = await RawUser(user.Id);
        var hash = raw["passwordHash"].AsString;
        hash.Should().NotBe(Password).And.NotContain(Password);
        hash.Should().StartWith("AQAAAA", "formato PBKDF2 do Identity (v3)");
        raw["securityStamp"].AsString.Should().NotBeNullOrEmpty();
        raw["normalizedEmail"].AsString.Should().Be("LIDER@AGUIABRANCA.COM");
        raw["role"].AsString.Should().Be("LIDER");
        raw["version"].AsInt32.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CheckCredentials_CorrectPassword_Succeeds_CaseInsensitiveEmail()
    {
        var user = await CreateAsync();

        var result = await CheckAsync("LIDER@AguiaBranca.com", Password);

        result.Status.Should().Be(CredentialStatus.Success);
        result.User!.Id.Should().Be(user.Id);
        result.User.Role.Should().Be(Role.LIDER);
    }

    [Fact]
    public async Task CheckCredentials_WrongPassword_AndUnknownEmail_AreIndistinguishable()
    {
        await CreateAsync();

        var wrong = await CheckAsync("lider@aguiabranca.com", "senha-errada-1");
        var unknown = await CheckAsync("ninguem@aguiabranca.com", "senha-errada-1");

        wrong.Should().BeEquivalentTo(unknown);
        wrong.Status.Should().Be(CredentialStatus.Invalid);
    }

    [Fact]
    public async Task ShortPassword_IsRejected_AsPasswordValidationError()
    {
        using var scope = _provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
            .CreateUserAsync(NewUser("curta@aguiabranca.com"), "curta", default);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Type == ErrorType.Validation && e.Field == "password");
        (await _db.Raw(Collections.Users).CountDocumentsAsync(new BsonDocument())).Should().Be(0);
    }

    [Fact]
    public async Task DuplicateEmail_IsRejected_CaseInsensitive()
    {
        await CreateAsync("dup@aguiabranca.com");

        using var scope = _provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
            .CreateUserAsync(NewUser("DUP@aguiabranca.com"), Password, default);

        result.FirstError.Should().BeEquivalentTo(new { Code = "USER_ALREADY_EXISTS", Type = ErrorType.Conflict });
    }

    [Fact]
    public async Task DuplicateEmail_RaceAtTheIndexLevel_IsReportedAsIdentityFailure_NotAnException()
    {
        await CreateAsync("race@aguiabranca.com");

        // Ignora a validação do UserManager (simula a corrida) e vai direto ao store: o índice único decide.
        using var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserStore<AppUser>>();
        var clone = NewUser("race2@aguiabranca.com");
        clone.NormalizedEmail = "RACE@AGUIABRANCA.COM";
        clone.NormalizedUserName = "RACE2@AGUIABRANCA.COM";

        var result = await store.CreateAsync(clone, default);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "DuplicateEmail");
    }

    [Fact]
    public async Task Lockout_After5Failures_LocksFor15Minutes_AndPersists()
    {
        await CreateAsync();

        for (var i = 1; i <= 4; i++)
            (await CheckAsync("lider@aguiabranca.com", "errada-123")).Status.Should().Be(CredentialStatus.Invalid, $"tentativa {i}");

        var fifth = await CheckAsync("lider@aguiabranca.com", "errada-123");
        fifth.Status.Should().Be(CredentialStatus.LockedOut);
        fifth.RetryAfter!.Value.TotalMinutes.Should().BeInRange(14, 15.01);

        // Bloqueado: nem a senha correta entra enquanto durar o lockout.
        (await CheckAsync("lider@aguiabranca.com", Password)).Status.Should().Be(CredentialStatus.LockedOut);

        var raw = await _db.Raw(Collections.Users).Find(new BsonDocument()).SingleAsync();
        raw["lockoutEnd"].IsBsonNull.Should().BeFalse("o lockout precisa estar persistido");
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsTheFailureCounter()
    {
        var user = await CreateAsync();
        for (var i = 0; i < 3; i++) await CheckAsync("lider@aguiabranca.com", "errada-123");
        (await RawUser(user.Id))["accessFailedCount"].AsInt32.Should().Be(3);

        (await CheckAsync("lider@aguiabranca.com", Password)).Status.Should().Be(CredentialStatus.Success);

        (await RawUser(user.Id))["accessFailedCount"].AsInt32.Should().Be(0);
        // com o contador zerado, mais 4 erros ainda não bloqueiam
        for (var i = 0; i < 4; i++) (await CheckAsync("lider@aguiabranca.com", "errada-123")).Status.Should().Be(CredentialStatus.Invalid);
    }

    [Fact]
    public async Task StaleIdentityWrite_DoesNotOverwritePointsChangedByAnotherTransaction()
    {
        var created = await CreateAsync();

        // A carrega o usuário; B credita pontos e confirma; só então A grava (contador de falhas).
        using var scopeA = _provider.CreateScope();
        var userManagerA = scopeA.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var staleUser = (await userManagerA.FindByEmailAsync("lider@aguiabranca.com"))!;

        await using (var ctxB = _db.CreateContext())
        {
            var userB = ctxB.Users.First(u => u.Id == created.Id);
            userB.ApplyPoints(50);
            await _db.CreateUnitOfWork(ctxB).SaveChangesAsync(default);
        }

        var result = await userManagerA.AccessFailedAsync(staleUser);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ConcurrencyFailure");
        var raw = await RawUser(created.Id);
        raw["points"].AsInt32.Should().Be(50, "os pontos creditados por B não podem ser perdidos");
        raw["accessFailedCount"].AsInt32.Should().Be(0, "a escrita velha foi recusada");
    }

    [Fact]
    public async Task IdentityService_RecoversFromAConcurrentPointsUpdate_WithoutLosingPoints()
    {
        var created = await CreateAsync();

        // Um crédito de pontos concorrente confirma ENTRE o carregamento e a gravação do contador de falhas:
        // simulamos com um usuário rastreado desatualizado passado ao retry do IdentityService via UserManager.
        using var scope = _provider.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _ = db.Users.First(u => u.Id == created.Id); // rastreia com versão antiga

        await using (var other = _db.CreateContext())
        {
            other.Users.First(u => u.Id == created.Id).ApplyPoints(20);
            await _db.CreateUnitOfWork(other).SaveChangesAsync(default);
        }

        // O IdentityService relê e reaplica o contador com a versão nova (Reload no retry).
        var result = await identity.CheckCredentialsAsync("lider@aguiabranca.com", "errada-123", default);

        result.Status.Should().Be(CredentialStatus.Invalid);
        var raw = await RawUser(created.Id);
        raw["points"].AsInt32.Should().Be(20);
        raw["accessFailedCount"].AsInt32.Should().Be(1);
    }

    [Fact]
    public async Task PasswordHasher_ProducesDifferentHashesForSamePassword()
    {
        var a = await CreateAsync("a@aguiabranca.com");
        var b = await CreateAsync("b@aguiabranca.com");

        (await RawUser(a.Id))["passwordHash"].AsString.Should().NotBe((await RawUser(b.Id))["passwordHash"].AsString, "salt por usuário");
    }
}
