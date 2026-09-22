using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EfMongoSpike;

/// <summary>Store mínimo de Identity sobre o DbContext (base para a B08).</summary>
public sealed class SpikeUserStore(SpikeContext db) :
    IUserPasswordStore<SpikeUser>, IUserEmailStore<SpikeUser>,
    IUserLockoutStore<SpikeUser>, IUserSecurityStampStore<SpikeUser>
{
    public void Dispose() { }
    public Task<string> GetUserIdAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.Id);
    public Task<string?> GetUserNameAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.UserName);
    public Task SetUserNameAsync(SpikeUser u, string? n, CancellationToken ct) { u.UserName = n; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(SpikeUser u, string? n, CancellationToken ct) { u.NormalizedUserName = n; return Task.CompletedTask; }

    public async Task<IdentityResult> CreateAsync(SpikeUser u, CancellationToken ct)
    { db.Users.Add(u); await db.SaveChangesAsync(ct); return IdentityResult.Success; }
    public async Task<IdentityResult> UpdateAsync(SpikeUser u, CancellationToken ct)
    { db.Users.Update(u); await db.SaveChangesAsync(ct); return IdentityResult.Success; }
    public async Task<IdentityResult> DeleteAsync(SpikeUser u, CancellationToken ct)
    { db.Users.Remove(u); await db.SaveChangesAsync(ct); return IdentityResult.Success; }

    public Task<SpikeUser?> FindByIdAsync(string id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<SpikeUser?> FindByNameAsync(string normalized, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(x => x.NormalizedUserName == normalized, ct);
    public Task<SpikeUser?> FindByEmailAsync(string normalized, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);

    public Task SetPasswordHashAsync(SpikeUser u, string? h, CancellationToken ct) { u.PasswordHash = h; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash);
    public Task<bool> HasPasswordAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash != null);

    public Task SetEmailAsync(SpikeUser u, string? e, CancellationToken ct) { u.Email = e; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.Email);
    public Task<bool> GetEmailConfirmedAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(true);
    public Task SetEmailConfirmedAsync(SpikeUser u, bool c, CancellationToken ct) => Task.CompletedTask;
    public Task<string?> GetNormalizedEmailAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.NormalizedEmail);
    public Task SetNormalizedEmailAsync(SpikeUser u, string? e, CancellationToken ct) { u.NormalizedEmail = e; return Task.CompletedTask; }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnd);
    public Task SetLockoutEndDateAsync(SpikeUser u, DateTimeOffset? d, CancellationToken ct) { u.LockoutEnd = d; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(++u.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(SpikeUser u, CancellationToken ct) { u.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(true);
    public Task SetLockoutEnabledAsync(SpikeUser u, bool e, CancellationToken ct) => Task.CompletedTask;

    public Task SetSecurityStampAsync(SpikeUser u, string s, CancellationToken ct) { u.SecurityStamp = s; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(SpikeUser u, CancellationToken ct) => Task.FromResult(u.SecurityStamp);
}
