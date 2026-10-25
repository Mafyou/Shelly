namespace ShellyBLE.Core.Buttons;

/// <summary>Un appui nouveau (les répétitions de la même annonce sont écartées).</summary>
public sealed record ButtonPress(int Index, ButtonEvent Event, string Text, ButtonAction? Action);

/// <summary>Contenu BTHome décodé d'une annonce.</summary>
public sealed record ButtonReading(bool IsEncrypted, byte? Battery, Version? Firmware, ImmutableArray<ButtonPress> Presses);

public union BTHomeReading(ButtonReading, BTHomeError);

/// <summary>Ce que l'application doit afficher après une annonce.</summary>
public sealed record ButtonObservation(
    Guid DeviceId,
    string? LocalName,
    ShellyModel? Model,
    MacAddress? Mac,
    bool? IsInPairingMode,
    BTHomeReading? Reading);

/// <summary>
/// Transforme les annonces BLE en observations : filtre les boutons Shelly, déchiffre avec la clé
/// enregistrée, écarte les répétitions et associe à chaque appui l'action configurée.
/// </summary>
public sealed class ButtonMonitor(IButtonSettingsStore settings, IActionRunner actions)
{
    readonly PacketDeduplicator deduplicator = new();
    readonly Dictionary<Guid, DeviceState> devices = [];

    /// <summary>Ce qu'on a appris d'un appareil au fil de ses annonces (nom et MAC peuvent arriver séparément).</summary>
    sealed record DeviceState(string? Name, ShellyModel? Model, MacAddress? AdvertisedMac)
    {
        public static readonly DeviceState Empty = new(null, null, null);

        public DeviceState Learn(ShellyAdvertisement advertisement) => this with
        {
            Name = advertisement.LocalName ?? Name,
            Model = advertisement.Model ?? Model,
            AdvertisedMac = advertisement.Manufacturer?.Mac ?? AdvertisedMac,
        };
    }

    /// <returns>null si l'annonce ne provient pas d'un bouton Shelly ni d'un appareil enregistré.</returns>
    public ButtonObservation? Process(Guid deviceId, ShellyAdvertisement advertisement)
    {
        if (advertisement.IsOta)
            return null;

        var saved = settings.Find($"{deviceId}");
        if (!devices.TryGetValue(deviceId, out var state))
        {
            if (saved is null && !IsButton(advertisement))
                return null;
            state = DeviceState.Empty;
        }
        devices[deviceId] = state = state.Learn(advertisement);

        // La MAC annoncée par Shelly sert là où l'OS la masque (iOS, macOS).
        var mac = saved?.ParsedMac ?? MacAddress.FromDeviceId(deviceId) ?? state.AdvertisedMac;
        // Seules les données constructeur portent l'indicateur : sans elles, on ne sait pas.
        var pairing = advertisement.Manufacturer?.Features is { } features ? features.HasFlag(ShellyFeatures.PairingMode) : (bool?)null;
        var reading = advertisement.BTHomePayload is { } payload ? Read(deviceId, payload, saved, mac, state) : (BTHomeReading?)null;

        return new(deviceId, state.Name, state.Model, mac, pairing, reading);
    }

    /// <summary>Exécute, dans l'ordre, les actions associées aux appuis d'une lecture.</summary>
    public async Task<ImmutableArray<ActionResult>> RunActionsAsync(ButtonReading reading, CancellationToken stoppingToken = default)
    {
        var results = ImmutableArray.CreateBuilder<ActionResult>();
        foreach (var press in reading.Presses)
        {
            if (press.Action is { } action)
                results.Add(await actions.RunAsync(action, stoppingToken));
        }
        return results.ToImmutable();
    }

    BTHomeReading Read(Guid deviceId, ImmutableArray<byte> payload, ButtonSettings? saved, MacAddress? mac, DeviceState state)
    {
        var decryption = saved?.KeyBytes is { } key
            ? new BTHomeDecryption(key, [.. new[] { mac, state.AdvertisedMac }.OfType<MacAddress>().Distinct()])
            : null;

        return BTHomeParser.Parse(payload.AsSpan(), decryption) switch
        {
            BTHomePacket packet => ToReading(deviceId, packet, saved),
            BTHomeError error => error,
        };
    }

    ButtonReading ToReading(Guid deviceId, BTHomePacket packet, ButtonSettings? saved)
    {
        if (!deduplicator.IsNew($"{deviceId}", packet))
            return new(packet.Info.IsEncrypted, packet.Battery, packet.Firmware, []);

        ImmutableArray<ButtonPressed> buttons = [.. packet.Buttons];
        ImmutableArray<ButtonPress> presses =
        [
            .. buttons
                .Where(b => b.Event != ButtonEvent.None)
                .Select(b => new ButtonPress(
                    b.Index,
                    b.Event,
                    buttons.Length > 1 ? $"Bouton {b.Index + 1} : {b.Event.Label}" : b.Event.Label,
                    // Les actions sont définies par type d'appui, pour le premier bouton uniquement.
                    b.Index == 0 ? ActionFor(saved, b.Event) : null)),
        ];

        return new(packet.Info.IsEncrypted, packet.Battery, packet.Firmware, presses);
    }

    /// <summary>GetValueOrDefault rendrait une union vide (struct par défaut) au lieu de null.</summary>
    static ButtonAction? ActionFor(ButtonSettings? saved, ButtonEvent buttonEvent) =>
        saved is not null && saved.Actions.TryGetValue(buttonEvent, out var action) ? (ButtonAction?)action : null;

    static bool IsButton(ShellyAdvertisement advertisement) =>
        advertisement.Model?.IsButton == true
        // Appareil Allterco d'un modèle inconnu de cette version : on le garde.
        || (advertisement.Manufacturer is not null && advertisement.Model is null)
        || (advertisement.BTHomePayload is { } payload && BTHomeParser.Parse(payload.AsSpan()) is BTHomePacket packet && packet.Buttons.Any());
}
