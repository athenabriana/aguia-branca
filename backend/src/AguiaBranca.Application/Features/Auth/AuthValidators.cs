using FluentValidation;

namespace AguiaBranca.Application.Features.Auth;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-mail é obrigatório.")
            .MaximumLength(254).WithMessage("E-mail muito longo.")
            .EmailAddress().WithMessage("E-mail inválido.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Senha é obrigatória.")
            .MaximumLength(128).WithMessage("Senha muito longa.");
    }
}

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("refreshToken é obrigatório.")
            .MaximumLength(200).WithMessage("refreshToken inválido.");
}

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("refreshToken é obrigatório.")
            .MaximumLength(200).WithMessage("refreshToken inválido.");
}
