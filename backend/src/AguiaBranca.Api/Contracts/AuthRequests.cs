namespace AguiaBranca.Api.Contracts;

// Propriedades anuláveis de propósito: a validação (mensagens em pt-BR) é da Application (FluentValidation),
// não do binding do MVC.
public sealed record LoginRequest(string? Email = null, string? Password = null);
public sealed record RefreshRequest(string? RefreshToken = null);
public sealed record LogoutRequest(string? RefreshToken = null);
