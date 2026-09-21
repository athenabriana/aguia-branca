using AguiaBranca.Domain.Exceptions;

namespace AguiaBranca.Domain.ValueObjects;

/// <summary>Matriz ICE (Impacto × Confiança × Facilidade), cada dimensão inteira de 1 a 10.</summary>
public sealed class Ice : IEquatable<Ice>
{
    public const int Min = 1;
    public const int Max = 10;

    public int Impact { get; private set; }
    public int Confidence { get; private set; }
    public int Ease { get; private set; }
    /// <summary>Persistido (há índice por score).</summary>
    public int Score { get; private set; }

    private Ice() { }

    public Ice(int impact, int confidence, int ease)
    {
        Require(nameof(impact), impact);
        Require(nameof(confidence), confidence);
        Require(nameof(ease), ease);
        Impact = impact;
        Confidence = confidence;
        Ease = ease;
        Score = impact * confidence * ease;
    }

    public static bool IsValid(int impact, int confidence, int ease) =>
        InRange(impact) && InRange(confidence) && InRange(ease);

    private static bool InRange(int v) => v is >= Min and <= Max;

    private static void Require(string field, int value)
    {
        if (!InRange(value))
            throw DomainException.Validation(field, $"'{field}' deve ser um inteiro entre {Min} e {Max}.");
    }

    public bool Equals(Ice? other) =>
        other is not null && Impact == other.Impact && Confidence == other.Confidence && Ease == other.Ease;

    public override bool Equals(object? obj) => Equals(obj as Ice);
    public override int GetHashCode() => HashCode.Combine(Impact, Confidence, Ease);
}
