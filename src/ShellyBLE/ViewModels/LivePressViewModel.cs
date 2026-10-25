namespace ShellyBLE.ViewModels;

/// <summary>
/// Mode affichage : le dernier appui en très gros (CLICK, DOUBLE CLICK, TRIPLE CLICK, LONG).
/// L'application n'émet aucun son ; le bip vient du buzzer du bouton, qu'on peut couper d'ici.
/// </summary>
public sealed partial class LivePressViewModel(ButtonsService buttons, BleScanner scanner, ILogger<PluginBleGattChannel> gattLogger) : ObservableObject
{
    const int RecentCount = 6;

    static readonly TimeSpan MuteTimeout = TimeSpan.FromSeconds(45);

    [ObservableProperty]
    public partial string Word { get; set; } = "PRÊT";

    [ObservableProperty]
    public partial string Icon { get; set; } = Icons.TapButton;

    /// <summary>Derniers appuis, le plus récent en premier.</summary>
    public ObservableCollection<PressVisual> Recent { get; } = [];

    [ObservableProperty]
    public partial string Detail { get; set; } = "En attente d'un appui";

    [ObservableProperty]
    public partial string? MuteStatus { get; set; }

    [ObservableProperty]
    public partial bool IsMuting { get; set; }

    /// <summary>Incrémenté à chaque appui : la vue s'en sert pour animer, même si le mot ne change pas.</summary>
    [ObservableProperty]
    public partial int PressCount { get; set; }

    public void Start() => buttons.PressReceived += OnPress;

    public void Stop() => buttons.PressReceived -= OnPress;

    void OnPress(object? sender, PressNotification notification)
    {
        var visual = PressVisual.Of(notification.Press.Event);
        Word = visual.Word;
        Icon = visual.Icon;
        Detail = $"{notification.Button.DisplayName} · {DateTime.Now:HH:mm:ss}";
        Recent.Insert(0, visual);
        while (Recent.Count > RecentCount)
            Recent.RemoveAt(Recent.Count - 1);
        PressCount++;
    }

    /// <summary>Coupe le buzzer du bouton (réglage GATT) : plus aucun bip à l'appui.</summary>
    [RelayCommand]
    async Task MuteButtonAsync()
    {
        if ((buttons.Buttons.FirstOrDefault(b => b.Device is not null && b.IsSaved) ?? buttons.Buttons.FirstOrDefault(b => b.Device is not null)) is not { } item)
        {
            MuteStatus = "Bouton pas encore détecté : appuyez dessus, puis réessayez.";
            return;
        }

        IsMuting = true;
        MuteStatus = "Connexion au bouton…";
        using var timeout = new CancellationTokenSource(MuteTimeout);
        try
        {
            await using var client = new ShellyButtonClient(await PluginBleGattChannel.ConnectAsync(scanner.Adapter, item.Device!, gattLogger, timeout.Token));
            var current = await client.ReadSettingsAsync(timeout.Token);
            if (current.BuzzerEnabled is null)
            {
                MuteStatus = "Ce firmware n'expose pas le réglage du buzzer.";
                return;
            }
            await client.ApplySettingsAsync(current, current with { BuzzerEnabled = false }, timeout.Token);
            MuteStatus = "Buzzer du bouton coupé : plus aucun bip.";
        }
        catch (Exception ex)
        {
            MuteStatus = $"Impossible de couper le buzzer : {ex.Message}";
        }
        finally
        {
            IsMuting = false;
        }
    }
}
