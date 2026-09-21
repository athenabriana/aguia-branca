using System.Linq.Expressions;
using AguiaBranca.Application.Common.Paging;
using AguiaBranca.Domain.Entities;
using AguiaBranca.Domain.Enums;
using FluentValidation;

namespace AguiaBranca.Application.Features.Projects;

internal static class ProjectRules
{
    public const decimal MaxMoney = 1_000_000_000_000m;   // 1 trilhão: teto contra valores absurdos/erros de digitação
    public const decimal MaxPercent = 10_000m;
    public const int NoteMax = 500;

    public static void Common<T>(
        AbstractValidator<T> v,
        Expression<Func<T, string?>> title, Expression<Func<T, string?>> description, Expression<Func<T, string?>> statusText,
        Expression<Func<T, Division?>> division, Expression<Func<T, ProjectStage?>> stage)
    {
        v.RuleFor(title).Cascade(CascadeMode.Stop)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Título é obrigatório.").OverridePropertyName("title")
            .Must(t => t!.Trim().Length <= Project.TitleMax).WithMessage($"Título deve ter no máximo {Project.TitleMax} caracteres.").OverridePropertyName("title");
        v.RuleFor(description).Must(d => (d ?? string.Empty).Trim().Length <= Project.DescriptionMax)
            .WithMessage($"Descrição deve ter no máximo {Project.DescriptionMax} caracteres.").OverridePropertyName("description");
        v.RuleFor(statusText).Must(d => (d ?? string.Empty).Trim().Length <= Project.StatusTextMax)
            .WithMessage($"Status deve ter no máximo {Project.StatusTextMax} caracteres.").OverridePropertyName("statusText");
        v.RuleFor(division).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Divisão é obrigatória.").OverridePropertyName("division")
            .Must(d => Enum.IsDefined(d!.Value)).WithMessage("Divisão inválida.").OverridePropertyName("division");
        v.RuleFor(stage).Must(s => s is null || Enum.IsDefined(s.Value)).WithMessage("Estágio inválido.").OverridePropertyName("stage");
    }

    public static void Amount<T>(AbstractValidator<T> v, Expression<Func<T, decimal?>> selector, string field, string label, decimal max, bool required)
    {
        if (required)
            v.RuleFor(selector).NotNull().WithMessage($"{label} é obrigatório(a).").OverridePropertyName(field);
        v.RuleFor(selector)
            .Must(x => x is null || x >= 0).WithMessage($"{label} não pode ser negativo(a).").OverridePropertyName(field)
            .Must(x => x is null || x <= max).WithMessage($"{label} excede o limite permitido.").OverridePropertyName(field);
    }
}

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        ProjectRules.Common(this, x => x.Title, x => x.Description, x => x.StatusText, x => x.Division, x => x.Stage);
        ProjectRules.Amount(this, x => x.Investment, "investment", "Investimento", ProjectRules.MaxMoney, required: false);
        ProjectRules.Amount(this, x => x.FinancialReturn, "financialReturn", "Retorno financeiro", ProjectRules.MaxMoney, required: false);
        ProjectRules.Amount(this, x => x.CostReduction, "costReduction", "Redução de custo", ProjectRules.MaxMoney, required: false);
        ProjectRules.Amount(this, x => x.ProductivityGain, "productivityGain", "Ganho de produtividade", ProjectRules.MaxPercent, required: false);
    }
}

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        ProjectRules.Common(this, x => x.Title, x => x.Description, x => x.StatusText, x => x.Division, x => x.Stage);
        RuleFor(x => x.Stage).NotNull().WithMessage("Estágio é obrigatório.").OverridePropertyName("stage");
        ProjectRules.Amount(this, x => x.Investment, "investment", "Investimento", ProjectRules.MaxMoney, required: true);
        ProjectRules.Amount(this, x => x.FinancialReturn, "financialReturn", "Retorno financeiro", ProjectRules.MaxMoney, required: true);
        ProjectRules.Amount(this, x => x.CostReduction, "costReduction", "Redução de custo", ProjectRules.MaxMoney, required: true);
        ProjectRules.Amount(this, x => x.ProductivityGain, "productivityGain", "Ganho de produtividade", ProjectRules.MaxPercent, required: true);
        RuleFor(x => x.Note).Must(n => (n ?? string.Empty).Trim().Length <= ProjectRules.NoteMax)
            .WithMessage($"A nota deve ter no máximo {ProjectRules.NoteMax} caracteres.").OverridePropertyName("note");
        RuleFor(x => x.Version).Must(v => v is null or >= 1).WithMessage("version deve ser >= 1.").OverridePropertyName("version");
    }
}

public sealed class ListProjectsQueryValidator : AbstractValidator<ListProjectsQuery>
{
    public ListProjectsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page deve ser >= 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithMessage($"pageSize deve estar entre 1 e {PageRequest.MaxPageSize}.");
        RuleFor(x => x.Stage).Must(s => s is null || Enum.IsDefined(s.Value)).WithMessage("Estágio inválido.");
        RuleFor(x => x.Division).Must(d => d is null || Enum.IsDefined(d.Value)).WithMessage("Divisão inválida.");
    }
}

public sealed class ListProjectUpdatesQueryValidator : AbstractValidator<ListProjectUpdatesQuery>
{
    public ListProjectUpdatesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page deve ser >= 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithMessage($"pageSize deve estar entre 1 e {PageRequest.MaxPageSize}.");
    }
}
