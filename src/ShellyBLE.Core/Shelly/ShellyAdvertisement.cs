namespace ShellyBLE.Core.Shelly;

/// <summary>Structure AD brute : type (cf. Bluetooth Assigned Numbers) et contenu.</summary>
public readonly record struct AdRecord(byte Type, ImmutableArray<byte> Data)
{
    public const byte ShortenedLocalName = 0x08;
    public const byte CompleteLocalName = 0x09;
    public const byte ServiceData16 = 0x16;
    public const byte ManufacturerSpecific = 0xFF;
}

/// <summary>Ce qu'une annonce BLE contient d'utile pour un appareil Shelly BLU.</summary>
public sealed record ShellyAdvertisement(string? LocalName, ShellyManufacturerData? Manufacturer, ImmutableArray<byte>? BTHomePayload)
{
    public ShellyModel? Model => Manufacturer?.Model ?? ShellyModel.FromShortName(LocalName);

    public bool IsShelly => Manufacturer is not null || Model is not null;

    /// <summary>
    /// Second jeu d'annonces émis en parallèle par le firmware (OTA « in-place » Silabs) : nom « OTA »,
    /// adresse aléatoire distincte, données constructeur portant la vraie MAC. S'y connecter mène au
    /// chargeur d'amorçage, sans service Shelly : à ignorer.
    /// </summary>
    public bool IsOta => LocalName == "OTA";

    public static ShellyAdvertisement From(IEnumerable<AdRecord> records)
    {
        string? name = null;
        ShellyManufacturerData? manufacturer = null;
        ImmutableArray<byte>? bthome = null;

        foreach (var record in records)
        {
            switch (record.Type)
            {
                case AdRecord.CompleteLocalName:
                case AdRecord.ShortenedLocalName when name is null:
                    name = Encoding.UTF8.GetString(record.Data.AsSpan()).TrimEnd('\0');
                    break;
                case AdRecord.ManufacturerSpecific:
                    manufacturer ??= ShellyManufacturerData.TryParse(record.Data.AsSpan());
                    break;
                case AdRecord.ServiceData16 when IsBTHome(record.Data.AsSpan()):
                    bthome = record.Data[2..];
                    break;
            }
        }

        return new(name, manufacturer, bthome);
    }

    // L'UUID est en little-endian dans la trame (D2 FC), mais iOS le restitue en big-endian (FC D2).
    static bool IsBTHome(ReadOnlySpan<byte> data) =>
        data.Length >= 2 &&
        (BinaryPrimitives.ReadUInt16LittleEndian(data) == BTHomeParser.ServiceUuid ||
         BinaryPrimitives.ReadUInt16BigEndian(data) == BTHomeParser.ServiceUuid);
}
