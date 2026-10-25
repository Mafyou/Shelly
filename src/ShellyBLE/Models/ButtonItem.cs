namespace ShellyBLE.Models;

public enum ActivityType
{
    Press,
    ActionSucceeded,
    ActionFailed,
    LinkOpened,
}

/// <summary>Une ligne du journal d'activité d'un bouton.</summary>
public sealed record ActivityEntry(DateTime Time, ActivityType Kind, string Title, string? Detail = null, PressVisual? Press = null)
{
    public string TimeText => $"{Time:HH:mm:ss}";

    public string Icon => Kind switch
    {
        ActivityType.Press => Press?.Icon ?? Icons.Tap,
        ActivityType.ActionSucceeded => Icons.Success,
        ActivityType.LinkOpened => Icons.Link,
        _ => Icons.Error,
    };

    public bool IsPress => Kind == ActivityType.Press;

    public bool IsFailure => Kind == ActivityType.ActionFailed;
}

/// <summary>État en direct d'un bouton détecté ou enregistré.</summary>
public sealed partial class ButtonItem(Guid id) : ObservableObject
{
    const int MaxEntries = 100;
    const int LowBattery = 10;

    /// <summary>Au-delà, le bouton n'émet plus : il dort (ou est hors de portée).</summary>
    static readonly TimeSpan SilenceThreshold = TimeSpan.FromSeconds(5);

    public Guid Id { get; } = id;

    /// <summary>Dernière instance Plugin.BLE vue en scan, nécessaire pour se connecter.</summary>
    public IDevice? Device { get; set; }

    public ObservableCollection<ActivityEntry> Activity { get; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string? AdvertisedName { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string? CustomName { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(DisplayName), nameof(Subtitle))]
    public partial ShellyModel? Model { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Subtitle))]
    public partial MacAddress? Mac { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SignalIcon), nameof(SignalText), nameof(IsLive))]
    public partial int? Rssi { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(BatteryIcon), nameof(BatteryText), nameof(IsBatteryLow))]
    public partial int? Battery { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SecurityIcon), nameof(SecurityText))]
    public partial bool IsEncrypted { get; set; }

    [ObservableProperty]
    public partial bool IsInPairingMode { get; set; }

    [ObservableProperty]
    public partial bool IsSaved { get; set; }

    /// <summary>Firmware lu à la dernière connexion ; null tant qu'on ne s'est pas connecté.</summary>
    [ObservableProperty]
    public partial ShellyFirmware? Firmware { get; set; }

    /// <summary>Problème de décodage en cours (clé manquante, déchiffrement en échec…).</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? Status { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(LastPressText), nameof(LastPressIcon), nameof(HasLastPress))]
    public partial ActivityEntry? LastPress { get; set; }

    public string DisplayName => CustomName ?? Model?.Name ?? AdvertisedName ?? "Appareil Shelly BLU";

    public string Subtitle => string.Join(" · ", new[] { Model?.ShortName, Mac is { } mac ? $"{mac}" : null }.OfType<string>());

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    /// <summary>Le bouton émet en ce moment (il dort entre deux appuis).</summary>
    public bool IsLive => Rssi is not null;

    public string SignalIcon => Rssi switch
    {
        null => Icons.SignalNone,
        >= -60 => Icons.Signal3,
        >= -75 => Icons.Signal2,
        _ => Icons.Signal1,
    };

    public string SignalText => Rssi is { } rssi ? $"{rssi} dBm" : "Endormi";

    /// <summary>
    /// Constaté en 1.0.2 : la balise du mode appairage annonce 0 %, alors que les trames d'appui
    /// portent la vraie valeur. Un bouton qui émet n'est jamais réellement à 0 %.
    /// </summary>
    bool IsBatteryReliable => Battery is > 0;

    public bool IsBatteryLow => IsBatteryReliable && Battery <= LowBattery;

    public string BatteryIcon => (IsBatteryReliable, IsBatteryLow) switch
    {
        (false, _) => Icons.BatteryUnknown,
        (true, true) => Icons.BatteryAlert,
        _ => Icons.Battery,
    };

    public string BatteryText => IsBatteryReliable ? $"{Battery} %" : "—";

    public string SecurityIcon => IsEncrypted ? Icons.Lock : Icons.LockOpen;

    public string SecurityText => IsEncrypted ? "Chiffré" : "Non chiffré";

    public bool HasLastPress => LastPress is not null;

    public string LastPressIcon => LastPress?.Icon ?? Icons.TapButton;

    public string LastPressText => LastPress is { } press ? $"{press.Title} · {Ago(press.Time)}" : "Aucun appui reçu";

    public DateTime? LastSeen { get; private set; }

    public void MarkSeen(DateTime now) => LastSeen = now;

    /// <summary>Appelé chaque seconde : état « en direct » et temps relatif du dernier appui.</summary>
    public void Refresh(DateTime now)
    {
        if (LastSeen is { } seen && now - seen > SilenceThreshold)
        {
            IsInPairingMode = false;
            Rssi = null;
        }
        if (LastPress is not null)
            OnPropertyChanged(nameof(LastPressText));
    }

    /// <param name="title">Intitulé à afficher ; « Bouton 2 : Appui simple » pour les modèles à plusieurs touches.</param>
    public void AddPress(PressVisual press, string? title = null)
    {
        var entry = new ActivityEntry(DateTime.Now, ActivityType.Press, title ?? press.Title, Press: press);
        Add(entry);
        LastPress = entry;
    }

    public void AddActionResult(ActivityType kind, string title, string? detail = null) =>
        Add(new(DateTime.Now, kind, title, detail));

    void Add(ActivityEntry entry)
    {
        Activity.Insert(0, entry);
        while (Activity.Count > MaxEntries)
            Activity.RemoveAt(Activity.Count - 1);
    }

    static string Ago(DateTime time) => (DateTime.Now - time) switch
    {
        { TotalSeconds: < 5 } => "à l'instant",
        { TotalMinutes: < 1 } elapsed => $"il y a {elapsed.Seconds} s",
        { TotalHours: < 1 } elapsed => $"il y a {(int)elapsed.TotalMinutes} min",
        _ => $"à {time:HH:mm}",
    };
}
