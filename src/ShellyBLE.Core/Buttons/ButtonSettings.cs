namespace ShellyBLE.Core.Buttons;

/// <summary>Réglages d'un bouton conservés par l'application (nom, MAC, clé BTHome, actions).</summary>
public sealed record ButtonSettings
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Mac { get; init; }

    /// <summary>Clé AES-CCM en hexadécimal (32 caractères).</summary>
    public string? Key { get; init; }

    /// <summary>Action déclenchée par chaque type d'appui du premier bouton.</summary>
    public IReadOnlyDictionary<ButtonEvent, ButtonAction> Actions { get; init; } = FrozenDictionary<ButtonEvent, ButtonAction>.Empty;

    [JsonIgnore]
    public MacAddress? ParsedMac => MacAddress.TryParse(Mac, out var mac) ? mac : null;

    [JsonIgnore]
    public ImmutableArray<byte>? KeyBytes => ButtonSettingsForm.TryParseKey(Key, out var key) ? key : null;
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ImmutableArray<ButtonSettings>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
