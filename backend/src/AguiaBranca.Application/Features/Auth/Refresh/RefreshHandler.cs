using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Entities;

namespace AguiaBranca.Application.Features.Auth.Refresh;

/// <summary>
/// Rotação do refresh token (R2-01.3). Reapresentar um token já rotacionado/revogado é indício de roubo:
/// toda a família é revogada e a resposta é 401.
/// </summary>
public sealed class RefreshHandler(
    IValidationService validation,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<RefreshCommand, AuthResponse>
{
    private static readonly Error Invalid = Error.Unauthorized("Refresh token inválido ou expirado.");

    public async Task<Result<AuthResponse>> HandleAsync(RefreshCommand request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<AuthResponse>.Fail(errors);

        var hash = tokens.HashRefreshToken(request.RefreshToken);

        // Tudo dentro da transação (releitura em caso de retry): a revogação da família precisa ser confirmada
        // mesmo quando a resposta é uma falha.
        return await unitOfWork.ExecuteInTransactionAsync<Result<AuthResponse>>(async token =>
        {
            var now = clock.UtcNow;
            var current = await refreshTokens.GetByHashAsync(hash, token);
            if (current is null) return Invalid;

            if (current.IsRevoked || current.WasRotated)
            {
                await RevokeFamilyAsync(current.FamilyId, now, token);
                return Invalid;
            }

            if (current.IsExpired(now)) return Invalid;

            var user = await users.GetByIdAsync(current.UserId, token);
            if (user is null)
            {
                await RevokeFamilyAsync(current.FamilyId, now, token);
                return Invalid;
            }

            var next = tokens.GenerateRefreshToken();
            current.Rotate(next.Hash, now);
            await refreshTokens.AddAsync(
                RefreshToken.Issue(user.Id, next.Hash, now, tokens.RefreshLifetime, current.FamilyId), token);

            var access = tokens.CreateAccessToken(user);
            return new AuthResponse(access.Value, next.Raw, access.ExpiresInSeconds, UserProfileResponse.From(user));
        }, ct);
    }

    private async Task RevokeFamilyAsync(string familyId, DateTime now, CancellationToken ct)
    {
        foreach (var member in await refreshTokens.ListByFamilyAsync(familyId, ct))
            member.Revoke(now);
    }
}
