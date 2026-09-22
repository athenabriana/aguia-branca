using System.Security.Claims;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Api.Http;

/// <summary>Claims do JWT: <c>sub</c>, <c>name</c>, <c>role</c>, <c>division</c> (design §7.2).</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public string Id => Claim("sub", ClaimTypes.NameIdentifier) ?? string.Empty;
    public string Name => Claim("name", ClaimTypes.Name) ?? string.Empty;
    public Role Role => Enum.TryParse<Role>(Claim("role", ClaimTypes.Role), out var role) ? role : default;
    public Division Division => Enum.TryParse<Division>(Claim("division"), out var division) ? division : default;

    private string? Claim(params string[] types) =>
        types.Select(t => Principal?.FindFirst(t)?.Value).FirstOrDefault(v => !string.IsNullOrEmpty(v));
}
