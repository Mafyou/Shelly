namespace ShellyBLE.Core.Shelly;

/// <summary>
/// Firmware Shelly BLU, lu dans la caractéristique Firmware Revision (0x2A26) sous la forme
/// « 20230223-150544/v1.0.2@e85f66ff » : date et heure de compilation, version, commit.
/// </summary>
public sealed partial record ShellyFirmware(string Raw, Version? Version, DateOnly? BuildDate)
{
    /// <summary>Dernière version publiée d'après le changelog Shelly BLU (1.0.23, août 2025).</summary>
    public static readonly Version Latest = new(1, 0, 23);

    /// <summary>Vitesse multi-clic : absente du 1.0.2 relevé, ajoutée en 1.0.11 d'après le changelog.</summary>
    public static readonly Version MulticlickSince = new(1, 0, 11);

    public bool IsOutdated => Version is { } version && version < Latest;

    public string Display => (Version, BuildDate) switch
    {
        ({ } version, { } date) => $"{version} ({date:yyyy-MM-dd})",
        ({ } version, null) => $"{version}",
        _ => Raw,
    };

    public static ShellyFirmware Parse(string raw)
    {
        var match = Pattern().Match(raw);
        if (!match.Success)
            return new(raw, null, null);

        return new(
            raw,
            Version.TryParse(match.Groups["version"].Value, out var version) ? version : null,
            DateOnly.TryParseExact(match.Groups["date"].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null);
    }

    [GeneratedRegex(@"^(?<date>\d{8})-\d{6}/v(?<version>\d+(?:\.\d+){1,3})")]
    private static partial Regex Pattern();
}
