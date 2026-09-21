using System.Security.Cryptography;
using System.Text;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AguiaBranca.Application.Tests.Support;

public sealed class FakeClock(DateTime? now = null) : IClock
{
    public DateTime UtcNow { get; set; } = now ?? new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
    public void Advance(TimeSpan by) => UtcNow += by;
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Usuário";
    public Role Role { get; set; } = Role.OPERADOR;
    public Division Division { get; set; } = Division.LOGISTICA;
}

/// <summary>Executa o trabalho na hora e conta as confirmações (não há banco).</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Transactions { get; private set; }
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct) { Saves++; return Task.FromResult(0); }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        Transactions++;
        return await work(ct);
    }
}

public sealed class FakeTokenService : ITokenService
{
    private int _counter;
    public TimeSpan RefreshLifetime { get; } = TimeSpan.FromDays(7);
    public string LastAccessTokenUserId { get; private set; } = string.Empty;

    public AccessToken CreateAccessToken(AppUser user)
    {
        LastAccessTokenUserId = user.Id;
        return new AccessToken($"jwt-for-{user.Id}", DateTime.UtcNow.AddMinutes(30), 1800);
    }

    public RefreshTokenValue GenerateRefreshToken()
    {
        var raw = $"raw-{++_counter}";
        return new RefreshTokenValue(raw, HashRefreshToken(raw));
    }

    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}

public sealed class InMemoryRefreshTokens : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];

    public Task AddAsync(RefreshToken token, CancellationToken ct) { Items.Add(token); return Task.CompletedTask; }
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash));
    public Task<IReadOnlyList<RefreshToken>> ListByFamilyAsync(string familyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(t => t.FamilyId == familyId).ToList());
}

public sealed class InMemoryUsers : IUserRepository
{
    public List<AppUser> Items { get; } = [];

    public Task<AppUser?> GetByIdAsync(string id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Id == id));
    public Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AppUser>>(Items.Where(u => ids.Contains(u.Id)).ToList());
    public Task<IReadOnlyList<AppUser>> ListAsync(Role? role, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AppUser>>(Items.Where(u => role is null || u.Role == role).ToList());
}

public sealed class FakeIdentityService : IIdentityService
{
    public CredentialCheckResult Result { get; set; } = CredentialCheckResult.Invalid();
    public List<(string Email, string Password)> Calls { get; } = [];

    public Task<CredentialCheckResult> CheckCredentialsAsync(string email, string password, CancellationToken ct)
    {
        Calls.Add((email, password));
        return Task.FromResult(Result);
    }

    public Task<Application.Common.Results.Result<AppUser>> CreateUserAsync(AppUser user, string password, CancellationToken ct) =>
        throw new NotSupportedException();
}

internal static class TestServices
{
    /// <summary>Serviço de validação real (com todos os validators da Application).</summary>
    public static IValidationService Validation()
    {
        var provider = new ServiceCollection().AddApplication().BuildServiceProvider();
        return provider.CreateScope().ServiceProvider.GetRequiredService<IValidationService>();
    }
}

internal static class UserFactory
{
    public static AppUser Operator(string name = "Operador") =>
        AppUser.Create(name, $"{Guid.NewGuid():N}@aguiabranca.com", Role.OPERADOR, Division.LOGISTICA, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
}
