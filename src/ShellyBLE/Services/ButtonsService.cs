namespace ShellyBLE.Services;

public sealed record PressNotification(ButtonItem Button, ButtonPress Press);

/// <summary>
/// Relie le scan BLE au <see cref="ButtonMonitor"/> et tient à jour la liste observable des boutons.
/// Toutes les mises à jour se font sur le thread UI.
/// </summary>
public sealed class ButtonsService
{
    readonly BleScanner scanner;
    readonly ButtonMonitor monitor;
    readonly IButtonSettingsStore store;
    readonly LinkOpener links;
    readonly ILogger<ButtonsService> logger;
    readonly Dictionary<Guid, ButtonItem> items = [];

    public ButtonsService(BleScanner scanner, ButtonMonitor monitor, IButtonSettingsStore store, LinkOpener links, ILogger<ButtonsService> logger)
    {
        this.scanner = scanner;
        this.monitor = monitor;
        this.store = store;
        this.links = links;
        this.logger = logger;

        foreach (var saved in store.All)
        {
            if (Guid.TryParse(saved.Id, out var id))
                ApplySettings(GetOrAdd(id), saved);
        }

        scanner.Advertised += (_, e) => MainThread.BeginInvokeOnMainThread(() => OnAdvertised(e));
    }

    public ObservableCollection<ButtonItem> Buttons { get; } = [];

    /// <summary>Chaque appui nouveau, sur le thread UI (mode affichage).</summary>
    public event EventHandler<PressNotification>? PressReceived;

    public ButtonItem? Find(Guid id) => items.GetValueOrDefault(id);

    public ButtonSettings? GetSettings(ButtonItem item) => store.Find($"{item.Id}");

    public async Task SaveAsync(ButtonItem item, ButtonSettings settings)
    {
        var known = store.Find(settings.Id)?.ParsedMac;
        await store.SaveAsync(settings);
        ApplySettings(item, settings);
        if (settings.ParsedMac != known)
            await scanner.RestartAsync();
    }

    public async Task ForgetAsync(ButtonItem item)
    {
        await store.RemoveAsync($"{item.Id}");
        item.IsSaved = false;
        item.CustomName = null;
    }

    void OnAdvertised(AdvertisementReceived e)
    {
        if (monitor.Process(e.Device.Id, e.Advertisement) is not { } observation)
            return;

        var item = GetOrAdd(observation.DeviceId);
        item.Device = e.Device;
        item.Rssi = e.Device.Rssi;
        item.MarkSeen(DateTime.Now);
        item.AdvertisedName = observation.LocalName ?? item.AdvertisedName;
        item.Model = observation.Model ?? item.Model;
        item.Mac = observation.Mac ?? item.Mac;
        item.IsInPairingMode = observation.IsInPairingMode ?? item.IsInPairingMode;

        switch (observation.Reading)
        {
            case ButtonReading reading:
                Apply(item, reading);
                break;
            case BTHomeError error:
                item.IsEncrypted |= error.Kind is BTHomeErrorKind.KeyRequired or BTHomeErrorKind.DecryptionFailed;
                if (item.Status != error.Message)
                    logger.LogInformation("{Mac} : {Error}", item.Mac, error.Message);
                item.Status = error.Message;
                break;
        }
    }

    void Apply(ButtonItem item, ButtonReading reading)
    {
        item.Status = null;
        item.IsEncrypted = reading.IsEncrypted;
        item.Battery = reading.Battery ?? item.Battery;

        foreach (var press in reading.Presses)
        {
            logger.LogInformation("Appui {Mac} : {Text} (chiffré : {Encrypted}, batterie : {Battery}, action : {Action})", item.Mac, press.Text, reading.IsEncrypted, reading.Battery, press.Action?.Summary);
            item.AddPress(PressVisual.Of(press.Event), press.Text);
            PressReceived?.Invoke(this, new(item, press));
        }

        if (reading.Presses.Any(p => p.Action is not null))
            _ = RunActionsAsync(item, reading);
    }

    async Task RunActionsAsync(ButtonItem item, ButtonReading reading)
    {
        foreach (var result in await monitor.RunActionsAsync(reading))
        {
            logger.LogInformation("Action {Mac} : {Message}", item.Mac, result.Message);
            await RecordAsync(item, result, item.DisplayName);
        }
    }

    /// <summary>Journalise le résultat d'une action ; ouvre le lien d'une action « Ouvrir un lien ».</summary>
    public async Task RecordAsync(ButtonItem item, ActionResult result, string title)
    {
        switch (result)
        {
            case ActionSucceeded success:
                item.AddActionResult(ActivityType.ActionSucceeded, "Action exécutée", success.Message);
                break;
            case ActionFailed failure:
                item.AddActionResult(ActivityType.ActionFailed, "Échec de l'action", failure.Reason);
                break;
            case LinkToOpen link:
                var opened = await links.OpenAsync(link.Uri, title);
                item.AddActionResult(opened ? ActivityType.LinkOpened : ActivityType.ActionFailed,
                    opened ? (LinkOpener.IsInForeground ? "Lien ouvert" : "Lien proposé en notification") : "Lien impossible à ouvrir",
                    $"{link.Uri}");
                break;
        }
    }

    static void ApplySettings(ButtonItem item, ButtonSettings settings)
    {
        item.IsSaved = true;
        item.CustomName = settings.Name;
        item.Mac = settings.ParsedMac ?? item.Mac;
    }

    ButtonItem GetOrAdd(Guid id)
    {
        if (!items.TryGetValue(id, out var item))
        {
            items[id] = item = new(id);
            Buttons.Add(item);
        }
        return item;
    }
}
