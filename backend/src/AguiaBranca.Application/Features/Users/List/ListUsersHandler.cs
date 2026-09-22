using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Abstractions.Repositories;
using AguiaBranca.Application.Common.Results;

namespace AguiaBranca.Application.Features.Users.List;

public sealed class ListUsersHandler(IUserRepository users) : IHandler<ListUsersQuery, IReadOnlyList<UserSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<UserSummaryResponse>>> HandleAsync(ListUsersQuery request, CancellationToken ct)
    {
        if (request.Role is { } role && !Enum.IsDefined(role))
            return Error.Validation("Perfil inválido.", "role");

        var list = await users.ListAsync(request.Role, ct);
        return Result.Ok<IReadOnlyList<UserSummaryResponse>>(list.OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase).Select(UserSummaryResponse.From).ToList());
    }
}
