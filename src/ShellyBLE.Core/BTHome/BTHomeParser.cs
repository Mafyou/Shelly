namespace ShellyBLE.Core.BTHome;

/// <summary>Octet d'information BTHome : bit 0 chiffrement, bit 2 annonce sur événement, bits 5-7 version.</summary>
public readonly record struct BTHomeDeviceInfo(byte Raw)
{
    public bool IsEncrypted => (Raw & 0x01) != 0;
    public bool IsTriggerBased => (Raw & 0x04) != 0;
    public int Version => Raw >> 5;
}

public sealed record BTHomePacket(BTHomeDeviceInfo Info, uint? Counter, ImmutableArray<BTHomeObject> Objects, bool IsComplete)
{
    public byte? PacketId => Objects.Select(o => o is PacketId p ? p.Value : (byte?)null).FirstOrDefault(v => v.HasValue);
    public byte? Battery => Objects.Select(o => o is Battery b ? b.Percent : (byte?)null).FirstOrDefault(v => v.HasValue);
    public Version? Firmware => Objects.Select(o => o is FirmwareVersion f ? f.Version : null).FirstOrDefault(v => v is not null);

    public IEnumerable<ButtonPressed> Buttons =>
        Objects.Select(o => o is ButtonPressed b ? b : null).OfType<ButtonPressed>();

    public IEnumerable<Measurement> Measurements =>
        Objects.Select(o => o is Measurement m ? m : null).OfType<Measurement>();
}

public enum BTHomeErrorKind
{
    Truncated,
    UnsupportedVersion,
    KeyRequired,
    DecryptionFailed,
}

public sealed record BTHomeError(BTHomeErrorKind Kind, string Message);

public union BTHomeResult(BTHomePacket, BTHomeError);

/// <summary>Clé AES et adresses MAC à essayer pour le nonce (l'ordre des octets annoncés varie selon la source).</summary>
public sealed record BTHomeDecryption(ImmutableArray<byte> Key, ImmutableArray<MacAddress> MacCandidates);

public static class BTHomeParser
{
    public const ushort ServiceUuid = 0xFCD2;

    public static readonly Guid ServiceGuid = new($"0000{ServiceUuid:x4}-0000-1000-8000-00805f9b34fb");

    /// <summary>Décode les données de service BTHome qui suivent l'UUID (l'octet d'info vient en premier).</summary>
    public static BTHomeResult Parse(ReadOnlySpan<byte> payload, BTHomeDecryption? decryption = null)
    {
        if (payload.IsEmpty)
            return new BTHomeError(BTHomeErrorKind.Truncated, "Annonce BTHome vide.");

        var info = new BTHomeDeviceInfo(payload[0]);
        if (info.Version != 2)
            return new BTHomeError(BTHomeErrorKind.UnsupportedVersion, $"BTHome v{info.Version} non pris en charge.");

        var data = payload[1..];
        if (!info.IsEncrypted)
            return ParseObjects(info, null, data);

        if (decryption is null)
            return new BTHomeError(BTHomeErrorKind.KeyRequired, "Annonce chiffrée : clé requise.");

        const int trailer = BTHomeCipher.CounterLength + BTHomeCipher.MicLength;
        if (data.Length < trailer)
            return new BTHomeError(BTHomeErrorKind.Truncated, "Annonce chiffrée tronquée.");

        var ciphertext = data[..^trailer];
        var counter = data.Slice(ciphertext.Length, BTHomeCipher.CounterLength);
        var mic = data[^BTHomeCipher.MicLength..];
        var plaintext = new byte[ciphertext.Length];

        foreach (var mac in decryption.MacCandidates)
        {
            var nonce = BTHomeCipher.BuildNonce(mac, info.Raw, counter);
            if (BTHomeCipher.TryDecrypt(decryption.Key.AsSpan(), nonce.AsSpan(), ciphertext, mic, plaintext))
                return ParseObjects(info, BinaryPrimitives.ReadUInt32LittleEndian(counter), plaintext);
        }

        return new BTHomeError(BTHomeErrorKind.DecryptionFailed, "Déchiffrement impossible : clé ou adresse MAC incorrecte.");
    }

    static BTHomePacket ParseObjects(BTHomeDeviceInfo info, uint? counter, ReadOnlySpan<byte> data)
    {
        var objects = ImmutableArray.CreateBuilder<BTHomeObject>();
        var buttonIndex = 0;
        var dimmerIndex = 0;

        while (!data.IsEmpty)
        {
            // La spécification demande d'arrêter au premier identifiant inconnu : sa taille l'est aussi.
            if (!BTHomeDefinitions.All.TryGetValue(data[0], out var definition)
                || definition.Type.Length(data[1..]) is not { } length
                || data.Length < 1 + length)
                return new(info, counter, objects.ToImmutable(), IsComplete: false);

            var value = data.Slice(1, length);
            objects.Add(definition.Id switch
            {
                0x00 => new PacketId(value[0]),
                0x01 => new Battery(value[0]),
                0x3A => new ButtonPressed(buttonIndex++, ToButtonEvent(value[0])),
                0x3B => new Command((BTHomeCommand)value[1], [.. value[2..]]),
                0x3C => new DimmerRotated(dimmerIndex++, (DimmerDirection)value[0], value[1]),
                0x50 => new Timestamp(DateTimeOffset.FromUnixTimeSeconds(definition.Type.ReadInteger(value))),
                0x53 => new TextValue(Encoding.UTF8.GetString(value[1..])),
                0x54 => new OtherObject(definition.Id, [.. value[1..]]),
                0xF0 => new DeviceTypeId(BinaryPrimitives.ReadUInt16LittleEndian(value)),
                0xF1 => new FirmwareVersion(new(value[3], value[2], value[1], value[0])),
                0xF2 => new FirmwareVersion(new(value[2], value[1], value[0])),
                _ when definition.Kind == BTHomeObjectKind.BinarySensor => new BinarySensor(definition.Id, definition.Name, value[0] != 0),
                _ => new Measurement(definition.Id, definition.Name, (double)(definition.Type.ReadInteger(value) * definition.Factor), definition.Unit),
            });
            data = data[(1 + length)..];
        }

        return new(info, counter, objects.ToImmutable(), IsComplete: true);
    }

    // Les firmwares Shelly antérieurs à 1.0.20 signalaient le maintien par 0xFE.
    static ButtonEvent ToButtonEvent(byte raw) => raw == 0xFE ? ButtonEvent.Hold : (ButtonEvent)raw;
}
