using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Application.Common.Results;

/// <summary>Converte <see cref="DomainException"/> (invariante violada) em <see cref="Error"/> tipado.</summary>
public static class DomainErrorMapper
{
    public static Error ToError(this DomainException ex) => ex.Code switch
    {
        DomainErrorCodes.ValidationError => Error.Validation(ex.Message, ex.Field),
        DomainErrorCodes.IdeaNotEditable or DomainErrorCodes.IdeaInvalidState or DomainErrorCodes.ConcurrencyConflict
            => Error.Conflict(ex.Code, ex.Message),
        DomainErrorCodes.SelfApprovalForbidden => Error.Forbidden(ex.Message, ex.Code),
        _ => Error.Unprocessable(ex.Code, ex.Message, ex.Field)
    };

    public static Result<T> ToResult<T>(this DomainException ex) => Result<T>.Fail(ex.ToError());
}
