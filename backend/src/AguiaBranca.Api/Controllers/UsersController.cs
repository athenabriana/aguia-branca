using AguiaBranca.Api.Authorization;
using AguiaBranca.Application.Features.Users;
using AguiaBranca.Application.Features.Users.List;
using AguiaBranca.Application.Features.Users.Ranking;
using AguiaBranca.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguiaBranca.Api.Controllers;

public sealed class UsersController : ApiControllerBase
{
    /// <summary>Usuários (id, nome, perfil, divisão), opcionalmente por perfil — para escolher responsáveis. Só gestor e líder.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.UsersRead)]
    [ProducesResponseType<IReadOnlyList<UserSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Role? role, [FromServices] ListUsersHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new ListUsersQuery(role), ct));

    /// <summary>Top do mês corrente (operadores), pela soma dos pontos do mês.</summary>
    [HttpGet("ranking")]
    [ProducesResponseType<IReadOnlyList<RankingEntryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Ranking([FromServices] RankingHandler handler, CancellationToken ct, [FromQuery] int limit = 5) =>
        FromResult(await handler.HandleAsync(new RankingQuery(limit), ct));
}
