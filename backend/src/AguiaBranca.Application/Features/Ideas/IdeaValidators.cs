using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using FluentValidation;

namespace AguiaBranca.Application.Features.Ideas;

internal static class IdeaRules
{
    public static void Apply<T>(
        AbstractValidator<T> v, Func<T, string?> title, Func<T, string?> description, Func<T, string?> category, Func<T, Division?> division)
    {
        v.RuleFor(x => title(x)).Cascade(CascadeMode.Stop)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Título é obrigatório.").OverridePropertyName("title")
            .Must(t => t!.Trim().Length is >= Idea.TitleMin and <= Idea.TitleMax)
            .WithMessage($"Título deve ter de {Idea.TitleMin} a {Idea.TitleMax} caracteres.").OverridePropertyName("title");

        v.RuleFor(x => description(x))
            .Must(d => (d ?? string.Empty).Trim().Length <= Idea.DescriptionMax)
            .WithMessage($"Descrição deve ter no máximo {Idea.DescriptionMax} caracteres.").OverridePropertyName("description");

        v.RuleFor(x => category(x)).Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Categoria é obrigatória.").OverridePropertyName("category")
            .Must(c => c!.Trim().Length is >= Idea.CategoryMin and <= Idea.CategoryMax)
            .WithMessage($"Categoria deve ter de {Idea.CategoryMin} a {Idea.CategoryMax} caracteres.").OverridePropertyName("category");

        v.RuleFor(x => division(x))
            .Must(d => d is null || Enum.IsDefined(d.Value)).WithMessage("Divisão inválida.").OverridePropertyName("division");
    }
}

public sealed class CreateIdeaCommandValidator : AbstractValidator<CreateIdeaCommand>
{
    public CreateIdeaCommandValidator() => IdeaRules.Apply(this, x => x.Title, x => x.Description, x => x.Category, x => x.Division);
}

public sealed class UpdateIdeaCommandValidator : AbstractValidator<UpdateIdeaCommand>
{
    public UpdateIdeaCommandValidator() => IdeaRules.Apply(this, x => x.Title, x => x.Description, x => x.Category, x => x.Division);
}

public sealed class ListIdeasQueryValidator : AbstractValidator<ListIdeasQuery>
{
    public ListIdeasQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page deve ser >= 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithMessage($"pageSize deve estar entre 1 e {PageRequest.MaxPageSize}.");
        RuleFor(x => x.Scope).Must(s => s is null || Enum.IsDefined(s.Value)).WithMessage("scope inválido (mine, curation ou all).");
        RuleFor(x => x.Status).Must(s => s is null || Enum.IsDefined(s.Value)).WithMessage("status inválido.");
        RuleFor(x => x.Division).Must(d => d is null || Enum.IsDefined(d.Value)).WithMessage("Divisão inválida.");
    }
}

public sealed class SaveIceCommandValidator : AbstractValidator<SaveIceCommand>
{
    public SaveIceCommandValidator()
    {
        Dimension(x => x.Impact, "impact", "Impacto");
        Dimension(x => x.Confidence, "confidence", "Confiança");
        Dimension(x => x.Ease, "ease", "Facilidade");
    }

    private void Dimension(System.Linq.Expressions.Expression<Func<SaveIceCommand, int?>> selector, string field, string label) =>
        RuleFor(selector).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage($"{label} é obrigatório(a).").OverridePropertyName(field)
            .InclusiveBetween(Domain.ValueObjects.Ice.Min, Domain.ValueObjects.Ice.Max)
            .WithMessage($"{label} deve ser um inteiro entre {Domain.ValueObjects.Ice.Min} e {Domain.ValueObjects.Ice.Max}.").OverridePropertyName(field);
}

public sealed class RejectIdeaCommandValidator : AbstractValidator<RejectIdeaCommand>
{
    public RejectIdeaCommandValidator() =>
        RuleFor(x => x.Comment).Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("O comentário é obrigatório para rejeitar.").OverridePropertyName("comment")
            .Must(c => c!.Trim().Length <= Idea.CommentMax).WithMessage($"O comentário deve ter no máximo {Idea.CommentMax} caracteres.").OverridePropertyName("comment");
}
