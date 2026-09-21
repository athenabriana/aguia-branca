using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;
using AguiaBranca.Application.Common.Validation;
using AguiaBranca.Domain.Entities;

namespace AguiaBranca.Application.Features.Auth.Login;

public sealed class LoginHandler(
    IValidationService validation,
    IIdentityService identity,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IClock clock) : IHandler<LoginCommand, AuthResponse>
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("E-mail ou senha inválidos.", "INVALID_CREDENTIALS");

    public async Task<Result<AuthResponse>> HandleAsync(LoginCommand request, CancellationToken ct)
    {
        var errors = await validation.ValidateAsync(request, ct);
        if (errors.Count > 0) return Result<AuthResponse>.Fail(errors);

        var check = await identity.CheckCredentialsAsync(request.Email.Trim(), request.Password, ct);
        switch (check.Status)
        {
            case CredentialStatus.Invalid:
                // Mesma resposta para "usuário inexistente" e "senha errada" (R2-01.7).
                return InvalidCredentials;
            case CredentialStatus.LockedOut:
                var seconds = (int)Math.Ceiling((check.RetryAfter ?? TimeSpan.FromMinutes(15)).TotalSeconds);
                return new Error("ACCOUNT_LOCKED", "Muitas tentativas. Tente novamente mais tarde.", ErrorType.TooManyRequests,
                    RetryAfterSeconds: Math.Max(seconds, 1));
        }

        var user = check.User!;
        var refresh = tokens.GenerateRefreshToken();
        var entity = RefreshToken.Issue(user.Id, refresh.Hash, clock.UtcNow, tokens.RefreshLifetime);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await refreshTokens.AddAsync(entity, token);
            return 0;
        }, ct);

        var access = tokens.CreateAccessToken(user);
        return new AuthResponse(access.Value, refresh.Raw, access.ExpiresInSeconds, UserProfileResponse.From(user));
    }
}
