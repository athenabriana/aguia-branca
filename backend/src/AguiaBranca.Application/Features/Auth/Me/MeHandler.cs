using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;

namespace AguiaBranca.Application.Features.Auth.Me;

public sealed class MeHandler(ICurrentUser currentUser, IUserRepository users) : IHandler<MeQuery, UserProfileResponse>
{
    public async Task<Result<UserProfileResponse>> HandleAsync(MeQuery request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(currentUser.Id, ct);
        // Token válido de um usuário que não existe mais: trata como não autenticado.
        return user is null
            ? Error.Unauthorized("Usuário não encontrado.")
            : UserProfileResponse.From(user);
    }
}
