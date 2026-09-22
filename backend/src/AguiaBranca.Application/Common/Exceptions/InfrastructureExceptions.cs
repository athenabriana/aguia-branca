namespace AguiaBranca.Application.Common.Exceptions;

/// <summary>Violação de índice único, traduzida pela Infrastructure (o driver não vaza para cima).</summary>
public sealed class DuplicateKeyException(string? indexName = null, Exception? inner = null)
    : Exception($"Registro duplicado{(indexName is null ? string.Empty : $" ({indexName})")}.", inner)
{
    public string? IndexName { get; } = indexName;
}

/// <summary>Conflito de concorrência otimista detectado no armazenamento.</summary>
public sealed class ConcurrencyConflictException(Exception? inner = null)
    : Exception("O registro foi alterado por outra operação. Recarregue e tente novamente.", inner);
