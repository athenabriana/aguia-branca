namespace AguiaBranca.Domain.Exceptions;

/// <summary>Violação de invariante/regra de negócio. <see cref="Code"/> é estável e mapeado para HTTP na Application.</summary>
public class DomainException : Exception
{
    public string Code { get; }
    public string? Field { get; }

    public DomainException(string code, string message, string? field = null) : base(message)
    {
        Code = code;
        Field = field;
    }

    public static DomainException Validation(string field, string message) =>
        new(DomainErrorCodes.ValidationError, message, field);
}

public static class DomainErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string IdeaNotEditable = "IDEA_NOT_EDITABLE";
    public const string IdeaInvalidState = "IDEA_INVALID_STATE";
    public const string SelfApprovalForbidden = "SELF_APPROVAL_FORBIDDEN";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}
