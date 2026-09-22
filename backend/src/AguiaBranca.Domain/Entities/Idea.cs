using AguiaBranca.Domain.Common;
using AguiaBranca.Domain.Enums;
using AguiaBranca.Domain.Exceptions;
using AguiaBranca.Domain.Rules;
using AguiaBranca.Domain.ValueObjects;

namespace AguiaBranca.Domain.Entities;

/// <summary>
/// Ideia de inovação. Máquina de estados:
/// SUBMETIDA → EM_ANALISE (ao salvar ICE) → APROVADA → IMPLEMENTADA; SUBMETIDA/EM_ANALISE → REJEITADA.
/// </summary>
public sealed class Idea
{
    public const int TitleMin = 3, TitleMax = 120, DescriptionMax = 2000, CategoryMin = 2, CategoryMax = 40, CommentMax = 1000;

    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public Division Division { get; private set; }
    public string? GuidelineId { get; private set; }
    public string AuthorId { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public IdeaStatus Status { get; private set; }
    public Ice? Ice { get; private set; }
    public string? ReviewerId { get; private set; }
    public string? ReviewComment { get; private set; }
    public string? LegacyId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    private Idea() { }

    public bool HasStrategicLink => GuidelineId is not null;
    public bool IsEditableByAuthor => Status == IdeaStatus.SUBMETIDA;
    public bool IsApprovedOrLater => Status is IdeaStatus.APROVADA or IdeaStatus.IMPLEMENTADA;
    public bool IsOpenForReview => Status is IdeaStatus.SUBMETIDA or IdeaStatus.EM_ANALISE;

    /// <summary>Pontos creditados na criação (+10, +5 com orientação).</summary>
    public int CreationPoints => PointsRules.ForCreation(HasStrategicLink);

    public static Idea Create(
        string title, string description, string category, Division division, string? guidelineId,
        string authorId, string authorName, DateTime now, string? legacyId = null)
    {
        var idea = new Idea
        {
            Id = EntityId.New(),
            AuthorId = authorId,
            AuthorName = authorName,
            Status = IdeaStatus.SUBMETIDA,
            LegacyId = legacyId,
            CreatedAt = now
        };
        idea.ApplyContent(title, description, category, division, guidelineId, now);
        return idea;
    }

    /// <summary>trim + primeira letra maiúscula; 2–40 caracteres (R-03.10).</summary>
    public static string NormalizeCategory(string? category)
    {
        var value = (category ?? string.Empty).Trim();
        if (value.Length is < CategoryMin or > CategoryMax)
            throw DomainException.Validation(nameof(category), $"Categoria deve ter de {CategoryMin} a {CategoryMax} caracteres.");
        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    public void EditContent(string title, string description, string category, Division division, string? guidelineId, DateTime now)
    {
        if (!IsEditableByAuthor)
            throw new DomainException(DomainErrorCodes.IdeaNotEditable, "A ideia só pode ser editada enquanto estiver SUBMETIDA.");
        ApplyContent(title, description, category, division, guidelineId, now);
    }

    /// <summary>Exclusão só é permitida em SUBMETIDA.</summary>
    public void EnsureDeletable()
    {
        if (!IsEditableByAuthor)
            throw new DomainException(DomainErrorCodes.IdeaNotEditable, "A ideia só pode ser excluída enquanto estiver SUBMETIDA.");
    }

    /// <summary>Salva o ICE; SUBMETIDA passa automaticamente para EM_ANALISE (R-03.12).</summary>
    public void SaveIce(Ice ice, string reviewerId, DateTime now)
    {
        EnsureOpenForReview();
        Ice = ice ?? throw new ArgumentNullException(nameof(ice));
        ReviewerId = reviewerId;
        if (Status == IdeaStatus.SUBMETIDA) Status = IdeaStatus.EM_ANALISE;
        UpdatedAt = now;
    }

    /// <summary>
    /// Aprova a ideia. Retorna <c>true</c> se o estado mudou e <c>false</c> se já estava aprovada/implementada
    /// (idempotente). O autor nunca pode aprovar a própria ideia.
    /// </summary>
    public bool Approve(string reviewerId, DateTime now)
    {
        if (reviewerId == AuthorId)
            throw new DomainException(DomainErrorCodes.SelfApprovalForbidden, "Você não pode aprovar a própria ideia.");
        if (IsApprovedOrLater) return false;

        EnsureOpenForReview();
        Status = IdeaStatus.APROVADA;
        ReviewerId = reviewerId;
        ReviewedAt = now;
        UpdatedAt = now;
        return true;
    }

    public void Reject(string reviewerId, string comment, DateTime now)
    {
        comment = (comment ?? string.Empty).Trim();
        if (comment.Length == 0)
            throw DomainException.Validation(nameof(comment), "O comentário é obrigatório para rejeitar.");
        if (comment.Length > CommentMax)
            throw DomainException.Validation(nameof(comment), $"O comentário deve ter no máximo {CommentMax} caracteres.");

        EnsureOpenForReview();
        Status = IdeaStatus.REJEITADA;
        ReviewerId = reviewerId;
        ReviewComment = comment;
        ReviewedAt = now;
        UpdatedAt = now;
    }

    /// <summary>APROVADA → IMPLEMENTADA. Idempotente: <c>false</c> quando não há mudança.</summary>
    public bool MarkImplemented(DateTime now)
    {
        if (Status != IdeaStatus.APROVADA) return false;
        Status = IdeaStatus.IMPLEMENTADA;
        UpdatedAt = now;
        return true;
    }

    private void EnsureOpenForReview()
    {
        if (!IsOpenForReview)
            throw new DomainException(DomainErrorCodes.IdeaInvalidState,
                $"A ideia está {Status} e não aceita esta operação.");
    }

    private void ApplyContent(string title, string description, string category, Division division, string? guidelineId, DateTime now)
    {
        title = (title ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();

        if (title.Length is < TitleMin or > TitleMax)
            throw DomainException.Validation(nameof(title), $"Título deve ter de {TitleMin} a {TitleMax} caracteres.");
        if (description.Length > DescriptionMax)
            throw DomainException.Validation(nameof(description), $"Descrição deve ter no máximo {DescriptionMax} caracteres.");
        if (guidelineId is not null && !EntityId.IsValid(guidelineId))
            throw DomainException.Validation(nameof(guidelineId), "Identificador de orientação inválido.");

        Title = title;
        Description = description;
        Category = NormalizeCategory(category);
        Division = division;
        GuidelineId = guidelineId;
        UpdatedAt = now;
    }
}
