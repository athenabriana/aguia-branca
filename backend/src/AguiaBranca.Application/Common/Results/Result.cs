namespace AguiaBranca.Application.Common.Results;

/// <summary>Retorno vazio para resultados sem valor.</summary>
public readonly record struct Unit;

public sealed class Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Result de falha não possui valor.");

    public Error FirstError => Errors.Count > 0
        ? Errors[0]
        : throw new InvalidOperationException("Result de sucesso não possui erro.");

    private Result(T value) { IsSuccess = true; _value = value; Errors = []; }
    private Result(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0) throw new ArgumentException("Uma falha precisa de ao menos um erro.", nameof(errors));
        IsSuccess = false;
        Errors = errors;
    }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(Error error) => new([error]);
    public static Result<T> Fail(IEnumerable<Error> errors) => new(errors.ToArray());

    public static implicit operator Result<T>(Error error) => Fail(error);

    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess ? Result<TOut>.Ok(map(_value!)) : Result<TOut>.Fail(Errors);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IReadOnlyList<Error>, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Errors);
}

public static class Result
{
    public static Result<Unit> Ok() => Result<Unit>.Ok(default);
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);
    public static Result<T> Fail<T>(Error error) => Result<T>.Fail(error);
    public static Result<T> Fail<T>(IEnumerable<Error> errors) => Result<T>.Fail(errors);
}
