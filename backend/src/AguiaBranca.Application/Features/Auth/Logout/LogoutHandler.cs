using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;

namespace AguiaBranca.Application.Features.Auth.Logout;

/// <summary>Revoga a família do refresh token informado. Idempotente e neutro: token desconhecido ou de outro usuário = no-op.</summary>
public sealed class LogoutHandler(
    IValidationService validation,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<LogoutCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(LogoutCommand request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<Unit>.Fail(errors);

        var hash = tokens.HashRefreshToken(request.RefreshToken);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var current = await refreshTokens.GetByHashAsync(hash, token);
            if (current is null || current.UserId != currentUser.Id) return 0;

            foreach (var member in await refreshTokens.ListByFamilyAsync(current.FamilyId, token))
                member.Revoke(clock.UtcNow);
            return 0;
        }, ct);

        return Result.Ok();
    }
}
