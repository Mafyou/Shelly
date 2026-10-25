namespace ShellyBLE.Core;

/// <summary>
/// Adresse Bluetooth 48 bits. <see cref="Value"/> suit l'ordre d'affichage :
/// l'octet de poids fort est le premier de « AA:BB:CC:DD:EE:FF ».
/// </summary>
public readonly record struct MacAddress(ulong Value)
{
    public const int Length = 6;

    public static MacAddress FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length)
            throw new ArgumentException($"Une adresse MAC fait {Length} octets.", nameof(bytes));

        Span<byte> buffer = stackalloc byte[8];
        bytes.CopyTo(buffer[2..]);
        return new(BinaryPrimitives.ReadUInt64BigEndian(buffer));
    }

    public void CopyTo(Span<byte> destination)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, Value);
        buffer[2..].CopyTo(destination);
    }

    public MacAddress Reversed()
    {
        Span<byte> bytes = stackalloc byte[Length];
        CopyTo(bytes);
        bytes.Reverse();
        return FromBytes(bytes);
    }

    /// <summary>
    /// Plugin.BLE (Android, Windows) encode l'adresse dans les 6 derniers octets de l'identifiant :
    /// 00000000-0000-0000-0000-aabbccddeeff. Sur iOS l'identifiant est opaque et on renvoie null.
    /// </summary>
    public static MacAddress? FromDeviceId(Guid deviceId)
    {
        var hex = $"{deviceId:N}";
        return hex.AsSpan(0, 20).ContainsAnyExcept('0') ? null : Parse(hex[20..]);
    }

    public static MacAddress Parse(string text) =>
        TryParse(text, out var mac) ? mac : throw new FormatException($"Adresse MAC invalide : « {text} ».");

    /// <summary>Accepte « AA:BB:CC:DD:EE:FF », « AA-BB-… » ou « AABBCCDDEEFF ».</summary>
    public static bool TryParse(string? text, out MacAddress mac)
    {
        mac = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var hex = text.Trim().Replace(":", "").Replace("-", "");
        if (hex.Length != Length * 2 || !ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;

        mac = new(value);
        return true;
    }

    public override string ToString()
    {
        Span<byte> b = stackalloc byte[Length];
        CopyTo(b);
        return $"{b[0]:X2}:{b[1]:X2}:{b[2]:X2}:{b[3]:X2}:{b[4]:X2}:{b[5]:X2}";
    }
}
