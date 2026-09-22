namespace AguiaBranca.Application.Common;

public static class DateTimeExtensions
{
    /// <summary>Datas sem fuso vindas da API ("2026-09-21") são tratadas como UTC; com fuso local, convertidas.</summary>
    public static DateTime? AsUtc(this DateTime? value) => value is null ? null : value.Value.AsUtc();

    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
