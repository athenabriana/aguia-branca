using AguiaBranca.Application.Common.Results;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Application.Common.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>Usuário autenticado da requisição (claims do JWT).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string Id { get; }
    string Name { get; }
    Role Role { get; }
    Division Division { get; }
}

/// <summary>Unidade de trabalho transacional (design §5.3, §8.3).</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Executa <paramref name="work"/> numa transação (commit ao final, rollback em qualquer exceção).
    /// Repete em erro transitório. Traduz violação de índice único em <c>DuplicateKeyException</c> e
    /// conflito de versão em <c>ConcurrencyConflictException</c>.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}

/// <summary>Caso de uso (comando ou consulta). Handlers são registrados por scan de assembly.</summary>
public interface IHandler<in TRequest, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken ct);
}

/// <summary>Fuso usado para "mês corrente" (ranking, badges, relatórios). Vem de <c>Reports:TimeZone</c>.</summary>
public interface ITimeZoneProvider
{
    TimeZoneInfo ReportTimeZone { get; }
}
