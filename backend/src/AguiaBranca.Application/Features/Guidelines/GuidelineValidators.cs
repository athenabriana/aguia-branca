using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Domain.Entities;
using FluentValidation;

namespace AguiaBranca.Application.Features.Guidelines;

internal static class GuidelineRules
{
    public static void Apply<T>(
        AbstractValidator<T> v,
        Func<T, string?> title, Func<T, string?> description, Func<T, Domain.Enums.Pillar?> pillar, Func<T, string?> campaign)
    {
        v.RuleFor(x => title(x)).Cascade(CascadeMode.Stop)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Título é obrigatório.").OverridePropertyName("title")
            .Must(t => t!.Trim().Length is >= Guideline.TitleMin and <= Guideline.TitleMax)
            .WithMessage($"Título deve ter de {Guideline.TitleMin} a {Guideline.TitleMax} caracteres.").OverridePropertyName("title");

        v.RuleFor(x => description(x))
            .Must(d => (d ?? string.Empty).Trim().Length <= Guideline.DescriptionMax)
            .WithMessage($"Descrição deve ter no máximo {Guideline.DescriptionMax} caracteres.").OverridePropertyName("description");

        v.RuleFor(x => pillar(x))
            .NotNull().WithMessage("Pilar é obrigatório.").OverridePropertyName("pillar")
            .IsInEnum().WithMessage("Pilar inválido.").OverridePropertyName("pillar");

        v.RuleFor(x => campaign(x))
            .Must(c => (c ?? string.Empty).Trim().Length <= Guideline.CampaignMax)
            .WithMessage($"Campanha deve ter no máximo {Guideline.CampaignMax} caracteres.").OverridePropertyName("campaign");
    }
}

public sealed class CreateGuidelineCommandValidator : AbstractValidator<CreateGuidelineCommand>
{
    public CreateGuidelineCommandValidator() => GuidelineRules.Apply(this, x => x.Title, x => x.Description, x => x.Pillar, x => x.Campaign);
}

public sealed class UpdateGuidelineCommandValidator : AbstractValidator<UpdateGuidelineCommand>
{
    public UpdateGuidelineCommandValidator() => GuidelineRules.Apply(this, x => x.Title, x => x.Description, x => x.Pillar, x => x.Campaign);
}

public sealed class ListGuidelinesQueryValidator : AbstractValidator<ListGuidelinesQuery>
{
    public ListGuidelinesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page deve ser >= 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithMessage($"pageSize deve estar entre 1 e {PageRequest.MaxPageSize}.");
    }
}

public sealed class GuidelineHistoryQueryValidator : AbstractValidator<GuidelineHistoryQuery>
{
    public GuidelineHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page deve ser >= 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithMessage($"pageSize deve estar entre 1 e {PageRequest.MaxPageSize}.");
        RuleFor(x => x.Category).IsInEnum().When(x => x.Category is not null).WithMessage("Categoria inválida.");
        RuleFor(x => x.Campaign).MaximumLength(Guideline.CampaignMax).WithMessage($"Campanha deve ter no máximo {Guideline.CampaignMax} caracteres.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("'from' não pode ser posterior a 'to'.").OverridePropertyName("from");
    }
}
