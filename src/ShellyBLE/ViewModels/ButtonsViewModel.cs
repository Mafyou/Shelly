namespace ShellyBLE.ViewModels;

public sealed partial class ButtonsViewModel : ObservableObject
{
    readonly BleScanner scanner;
    bool started;

    public ButtonsViewModel(BleScanner scanner, ButtonsService buttons)
    {
        this.scanner = scanner;
        Buttons = buttons.Buttons;
        scanner.ScanningChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        Refresh();
    }

    public ObservableCollection<ButtonItem> Buttons { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ScanActionText))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    public string ScanActionText => IsScanning ? "Arrêter" : "Scanner";

    /// <summary>Le scan démarre tout seul au premier affichage.</summary>
    [RelayCommand]
    async Task AppearingAsync()
    {
        if (started)
            return;
        started = true;
        await StartAsync();
        await RefreshLoopAsync();
    }

    [RelayCommand]
    async Task ToggleScanAsync()
    {
        if (scanner.IsScanning)
            await scanner.StopAsync();
        else
            await StartAsync();
    }

    [RelayCommand]
    static Task OpenLiveModeAsync() => Shell.Current.GoToAsync(nameof(LivePressPage));

    [RelayCommand]
    static Task OpenAsync(ButtonItem item) =>
        Shell.Current.GoToAsync(nameof(ButtonDetailPage), new ShellNavigationQueryParameters { [ButtonDetailViewModel.IdParameter] = item.Id });

    async Task StartAsync()
    {
        if (!await BlePermissions.RequestAsync())
        {
            Status = "Autorisation Bluetooth refusée : impossible de scanner.";
            return;
        }
        if (!scanner.IsBluetoothOn)
        {
            Status = "Bluetooth désactivé : activez-le puis relancez le scan.";
            return;
        }
        scanner.Start();
    }

    /// <summary>Rafraîchit les compteurs de diagnostic et l'état « en direct » des boutons.</summary>
    async Task RefreshLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync())
        {
            Refresh();
            foreach (var item in Buttons)
                item.Refresh(DateTime.Now);
        }
    }

    void Refresh()
    {
        IsScanning = scanner.IsScanning;
        Status = (scanner.IsScanning, scanner.IsFiltered, scanner.LastError) switch
        {
            (_, _, { } error) => $"Écoute impossible : {error}",
            (true, true, _) => $"Écoute active · {scanner.ShellyCount} annonces",
            (true, false, _) => $"Écoute active · {scanner.ShellyCount} annonces Shelly sur {scanner.AdvertisementCount}",
            _ => "Écoute arrêtée",
        };
    }
}
