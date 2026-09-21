using AguiaBranca.Domain.Enums;

namespace AguiaBranca.FirestoreMigrator.Transform;

// Registros intermediários: já validados e tipados, mas com as referências ainda como IDs do Firestore ("legacy").

public sealed record UserRec(string LegacyId, string Name, string Email, Role Role, Division Division, int Points, DateTime CreatedAt);

public sealed record GuidelineRec(
    string LegacyId, string Title, string Description, Pillar Pillar, string AuthorId, string AuthorName, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record IceRec(int Impact, int Confidence, int Ease);

public sealed record IdeaRec(
    string LegacyId, string Title, string Description, string Category, Division Division, string? GuidelineId,
    string AuthorId, string AuthorName, IdeaStatus Status, IceRec? Ice, string? ReviewerId, string? ReviewComment,
    DateTime CreatedAt, DateTime? ReviewedAt, DateTime UpdatedAt);

public sealed record ProjectRec(
    string LegacyId, string Title, string Description, ProjectStage Stage, string StatusText,
    decimal Investment, DateTime? TargetDate, decimal FinancialReturn, decimal ProductivityGain, decimal CostReduction,
    Division Division, string? GuidelineId, string CreatorManagerId, string? OriginatingIdeaId, int? PriorityScore,
    string? ReporterId, string? ReporterName, string? ResponsibleId, string? ResponsibleName, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record ChangeRec(string Field, FieldValueKind Kind, string? From, string? To);

public sealed record UpdateRec(
    string LegacyId, string ProjectLegacyId, string AuthorId, string AuthorName, string Note, IReadOnlyList<ChangeRec> Changes, DateTime CreatedAt);
