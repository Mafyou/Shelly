namespace ShellyBLE.Services;

public sealed record AdvertisementReceived(IDevice Device, ShellyAdvertisement Advertisement);

/// <summary>
/// Scan BLE continu. Les boutons Shelly BLU n'émettent qu'au moment d'un appui :
/// on garde donc le scan actif et on reçoit chaque annonce, doublons compris.
/// </summary>
public sealed class BleScanner
{
    readonly IBluetoothLE bluetooth;
    readonly IButtonSettingsStore settings;
    readonly ILogger<BleScanner> logger;
    readonly HashSet<MacAddress> watched = [];
    CancellationTokenSource? scan;
    int advertisementCount;
    int shellyCount;

    public BleScanner(IBluetoothLE bluetooth, IAdapter adapter, IButtonSettingsStore settings, ILogger<BleScanner> logger)
    {
        this.bluetooth = bluetooth;
        this.settings = settings;
        this.logger = logger;
        Adapter = adapter;
        Adapter.ScanMode = ScanMode.LowLatency;
        Adapter.ScanTimeout = (int)TimeSpan.FromDays(1).TotalMilliseconds;
        // Selon la plateforme, la première annonce d'un appareil n'arrive que par DeviceDiscovered ;
        // les doublons sont écartés plus loin par l'identifiant de paquet.
        Adapter.DeviceAdvertised += OnDevice;
        Adapter.DeviceDiscovered += OnDevice;
    }

    public IAdapter Adapter { get; }

    public bool IsBluetoothOn => bluetooth.IsOn;

    public bool IsScanning => scan is not null;

    public string? LastError { get; private set; }

    /// <summary>Scan filtré (Android) : seules les annonces Shelly/BTHome remontent.</summary>
    public bool IsFiltered { get; private set; }

    /// <summary>Diagnostic : scan sans aucun filtre, écran allumé uniquement (Android le suspend écran éteint).</summary>
    public bool IsRawScan { get; set; }

    /// <summary>Annonces BLE reçues depuis le démarrage, tous appareils confondus (diagnostic).</summary>
    public int AdvertisementCount => Volatile.Read(ref advertisementCount);

    /// <summary>Annonces reconnues comme Shelly ou BTHome.</summary>
    public int ShellyCount => Volatile.Read(ref shellyCount);

    public event EventHandler<AdvertisementReceived>? Advertised;

    public event EventHandler? ScanningChanged;

    public void Start()
    {
        if (scan is not null)
            return;

        scan = new();
        LastError = null;
        IsFiltered = DeviceInfo.Platform == DevicePlatform.Android && !IsRawScan;
#if ANDROID
        ScanForegroundService.Start();
#endif
        ScanningChanged?.Invoke(this, EventArgs.Empty);
        _ = RunAsync(scan);
    }

    public async Task StopAsync()
    {
        if (scan is not { } current)
            return;

        // Libéré tout de suite pour qu'un Start() immédiat relance bien un scan.
        scan = null;
        await current.CancelAsync();
        await Adapter.StopScanningForDevicesAsync();
    }

    async Task RunAsync(CancellationTokenSource current)
    {
        try
        {
            await Adapter.StartScanningForDevicesAsync(scanFilterOptions: IsFiltered ? Filters() : null, allowDuplicatesKey: true, cancellationToken: current.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(scan, current))
            {
                scan = null;
#if ANDROID
                ScanForegroundService.Stop();
#endif
            }
            current.Dispose();
            ScanningChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Android suspend les scans non filtrés écran éteint (constaté : plus aucun appui, téléphone débranché)
    /// et, jusqu'à Android 11, quand la localisation est coupée. On filtre donc toujours sur
    /// BTHome ou les données constructeur Allterco (les filtres s'additionnent). Pas de filtre par nom :
    /// redondant, et les emplacements de filtres du contrôleur (filtrage matériel écran éteint) sont comptés.
    /// Un appui n'émet pas de réponse au scan : le nom et les données BTHome sont les plus sûrs.
    /// </summary>
    /// <remarks>
    /// Les trames d'appui du BLU Button1 portent aussi les données constructeur (octet d'état 0x01) : le filtre
    /// constructeur suffit pour lui. Le filtre par adresse, géré par tous les contrôleurs, couvre en plus les
    /// appareils BTHome dont le filtre de données de service serait mal appliqué par la puce : on y ajoute
    /// chaque bouton enregistré ou déjà aperçu.
    /// </remarks>
    ScanFilterOptions Filters() => new()
    {
        ServiceDataFilters = [new(BTHomeParser.ServiceGuid, [], [])],
        ManufacturerDataFilters = [new(ShellyManufacturerData.AlltercoCompanyId, [], [])],
        DeviceAddresses = [.. settings.All.Select(s => s.ParsedMac).OfType<MacAddress>().Union(watched).Select(mac => $"{mac}")],
    };

    /// <summary>
    /// Ajoute un bouton aperçu au filtre par adresse, pour recevoir ensuite ses trames d'appui.
    /// Relance le scan si l'adresse est nouvelle (thread UI).
    /// </summary>
    public async Task WatchAsync(MacAddress mac)
    {
        if (!watched.Add(mac) || !IsFiltered || settings.All.Any(s => s.ParsedMac == mac))
            return;
        logger.LogInformation("Filtre par adresse : {Mac} ajouté, relance du scan", mac);
        await RestartAsync();
    }

    /// <summary>Relance le scan pour prendre en compte un bouton nouvellement enregistré (filtre par adresse).</summary>
    public async Task RestartAsync()
    {
        if (!IsScanning)
            return;
        await StopAsync();
        Start();
    }

    void OnDevice(object? sender, DeviceEventArgs e)
    {
        if (e.Device.AdvertisementRecords is not { Count: > 0 } records)
            return;

        Interlocked.Increment(ref advertisementCount);
        var advertisement = ShellyAdvertisement.From(records.Select(r => new AdRecord((byte)r.Type, [.. r.Data ?? []])));
        if (advertisement.IsShelly || advertisement.BTHomePayload is not null)
        {
            Interlocked.Increment(ref shellyCount);
            // Trame brute (type:contenu par structure AD) : la référence pour vérifier le décodage.
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Shelly {Device} {Rssi} dBm {Records}", e.Device.Id, e.Device.Rssi,
                    string.Join(' ', records.Select(r => $"{(byte)r.Type:X2}:{Convert.ToHexString(r.Data ?? [])}")));
        }
        Advertised?.Invoke(this, new(e.Device, advertisement));
    }
}
