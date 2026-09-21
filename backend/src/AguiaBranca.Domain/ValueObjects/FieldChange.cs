using System.Globalization;
using AguiaBranca.Domain.Enums;

namespace AguiaBranca.Domain.ValueObjects;

/// <summary>
/// Diferença de um campo no histórico do projeto. Os valores são guardados em forma canônica (texto)
/// junto com o <see cref="Kind"/>, para a API devolver JSON tipado (número/texto/data) sem <c>object</c> no banco.
/// </summary>
public sealed class FieldChange
{
    public string Field { get; private set; } = string.Empty;
    public FieldValueKind Kind { get; private set; }
    public string? From { get; private set; }
    public string? To { get; private set; }

    private FieldChange() { }

    private FieldChange(string field, FieldValueKind kind, string? from, string? to)
    {
        Field = field;
        Kind = kind;
        From = from;
        To = to;
    }

    public static FieldChange Text(string field, string? from, string? to) =>
        new(field, FieldValueKind.TEXT, from, to);

    public static FieldChange Number(string field, decimal from, decimal to) =>
        new(field, FieldValueKind.NUMBER, FormatNumber(from), FormatNumber(to));

    public static FieldChange Date(string field, DateTime? from, DateTime? to) =>
        new(field, FieldValueKind.DATE, FormatDate(from), FormatDate(to));

    /// <summary>100m e 100.00m viram ambos "100".</summary>
    public static string FormatNumber(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    public static string? FormatDate(DateTime? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
