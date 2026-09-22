using System.Security.Cryptography;

namespace AguiaBranca.Domain.Common;

/// <summary>
/// Identificador de agregado no formato ObjectId (24 hex): 4 bytes de timestamp + 5 aleatórios + 3 de contador.
/// Gerado no Domain (sem depender do driver MongoDB); a Infrastructure o persiste como ObjectId nativo.
/// </summary>
public static class EntityId
{
    private static readonly byte[] Random5 = RandomNumberGenerator.GetBytes(5);
    private static int _counter = RandomNumberGenerator.GetInt32(0, 0x1000000);

    public static string New() => New(DateTimeOffset.UtcNow);

    public static string New(DateTimeOffset at)
    {
        Span<byte> bytes = stackalloc byte[12];
        var seconds = (int)at.ToUnixTimeSeconds();
        bytes[0] = (byte)(seconds >> 24);
        bytes[1] = (byte)(seconds >> 16);
        bytes[2] = (byte)(seconds >> 8);
        bytes[3] = (byte)seconds;
        Random5.CopyTo(bytes[4..9]);
        var counter = Interlocked.Increment(ref _counter) & 0xFFFFFF;
        bytes[9] = (byte)(counter >> 16);
        bytes[10] = (byte)(counter >> 8);
        bytes[11] = (byte)counter;
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 24) return false;
        foreach (var c in value)
        {
            var hex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!hex) return false;
        }
        return true;
    }
}
