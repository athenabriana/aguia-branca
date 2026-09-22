using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Features.Auth;

public sealed record UserProfileResponse(
    string Id, string Name, string Email, Role Role, Division Division, int Points, IReadOnlyList<string> Badges)
{
    public static UserProfileResponse From(AppUser user) =>
        new(user.Id, user.Name, user.Email ?? string.Empty, user.Role, user.Division, user.Points, user.Badges.ToArray());
}

public sealed record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, UserProfileResponse User);

public sealed record LoginCommand(string Email, string Password);
public sealed record RefreshCommand(string RefreshToken);
public sealed record LogoutCommand(string RefreshToken);
public sealed record MeQuery;
