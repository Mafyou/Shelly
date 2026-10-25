namespace ShellyBLE.Core.Shelly;

[Flags]
public enum ShellyFeatures : ushort
{
    None = 0,
    Discoverable = 1 << 0,
    AuthEnabled = 1 << 1,
    RpcOverBle = 1 << 2,
    BuzzerEnabled = 1 << 3,
    PairingMode = 1 << 4,
}

public sealed record ShellyModel(ushort Id, string Name, string ShortName)
{
    public static readonly FrozenDictionary<ushort, ShellyModel> Known = new[]
    {
        new ShellyModel(0x0001, "Shelly BLU Button1", "SBBT-002C"),
        new ShellyModel(0x0002, "Shelly BLU DoorWindow", "SBDW-002C"),
        new ShellyModel(0x0003, "Shelly BLU HT", "SBHT-003C"),
        new ShellyModel(0x0005, "Shelly BLU Motion", "SBMO-003Z"),
        new ShellyModel(0x0006, "Shelly BLU Wall Switch 4", "SBBT-004CEU"),
        new ShellyModel(0x0007, "Shelly BLU RC Button 4", "SBBT-004CUS"),
        new ShellyModel(0x0008, "Shelly BLU TRV", "SBTR-001AEU"),
        new ShellyModel(0x0009, "Shelly BLU Remote", "SBRC-005B"),
        new ShellyModel(0x000B, "Shelly BLU Weather Station", "SBWS-90CM"),
        new ShellyModel(0x000C, "Shelly BLU H&T Display ZB", "SBHT-103C"),
    }.ToFrozenDictionary(m => m.Id);

    public static ShellyModel? FromShortName(string? name) =>
        name is null ? null : Known.Values.FirstOrDefault(m => name.StartsWith(m.ShortName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Famille « SBBT » : Button1, Wall Switch 4 et RC Button 4.</summary>
    public bool IsButton => ShortName.StartsWith("SBBT", StringComparison.Ordinal);
}

/// <summary>
/// Données constructeur Allterco (0x0BA9) : une suite de blocs « type (1 octet) + contenu ».
/// </summary>
public sealed record ShellyManufacturerData(ShellyFeatures? Features, MacAddress? Mac, ushort? ModelId)
{
    public const ushort AlltercoCompanyId = 0x0BA9;

    const byte FeaturesBlock = 0x01;
    const byte MacBlock = 0x0A;
    const byte ModelBlock = 0x0B;

    static readonly FrozenDictionary<byte, int> BlockSizes = new Dictionary<byte, int>
    {
        [FeaturesBlock] = 2,
        [MacBlock] = MacAddress.Length,
        [ModelBlock] = 2,
    }.ToFrozenDictionary();

    public ShellyModel? Model => ModelId is { } id ? ShellyModel.Known.GetValueOrDefault(id) : null;


    /// <param name="data">Contenu de l'enregistrement 0xFF, identifiant constructeur (little-endian) compris.</param>
    public static ShellyManufacturerData? TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2 || BinaryPrimitives.ReadUInt16LittleEndian(data) != AlltercoCompanyId)
            return null;

        ShellyFeatures? features = null;
        MacAddress? mac = null;
        ushort? model = null;

        var blocks = data[2..];
        while (!blocks.IsEmpty)
        {
            if (!BlockSizes.TryGetValue(blocks[0], out var size) || blocks.Length < 1 + size)
                break;

            var value = blocks.Slice(1, size);
            switch (blocks[0])
            {
                case FeaturesBlock: features = (ShellyFeatures)BinaryPrimitives.ReadUInt16LittleEndian(value); break;
                // Little-endian, comme l'adresse dans la trame radio (vérifié sur un BLU Button1).
                case MacBlock: mac = MacAddress.FromBytes(value).Reversed(); break;
                case ModelBlock: model = BinaryPrimitives.ReadUInt16LittleEndian(value); break;
            }
            blocks = blocks[(1 + size)..];
        }

        return new(features, mac, model);
    }
}
