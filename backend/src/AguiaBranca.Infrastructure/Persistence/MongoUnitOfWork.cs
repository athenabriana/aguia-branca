using System.Text.RegularExpressions;
using AguiaBranca.Application.Common.Abstractions;
using AguiaBranca.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AguiaBranca.Infrastructure.Persistence;

/// <summary>
/// Unidade de trabalho sobre o <see cref="AppDbContext"/>. Requer replica set (transações multi-documento).
/// Traduz exceções do driver/EF em exceções da Application, para que nada do MongoDB vaze para as camadas de cima.
/// </summary>
internal sealed partial class MongoUnitOfWork(AppDbContext db, ILogger<MongoUnitOfWork> logger) : IUnitOfWork
{
    /// <summary>
    /// Tentativas sob conflito de escrita. Com N escritores concorrentes no mesmo documento, um pode precisar de até N
    /// tentativas (cada tentativa reescreve e volta a competir), por isso o limite é folgado e há jitter.
    /// </summary>
    internal const int MaxAttempts = 8;

    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (Translate(ex) is { } translated)
        {
            db.ChangeTracker.Clear(); // a escrita falhou: o estado rastreado não é mais confiável
            throw translated;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        // Já dentro de uma transação: participa dela (sem transação aninhada).
        if (db.Database.CurrentTransaction is not null)
        {
            var nested = await work(ct);
            await SaveChangesAsync(ct);
            return nested;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var result = await work(ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < MaxAttempts)
            {
                logger.LogWarning("Conflito transitório na transação (tentativa {Attempt}/{Max}); repetindo.", attempt, MaxAttempts);
                db.ChangeTracker.Clear(); // descarta o estado da tentativa que falhou; o trabalho será reexecutado
                // jitter: evita que os concorrentes repitam em lockstep e colidam de novo
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 25) * attempt), ct);
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                // Orçamento esgotado: é contenção, não falha interna — vira conflito (409), nunca 500.
                db.ChangeTracker.Clear();
                logger.LogError(ex, "Transação abandonada após {Max} tentativas por conflito de escrita.", MaxAttempts);
                throw new ConcurrencyConflictException(ex);
            }
            catch (Exception ex) when (Translate(ex) is { } translated)
            {
                // Rollback já feito pelo dispose da transação; descarta as entidades rastreadas da tentativa que falhou
                // para o chamador poder reler o banco (ex.: aprovação concorrente perdeu a corrida).
                db.ChangeTracker.Clear();
                throw translated;
            }
        }
    }

    private static bool IsTransient(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
        {
            if (e is MongoException m &&
                (m.HasErrorLabel("TransientTransactionError") || m.HasErrorLabel("UnknownTransactionCommitResult")))
                return true;
            if (e.InnerException is null) break;
        }
        return false;
    }

    /// <summary>Devolve a exceção equivalente da Application, ou <c>null</c> se não houver tradução (relança a original).</summary>
    internal static Exception? Translate(Exception ex)
    {
        if (ex is DbUpdateConcurrencyException) return new ConcurrencyConflictException(ex);

        for (var e = ex; e is not null; e = e.InnerException!)
        {
            var duplicate = e switch
            {
                MongoBulkWriteException bulk => bulk.WriteErrors.Any(w => w.Category == ServerErrorCategory.DuplicateKey)
                    ? bulk.WriteErrors.First(w => w.Category == ServerErrorCategory.DuplicateKey).Message
                    : null,
                MongoWriteException write when write.WriteError.Category == ServerErrorCategory.DuplicateKey => write.WriteError.Message,
                _ => null
            };
            if (duplicate is not null) return new DuplicateKeyException(IndexName(duplicate), ex);
            if (e.InnerException is null) break;
        }

        return null;
    }

    private static string? IndexName(string message)
    {
        var match = IndexRegex().Match(message);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"index: (\S+)")]
    private static partial Regex IndexRegex();
}
