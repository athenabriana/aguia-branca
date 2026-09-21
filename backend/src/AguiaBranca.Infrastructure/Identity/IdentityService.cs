using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AguiaBranca.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<AppUser> users, IPasswordHasher<AppUser> hasher, AppDbContext db) : IIdentityService
{
    // Hash de uma senha descartável: quando o e-mail não existe, gastamos o mesmo custo de PBKDF2
    // para que o tempo de resposta não revele quais contas existem.
    private static readonly AppUser Decoy = AppUser.Create("decoy", "decoy@invalid", Domain.Enums.Role.OPERADOR, Domain.Enums.Division.CORPORATIVO, DateTime.UnixEpoch);

    public async Task<CredentialCheckResult> CheckCredentialsAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            hasher.HashPassword(Decoy, password);
            return CredentialCheckResult.Invalid();
        }

        if (await users.IsLockedOutAsync(user)) return CredentialCheckResult.Locked(RemainingLockout(user));

        if (await users.CheckPasswordAsync(user, password))
        {
            if (user.AccessFailedCount > 0) user = await ApplyAsync(user, users.ResetAccessFailedCountAsync);
            return CredentialCheckResult.Ok(user);
        }

        user = await ApplyAsync(user, users.AccessFailedAsync);
        return await users.IsLockedOutAsync(user)
            ? CredentialCheckResult.Locked(RemainingLockout(user))
            : CredentialCheckResult.Invalid();
    }

    public async Task<Result<AppUser>> CreateUserAsync(AppUser user, string password, CancellationToken ct)
    {
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded) return user;

        return Result<AppUser>.Fail(result.Errors.Select(e => e.Code switch
        {
            "DuplicateEmail" or "DuplicateUserName" => Error.Conflict("USER_ALREADY_EXISTS", "Já existe um usuário com este e-mail."),
            var code when code.StartsWith("Password", StringComparison.Ordinal) => Error.Validation(e.Description, "password"),
            _ => Error.Validation(e.Description)
        }));
    }

    private static TimeSpan RemainingLockout(AppUser user) =>
        user.LockoutEnd is { } end && end > DateTimeOffset.UtcNow ? end - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(1);

    /// <summary>
    /// Aplica uma operação de gravação do Identity. Em conflito de versão (outra transação alterou o usuário, ex.: crédito de
    /// pontos) descarta a entidade velha, relê e tenta uma vez mais — devolvendo o usuário atualizado.
    /// (Não usa <c>ReloadAsync</c>: o provider não desserializa <c>DateTimeOffset?</c> nulo nesse caminho.)
    /// </summary>
    private async Task<AppUser> ApplyAsync(AppUser user, Func<AppUser, Task<IdentityResult>> operation)
    {
        var result = await operation(user);
        if (result.Succeeded || result.Errors.All(e => e.Code != "ConcurrencyFailure")) return user;

        db.Entry(user).State = EntityState.Detached;
        var fresh = await users.FindByIdAsync(user.Id) ?? user;
        await operation(fresh);
        return fresh;
    }
}
