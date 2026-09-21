using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;
using FluentValidation;

namespace AguiaBranca.Application.Features.Reports.Insights;

public sealed class GenerateInsightsCommandValidator : AbstractValidator<GenerateInsightsCommand>
{
    public GenerateInsightsCommandValidator()
    {
        RuleFor(x => x.GuidelineId)
            .Must(id => id is null || EntityId.IsValid(id)).WithMessage("Identificador de orientação inválido.")
            .OverridePropertyName("guidelineId");
        RuleFor(x => x.Period).IsInEnum().WithMessage("Período inválido.").OverridePropertyName("period");
        RuleFor(x => x.Division)
            .Must(d => d is null || Enum.IsDefined(d.Value)).WithMessage("Divisão inválida.").OverridePropertyName("division");
    }
}
