namespace ShellyBLE.Core.Shelly;

/// <summary>
/// Service GATT de configuration des appareils Shelly BLU.
/// Les écritures exigent une liaison (bonding) : l'appareil doit être en mode appairage
/// (appui de plus de 10 s, LED bleue clignotante) lors de la première connexion.
/// </summary>
public static class ShellyGatt
{
    public static readonly Guid Service = new("de8a5aac-a99b-c315-0c80-60d4cbb51225");

    public static readonly Guid FactoryReset = new("b0a7e40f-2b87-49db-801c-eb3686a24bdb");
    public static readonly Guid Passkey = new("0ffb7104-860c-49ae-8989-1f946d5f6c03");
    public static readonly Guid BeaconMode = new("cb9e957e-952d-4761-a7e1-4416494a5bfa");
    public static readonly Guid EncryptionKey = new("eb0fb41b-af4b-4724-a6f9-974f55aba81a");
    public static readonly Guid UnixTime = new("d56a3410-115e-41d1-945b-3a7f189966a1");
    public static readonly Guid SampleBTHomeData = new("d52246df-98ac-4d21-be1b-70d5f66a5ddb");

    // Spécifiques au BLU Button1.
    public static readonly Guid ActivateBuzzer = new("5b026510-4088-c297-46d8-be6c736a087b");
    public static readonly Guid BuzzerEnabled = new("dd78bf35-7680-484e-ad86-1bc1e7738e14");
    public static readonly Guid MulticlickSpeed = new("35c8a5e2-1cac-4b03-8386-eb6846a1776e");

    // Relevé GATT d'un BLU Button1 en 1.0.2 : présents mais absents de la doc du bouton.

    /// <summary>Adresse du chargeur d'amorçage (jeu d'annonces « OTA »), lecture seule.</summary>
    public static readonly Guid ReconnectionAddress = Uuid16(0x2A03);

    /// <summary>Données de provisionnement cloud (255 octets, lecture seule), cf. doc Shelly du WS90.</summary>
    public static readonly Guid ProvisionDataLow = new("0c31d671-5262-4620-87f3-51851c367cec");
    public static readonly Guid ProvisionDataHigh = new("55bef871-f611-46f5-b498-7b9cc9759927");

    /// <summary>Service OTA Silicon Labs et sa caractéristique de contrôle.</summary>
    public static readonly Guid SilabsOtaService = new("1d14d6ee-fd63-4fa1-bfa4-8f47b42119f0");

    /// <summary>Y écrire redémarre le bouton dans son chargeur d'amorçage : à ne jamais toucher.</summary>
    public static readonly Guid SilabsOtaControl = new("f7bf3564-fb6d-4e53-88a4-5e37e0326063");

    public static readonly Guid DeviceInformationService = Uuid16(0x180A);
    public static readonly Guid FirmwareRevision = Uuid16(0x2A26);

    public const int MaxPasskey = 999_999;

    static Guid Uuid16(ushort value) => new($"0000{value:x4}-0000-1000-8000-00805f9b34fb");

    public static ImmutableArray<byte> EncodePasskey(int passkey)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(passkey);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(passkey, MaxPasskey);
        return UInt32(passkey);
    }

    public static ImmutableArray<byte> EncodeUnixTime(DateTimeOffset time) => UInt32(time.ToUnixTimeSeconds());

    static ImmutableArray<byte> UInt32(long value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)value);
        return ImmutableCollectionsMarshal.AsImmutableArray(bytes);
    }

    public static DateTimeOffset DecodeUnixTime(ReadOnlySpan<byte> bytes) =>
        DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32LittleEndian(bytes));

    public static ImmutableArray<byte> EncodeBool(bool value) => [value ? (byte)1 : (byte)0];
}

/// <summary>Délai maximal entre deux clics pour qu'ils comptent comme un multi-clic.</summary>
public enum MulticlickSpeed : byte
{
    Ms800 = 0,
    Ms550 = 1,
    Ms300 = 2,
    Ms150 = 3,
    Ms90 = 4,
    Ms55 = 5,
}

public static class MulticlickSpeedExtensions
{
    extension(MulticlickSpeed speed)
    {
        public int Milliseconds => speed switch
        {
            MulticlickSpeed.Ms800 => 800,
            MulticlickSpeed.Ms550 => 550,
            MulticlickSpeed.Ms300 => 300,
            MulticlickSpeed.Ms150 => 150,
            MulticlickSpeed.Ms90 => 90,
            MulticlickSpeed.Ms55 => 55,
            _ => throw new ArgumentOutOfRangeException(nameof(speed)),
        };
    }
}
