using AguiaBranca.Api.Authorization;
using AguiaBranca.Api.Contracts;
using AguiaBranca.Application.Features.Auth;
using AguiaBranca.Application.Features.Auth.Login;
using AguiaBranca.Application.Features.Auth.Logout;
using AguiaBranca.Application.Features.Auth.Me;
using AguiaBranca.Application.Features.Auth.Refresh;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AguiaBranca.Api.Controllers;

[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Autentica com e-mail e senha e devolve o par de tokens e o perfil.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, [FromServices] LoginHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty), ct));

    /// <summary>Troca o refresh token por um novo par (rotação). Reuso de um token já trocado revoga a sessão inteira.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request, [FromServices] RefreshHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new RefreshCommand(request.RefreshToken ?? string.Empty), ct));

    /// <summary>Revoga a sessão do refresh token informado.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request, [FromServices] LogoutHandler handler, CancellationToken ct) =>
        NoContentFrom(await handler.HandleAsync(new LogoutCommand(request.RefreshToken ?? string.Empty), ct));

    /// <summary>Perfil do usuário autenticado, com pontos e badges atualizados.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me([FromServices] MeHandler handler, CancellationToken ct) =>
        FromResult(await handler.HandleAsync(new MeQuery(), ct));
}
