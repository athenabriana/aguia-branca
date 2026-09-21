using AguiaBranca.Domain.Entities;
using AguiaBranca.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppConflict = AguiaBranca.Application.Common.Exceptions;

namespace AguiaBranca.Infrastructure.Identity;

/// <summary>
/// Store do ASP.NET Identity sobre o <see cref="AppDbContext"/> (o provider EF Mongo não suporta o <c>IdentityDbContext</c>;
/// ADR-003, validado no spike B03). Sem role store: o perfil é o campo <c>role</c> do usuário.
/// </summary>
internal sealed class MongoUserStore(AppDbContext db, IdentityErrorDescriber describer) :
    IUserPasswordStore<AppUser>, IUserEmailStore<AppUser>, IUserLockoutStore<AppUser>, IUserSecurityStampStore<AppUser>
{
    public void Dispose() { }

    // ---------- IUserStore ----------
    public Task<string> GetUserIdAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.Id);
    public Task<string?> GetUserNameAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.UserName);
    public Task SetUserNameAsync(AppUser user, string? userName, CancellationToken ct) { user.UserName = userName; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(AppUser user, string? normalizedName, CancellationToken ct) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }

    public async Task<IdentityResult> CreateAsync(AppUser user, CancellationToken ct)
    {
        db.Users.Add(user);
        return await SaveAsync(ct);
    }

    public async Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken ct)
    {
        // Entidade já rastreada (carregada por este contexto) grava só o que mudou; senão anexa.
        if (db.Entry(user).State == EntityState.Detached) db.Users.Update(user);
        user.MarkModified();
        return await SaveAsync(ct);
    }

    public async Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken ct)
    {
        db.Users.Remove(user);
        return await SaveAsync(ct);
    }

    public Task<AppUser?> FindByIdAsync(string userId, CancellationToken ct) =>
        Domain.Common.EntityId.IsValid(userId) ? db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct) : Task.FromResult<AppUser?>(null);

    public Task<AppUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName, ct);

    // ---------- IUserEmailStore ----------
    public Task<AppUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);
    public Task SetEmailAsync(AppUser user, string? email, CancellationToken ct) { user.Email = email; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(AppUser user, CancellationToken ct) => Task.FromResult(true);
    public Task SetEmailConfirmedAsync(AppUser user, bool confirmed, CancellationToken ct) => Task.CompletedTask;
    public Task<string?> GetNormalizedEmailAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.NormalizedEmail);
    public Task SetNormalizedEmailAsync(AppUser user, string? normalizedEmail, CancellationToken ct) { user.NormalizedEmail = normalizedEmail; return Task.CompletedTask; }

    // ---------- IUserPasswordStore ----------
    public Task SetPasswordHashAsync(AppUser user, string? passwordHash, CancellationToken ct) { user.PasswordHash = passwordHash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(AppUser user, CancellationToken ct) => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    // ---------- IUserLockoutStore ----------
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.LockoutEnd);
    public Task SetLockoutEndDateAsync(AppUser user, DateTimeOffset? lockoutEnd, CancellationToken ct) { user.LockoutEnd = lockoutEnd; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(AppUser user, CancellationToken ct) => Task.FromResult(++user.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(AppUser user, CancellationToken ct) { user.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(AppUser user, CancellationToken ct) => Task.FromResult(true);
    public Task SetLockoutEnabledAsync(AppUser user, bool enabled, CancellationToken ct) => Task.CompletedTask;

    // ---------- IUserSecurityStampStore ----------
    public Task SetSecurityStampAsync(AppUser user, string stamp, CancellationToken ct) { user.SecurityStamp = stamp; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.SecurityStamp);

    // ---------- persistência ----------
    private async Task<IdentityResult> SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return IdentityResult.Success;
        }
        catch (Exception ex) when (MongoUnitOfWork.Translate(ex) is { } translated)
        {
            return translated switch
            {
                AppConflict.ConcurrencyConflictException => IdentityResult.Failed(describer.ConcurrencyFailure()),
                AppConflict.DuplicateKeyException => IdentityResult.Failed(describer.DuplicateEmail(string.Empty)),
                _ => throw translated
            };
        }
    }
}
