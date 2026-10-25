namespace ShellyBLE.ViewModels;

public enum DetailTab
{
    Activity,
    Programming,
    Device,
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
}

/// <summary>
/// Fiche d'un bouton : activité en direct, programmation des appuis, configuration de l'appareil par GATT.
/// </summary>
public sealed partial class ButtonDetailViewModel(ButtonsService buttons, BleScanner scanner, ILogger<PluginBleGattChannel> gattLogger)
    : ObservableObject, IQueryAttributable
{
    public const string IdParameter = "id";

    static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Plusieurs essais de connexion, chacun pouvant attendre 5 s la fin de la supervision.</summary>
    static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(45);

    static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    ShellyButtonClient? client;
    ButtonDeviceSettings? deviceSettings;
    bool showingSettings;

    public ImmutableArray<SpeedOption> Speeds => SpeedOption.All;

    public ObservableCollection<ProgramSlot> Programs { get; } = [];

    [ObservableProperty]
    public partial ButtonItem? Item { get; set; }

    // Onglets.

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsActivityTab), nameof(IsProgrammingTab), nameof(IsDeviceTab))]
    public partial DetailTab Tab { get; set; } = DetailTab.Activity;

    public bool IsActivityTab => Tab == DetailTab.Activity;

    public bool IsProgrammingTab => Tab == DetailTab.Programming;

    public bool IsDeviceTab => Tab == DetailTab.Device;

    [RelayCommand]
    void ShowTab(DetailTab tab) => Tab = tab;

    // Retour de la dernière opération.

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FeedbackText), nameof(HasFeedback), nameof(IsProblem))]
    public partial Feedback? Feedback { get; set; }

    public string? FeedbackText => Feedback?.Text;

    public bool HasFeedback => Feedback is not null;

    public bool IsProblem => Feedback?.IsProblem == true;

    // Connexion.

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsConnected), nameof(IsDisconnected), nameof(IsConnecting), nameof(ConnectionTitle), nameof(ConnectionHint), nameof(ConnectionIcon))]
    public partial ConnectionState Connection { get; set; }

    public bool IsConnected => Connection == ConnectionState.Connected;

    public bool IsDisconnected => Connection == ConnectionState.Disconnected;

    public bool IsConnecting => Connection == ConnectionState.Connecting;

    public string ConnectionIcon => Connection switch
    {
        ConnectionState.Connected => Icons.BluetoothConnect,
        ConnectionState.Connecting => Icons.Bluetooth,
        _ => Icons.BluetoothOff,
    };

    public string ConnectionTitle => Connection switch
    {
        ConnectionState.Connected => "Connecté",
        ConnectionState.Connecting => "Connexion…",
        _ => "Non connecté",
    };

    public string ConnectionHint => Connection switch
    {
        ConnectionState.Connected => "Les réglages s'appliquent dès qu'on les modifie.",
        ConnectionState.Connecting => "Gardez le bouton éveillé ; plusieurs essais possibles (jusqu'à 45 s).",
        _ => "Le bouton dort entre deux appuis : appuyez dessus juste avant. La première fois, maintenez-le 10 s (LED bleue) pour l'associer.",
    };

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    // Réglages de l'appareil.

    [ObservableProperty]
    public partial string? Firmware { get; set; }

    [ObservableProperty]
    public partial string? FirmwareWarning { get; set; }

    [ObservableProperty]
    public partial bool IsBeaconSupported { get; set; }

    [ObservableProperty]
    public partial bool IsBuzzerSupported { get; set; }

    [ObservableProperty]
    public partial bool IsSpeedSupported { get; set; }

    [ObservableProperty]
    public partial bool IsTimeSupported { get; set; }

    [ObservableProperty]
    public partial bool BeaconMode { get; set; }

    [ObservableProperty]
    public partial bool BuzzerEnabled { get; set; }

    [ObservableProperty]
    public partial SpeedOption? Speed { get; set; }

    [ObservableProperty]
    public partial string? DeviceTime { get; set; }

    // Sécurité et identité.

    [ObservableProperty]
    public partial string? Passkey { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(KeyPreview), nameof(HasKey))]
    public partial string? KeyText { get; set; }

    [ObservableProperty]
    public partial string? MacText { get; set; }

    public bool HasKey => !string.IsNullOrWhiteSpace(KeyText);

    public string KeyPreview => HasKey ? $"•••• •••• •••• {KeyText![^4..]}" : "Aucune clé enregistrée";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(IdParameter, out var value) && value is Guid id && buttons.Find(id) is { } item)
            Load(item);
    }

    /// <summary>Recharge la programmation (au retour de l'éditeur d'action).</summary>
    public void Reload()
    {
        if (Item is { } item)
            Load(item);
    }

    void Load(ButtonItem item)
    {
        Item = item;
        var saved = buttons.GetSettings(item);
        MacText = saved?.Mac ?? (item.Mac is { } mac ? $"{mac}" : null);
        KeyText = saved?.Key;

        Programs.Clear();
        foreach (var visual in PressVisual.Programmable)
            Programs.Add(new(visual, ActionFor(saved, visual.Event)));
    }

    /// <summary>Attention : GetValueOrDefault sur une union renverrait une union vide, pas null.</summary>
    internal static ButtonAction? ActionFor(ButtonSettings? settings, ButtonEvent buttonEvent) =>
        settings is not null && settings.Actions.TryGetValue(buttonEvent, out var action) ? (ButtonAction?)action : null;

    [RelayCommand]
    async Task EditProgramAsync(ProgramSlot slot)
    {
        if (Item is not { } item)
            return;
        await Shell.Current.GoToAsync(nameof(ActionEditorPage), new ShellNavigationQueryParameters
        {
            [ActionEditorViewModel.IdParameter] = item.Id,
            [ActionEditorViewModel.EventParameter] = slot.Visual.Event,
        });
    }

    [RelayCommand]
    async Task RenameAsync()
    {
        if (Item is not { } item)
            return;
        var name = await Shell.Current.DisplayPromptAsync("Nom du bouton", "Par exemple « Entrée » ou « Chevet ».",
            "Enregistrer", "Annuler", placeholder: item.Model?.Name, initialValue: item.CustomName ?? "", maxLength: 40);
        if (name is null)
            return;
        Feedback = await PersistAsync(settings => settings with { Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim() }, "Nom enregistré.");
    }

    [RelayCommand]
    async Task SaveIdentityAsync() =>
        Feedback = await PersistAsync(settings => settings with { Mac = MacText, Key = KeyText }, "Adresse et clé enregistrées.");

    [RelayCommand]
    async Task CopyKeyAsync()
    {
        if (!HasKey)
            return;
        await Clipboard.Default.SetTextAsync(KeyText);
        Feedback = new Info("Clé copiée dans le presse-papiers.");
    }

    [RelayCommand]
    async Task ForgetAsync()
    {
        if (Item is not { } item || !await ConfirmAsync("Oublier ce bouton ?", "Son nom, sa clé et sa programmation seront supprimés de l'application. Le bouton lui-même n'est pas modifié.", "Oublier"))
            return;

        await buttons.ForgetAsync(item);
        Load(item);
        Feedback = new Info("Bouton oublié.");
    }

    [RelayCommand]
    async Task ConnectAsync()
    {
        if (IsBusy || !IsDisconnected)
            return;
        if (Item?.Device is not { } device)
        {
            Feedback = new Problem("Bouton pas encore détecté : appuyez dessus, puis réessayez.");
            return;
        }

        Connection = ConnectionState.Connecting;
        Feedback = null;
        using var timeout = new CancellationTokenSource(ConnectTimeout);
        try
        {
            var channel = await PluginBleGattChannel.ConnectAsync(scanner.Adapter, device, gattLogger, timeout.Token);
            channel.ConnectionLost += OnConnectionLost;
            client = new(channel);
            Show(deviceSettings = await client.ReadSettingsAsync(timeout.Token));
            if (client is { } connected)
            {
                if (await connected.SyncTimeIfDriftedAsync(deviceSettings, timeout.Token) is { } synced)
                    DeviceTime = $"{synced.ToLocalTime():G}";
                // La page a pu se déconnecter pendant la mise à l'heure : ne pas ressusciter l'état connecté.
                if (ReferenceEquals(client, connected))
                {
                    Connection = ConnectionState.Connected;
                    _ = KeepAliveAsync(connected);
                }
            }
        }
        catch (Exception ex)
        {
            await CloseAsync();
            Feedback = new Problem($"Connexion impossible : {Describe(ex)}");
        }
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        await CloseAsync();
        if (Feedback is Info)
            Feedback = null;
    }

    // Réglages appliqués immédiatement.

    partial void OnBeaconModeChanged(bool value) => ApplySoon();

    partial void OnBuzzerEnabledChanged(bool value) => ApplySoon();

    partial void OnSpeedChanged(SpeedOption? value) => ApplySoon();

    void ApplySoon()
    {
        if (!showingSettings && IsConnected)
            ApplySettingsCommand.Execute(null);
    }

    [RelayCommand]
    Task ApplySettingsAsync() => RunAsync("Enregistrement sur le bouton…", async (device, stoppingToken) =>
    {
        if (deviceSettings is not { } current)
            return new Problem("Réglages du bouton inconnus : reconnectez-vous.");

        var desired = current with
        {
            BeaconMode = current.BeaconMode is null ? null : BeaconMode,
            BuzzerEnabled = current.BuzzerEnabled is null ? null : BuzzerEnabled,
            MulticlickSpeed = current.MulticlickSpeed is null ? null : Speed?.Value,
        };
        await device.ApplySettingsAsync(current, desired, stoppingToken);
        deviceSettings = desired;
        return new Info("Réglage enregistré sur le bouton.");
    });

    [RelayCommand]
    Task RingAsync() => RunAsync("Sonnerie…", async (device, stoppingToken) =>
    {
        if (deviceSettings is not { BeaconMode: true, BuzzerEnabled: true })
            return new Problem("Activez le mode balise et le buzzer pour faire sonner le bouton.");

        await device.SetBuzzerActiveAsync(true, stoppingToken);
        return new Info("Le bouton sonne (30 s maximum).");
    });

    [RelayCommand]
    Task StopRingAsync() => RunAsync("Arrêt de la sonnerie…", async (device, stoppingToken) =>
    {
        await device.SetBuzzerActiveAsync(false, stoppingToken);
        return new Info("Sonnerie arrêtée.");
    });

    [RelayCommand]
    Task SyncTimeAsync() => RunAsync("Mise à l'heure…", async (device, stoppingToken) =>
    {
        DeviceTime = $"{(await device.SyncTimeAsync(stoppingToken)).ToLocalTime():G}";
        return new Info("Horloge du bouton mise à l'heure.");
    });

    [RelayCommand]
    async Task EnableEncryptionAsync()
    {
        if (!int.TryParse(Passkey, out var pin) || pin is < 0 or > ShellyGatt.MaxPasskey)
        {
            Feedback = new Problem("Code PIN : de 1 à 6 chiffres.");
            return;
        }
        if (!await ConfirmAsync("Activer le chiffrement ?",
                $"Le code {pin:D6} sera demandé pour associer le bouton. Seule une réinitialisation usine permet de revenir en arrière.", "Activer"))
            return;

        await RunAsync("Activation du chiffrement…", async (device, stoppingToken) =>
            await SaveKeyAsync(await device.EnableEncryptionAsync(pin, stoppingToken), $"Chiffrement activé, clé enregistrée. Notez le code {pin:D6}."));
    }

    [RelayCommand]
    Task ReadKeyAsync() => RunAsync("Lecture de la clé…", async (device, stoppingToken) =>
        await SaveKeyAsync(await device.ReadEncryptionKeyAsync(stoppingToken), "Clé lue sur le bouton et enregistrée."));

    [RelayCommand]
    async Task FactoryResetAsync()
    {
        if (!await ConfirmAsync("Réinitialisation usine ?",
                "Le bouton oublie ses associations et désactive le chiffrement. Pensez ensuite à l'oublier dans les réglages Bluetooth du téléphone.", "Réinitialiser"))
            return;

        await RunAsync("Réinitialisation…", async (device, stoppingToken) =>
        {
            await device.FactoryResetAsync(stoppingToken);
            await CloseAsync();
            KeyText = null;
            return await PersistAsync(settings => settings with { Key = null }, "Bouton réinitialisé, clé effacée.");
        });
    }

    async Task<Feedback> SaveKeyAsync(ImmutableArray<byte> key, string message)
    {
        KeyText = Convert.ToHexStringLower(key.AsSpan());
        return await PersistAsync(settings => settings with { Key = KeyText }, message);
    }

    /// <summary>Valide puis enregistre les réglages de l'application modifiés par <paramref name="change"/>.</summary>
    async Task<Feedback> PersistAsync(Func<ButtonSettings, ButtonSettings> change, string success)
    {
        if (Item is not { } item)
            return new Problem("Bouton introuvable.");

        var current = change(buttons.GetSettings(item) ?? new ButtonSettings { Id = $"{item.Id}" });
        var form = new ButtonSettingsForm(current.Id, current.Name, current.Mac, current.Key,
            current.Actions.ToFrozenDictionary(pair => pair.Key, pair => (ButtonAction?)pair.Value));
        switch (form.Validate())
        {
            case ButtonSettings settings:
                await buttons.SaveAsync(item, settings);
                Load(item);
                return new Info(success);
            case SettingsErrors errors:
                return new Problem(string.Join(Environment.NewLine, errors.Messages));
            default:
                return new Problem("Réglages invalides.");
        }
    }

    async Task RunAsync(string progress, Func<ShellyButtonClient, CancellationToken, Task<Feedback>> operation)
    {
        if (client is not { } device || IsBusy)
            return;

        IsBusy = true;
        Feedback = new Info(progress);
        using var timeout = new CancellationTokenSource(OperationTimeout);
        try
        {
            Feedback = await operation(device, timeout.Token);
        }
        catch (Exception ex)
        {
            Feedback = new Problem(Describe(ex));
        }
        finally
        {
            IsBusy = false;
        }
    }

    void Show(ButtonDeviceSettings settings)
    {
        showingSettings = true;
        Firmware = settings.Firmware?.Display ?? "inconnu";
        FirmwareWarning = settings.Firmware is { IsOutdated: true } firmware
            ? $"Firmware {firmware.Display} ancien (dernier : {ShellyFirmware.Latest}). Mettez-le à jour avec l'application officielle Shelly."
            : null;
        if (Item is { } item)
            item.Firmware = settings.Firmware;

        IsBeaconSupported = settings.BeaconMode is not null;
        IsBuzzerSupported = settings.BuzzerEnabled is not null;
        IsSpeedSupported = settings.MulticlickSpeed is not null;
        IsTimeSupported = settings.DeviceTime is not null;

        BeaconMode = settings.BeaconMode ?? false;
        BuzzerEnabled = settings.BuzzerEnabled ?? false;
        Speed = settings.MulticlickSpeed is { } speed ? new SpeedOption(speed) : null;
        DeviceTime = settings.DeviceTime is { } time ? $"{time.ToLocalTime():G}" : null;
        showingSettings = false;
    }

    /// <summary>Maintient la liaison tant que cette connexion est la courante (cf. ShellyButtonClient.KeepAliveAsync).</summary>
    async Task KeepAliveAsync(ShellyButtonClient connection)
    {
        using var timer = new PeriodicTimer(KeepAliveInterval);
        while (await timer.WaitForNextTickAsync() && ReferenceEquals(client, connection))
        {
            if (IsBusy)
                continue;
            try
            {
                await connection.KeepAliveAsync();
            }
            catch (GattException)
            {
                // La perte de liaison est signalée par OnConnectionLost.
            }
        }
    }

    /// <summary>Sans cela, l'écran se croirait encore connecté et chaque action échouerait sans explication.</summary>
    void OnConnectionLost(object? sender, string reason) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        if (client is null)
            return;
        await CloseAsync();
        Feedback = new Problem("Le bouton a coupé la liaison. Appuyez dessus puis reconnectez-vous.");
    });

    async Task CloseAsync()
    {
        Connection = ConnectionState.Disconnected;
        deviceSettings = null;
        if (client is { } current)
        {
            client = null;
            await current.DisposeAsync();
        }
    }

    static string Describe(Exception ex) => ex switch
    {
        OperationCanceledException => "délai dépassé. Le bouton s'est peut-être rendormi : appuyez dessus et réessayez.",
        _ => ex.Message,
    };

    static Task<bool> ConfirmAsync(string title, string message, string accept) =>
        Shell.Current.DisplayAlertAsync(title, message, accept, "Annuler");
}
