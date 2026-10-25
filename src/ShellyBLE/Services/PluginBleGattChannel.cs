namespace ShellyBLE.Services;

/// <summary>Implémentation de <see cref="IGattChannel"/> au-dessus de Plugin.BLE.</summary>
public sealed class PluginBleGattChannel : IGattChannel
{
    readonly IAdapter adapter;
    readonly IDevice device;
    readonly ILogger logger;
    readonly Dictionary<Guid, IService?> services = [];

    PluginBleGattChannel(IAdapter adapter, IDevice device, ILogger logger)
    {
        this.adapter = adapter;
        this.device = device;
        this.logger = logger;
        adapter.DeviceConnectionLost += OnConnectionLost;
    }

    /// <summary>Le bouton a coupé la liaison (Plugin.BLE : DeviceConnectionLost).</summary>
    public event EventHandler<string>? ConnectionLost;

    /// <summary>
    /// BluetoothGatt.CONNECTION_PRIORITY_DCK (3) : absent de l'énumération Plugin.BLE, qui transmet
    /// la valeur telle quelle à requestConnectionPriority.
    /// </summary>
    const ConnectionInterval DckPriority = (ConnectionInterval)3;

    /// <summary>Attente avant la découverte des services (plusieurs essais).</summary>
    static readonly ImmutableArray<TimeSpan> DiscoveryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(2)];

    /// <summary>
    /// Se connecte et découvre le service Shelly, sans lier l'appareil : une liaison lancée d'emblée
    /// échoue (SMP_CONN_TOUT, constaté sur Android). Les lectures n'exigent pas de liaison ;
    /// elle est demandée à la première écriture refusée.
    /// </summary>
    public static async Task<PluginBleGattChannel> ConnectAsync(IAdapter adapter, IDevice device, ILogger logger, CancellationToken stoppingToken)
    {
        // Capture HCI (OnePlus 7 Pro / Android 17, firmware 1.0.2) : le bouton se tait ~200 ms après toute
        // mise à jour des paramètres de connexion qui l'autorise à dormir plus longtemps (latence 1 avec la
        // priorité « haute », intervalle plus long avec le relâchement qu'Android applique sinon). Il tient
        // à 15 ms sans latence. On demande donc, dès le rappel natif et avant ce relâchement, une priorité
        // qui garde un intervalle court sans latence : DCK (Android 14+), sinon haute.
        void OnConnected(object? sender, DeviceEventArgs e)
        {
            if (e.Device.Id != device.Id)
                return;
#if ANDROID
            // Paramètres identiques à ceux de la liaison : Android n'a plus rien à mettre à jour.
            if (GattConnectionTuning.KeepInitialParameters(e.Device, logger))
                return;
#endif
            var priority = OperatingSystem.IsAndroidVersionAtLeast(34) ? DckPriority : ConnectionInterval.High;
            logger.LogInformation("Rappel de connexion : priorité {Priority} demandée ({Accepted})", (int)priority, e.Device.UpdateConnectionInterval(priority));
        }

        adapter.DeviceConnected += OnConnected;
        try
        {
            return await ConnectWithRetriesAsync(adapter, device, logger, stoppingToken);
        }
        finally
        {
            adapter.DeviceConnected -= OnConnected;
        }
    }

    static async Task<PluginBleGattChannel> ConnectWithRetriesAsync(IAdapter adapter, IDevice device, ILogger logger, CancellationToken stoppingToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var delay = DiscoveryDelays[attempt];
            logger.LogInformation("Connexion à {Device}, essai {Attempt}/{Count}, attente {Delay} s (liaison : {Bond})",
                device.Id, attempt + 1, DiscoveryDelays.Length, delay.TotalSeconds, device.BondState);

            var channel = new PluginBleGattChannel(adapter, device, logger);
            try
            {
                await adapter.ConnectToDeviceAsync(device, new ConnectParameters(autoConnect: false, forceBleTransport: true), stoppingToken);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, stoppingToken);
                logger.LogInformation("Découverte des services (connecté : {State})", device.State);
                await channel.GetServiceAsync(ShellyGatt.Service, stoppingToken);
                logger.LogInformation("Connecté, service Shelly trouvé (essai {Attempt})", attempt + 1);
                await channel.LogGattDatabaseAsync(stoppingToken);
                return channel;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt + 1 < DiscoveryDelays.Length)
            {
                logger.LogWarning("Essai {Attempt} échoué : {Message}", attempt + 1, ex.Message);
                await channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Échec de la connexion");
                await channel.DisposeAsync();
                throw;
            }
        }
    }

    public async Task<ImmutableArray<byte>> ReadAsync(Guid service, Guid characteristic, CancellationToken stoppingToken = default)
    {
        var target = await GetCharacteristicAsync(service, characteristic, stoppingToken);
        try
        {
            var (data, result) = await target.ReadAsync(stoppingToken);
            logger.LogDebug("Lecture {Characteristic} → code {Result} {Data}", characteristic, result, Convert.ToHexString(data ?? []));
            return result == 0 ? ImmutableCollectionsMarshal.AsImmutableArray(data) : throw new GattException($"Lecture refusée (code {result}).");
        }
        catch (Exception ex) when (ex is not (GattException or OperationCanceledException))
        {
            logger.LogWarning(ex, "Lecture {Characteristic} impossible", characteristic);
            throw new GattException($"Lecture impossible : {ex.Message}", ex);
        }
    }

    public async Task WriteAsync(Guid service, Guid characteristic, ImmutableArray<byte> value, bool withResponse = true, CancellationToken stoppingToken = default)
    {
        var target = await GetCharacteristicAsync(service, characteristic, stoppingToken);
        target.WriteType = withResponse ? CharacteristicWriteType.WithResponse : CharacteristicWriteType.WithoutResponse;
        try
        {
            // Le firmware coupe la liaison (sans refus GATT) quand un client non lié écrit une
            // caractéristique protégée : il faut donc se lier avant la première écriture.
            await EnsureBondedAsync();
            var result = await WriteOnceAsync(target, characteristic, value, stoppingToken);
            if (result != 0)
                throw new GattException($"Écriture refusée (code {result}). Le bouton est-il appairé ? Maintenez-le 10 s puis reconnectez-vous.");
        }
        catch (Exception ex) when (ex is not (GattException or OperationCanceledException))
        {
            logger.LogWarning(ex, "Écriture {Characteristic} impossible", characteristic);
            throw new GattException($"Écriture impossible : {ex.Message}. Le bouton est-il appairé ?", ex);
        }
    }

    async Task<int> WriteOnceAsync(ICharacteristic target, Guid characteristic, ImmutableArray<byte> value, CancellationToken stoppingToken)
    {
        var result = await target.WriteAsync(ImmutableCollectionsMarshal.AsArray(value)!, stoppingToken);
        logger.LogDebug("Écriture {Characteristic} {Data} → code {Result}", characteristic, Convert.ToHexString(value.AsSpan()), result);
        return result;
    }

    /// <summary>Lie l'appareil (une seule fois) pendant que la connexion est active ; iOS et macOS le font d'eux-mêmes.</summary>
    public async Task<bool> EnsureBondedAsync()
    {
        if (device.BondState == DeviceBondState.Bonded)
            return true;
        if (DeviceInfo.Platform != DevicePlatform.Android && DeviceInfo.Platform != DevicePlatform.WinUI)
            return false;

        logger.LogInformation("Liaison avec {Device} avant écriture", device.Id);
        try
        {
            await adapter.BondAsync(device);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Liaison impossible");
        }
        logger.LogInformation("Liaison : {Bond}", device.BondState);
        return device.BondState == DeviceBondState.Bonded;
    }

    /// <summary>
    /// Rétro-ingénierie : journalise services et caractéristiques (propriétés comprises) sans rien lire,
    /// car lire une caractéristique protégée sans liaison fait couper la connexion au firmware.
    /// </summary>
    async Task LogGattDatabaseAsync(CancellationToken stoppingToken)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
            return;

        foreach (var service in await device.GetServicesAsync(stoppingToken))
        {
            logger.LogDebug("GATT service {Service} {Name}", service.Id, service.Name);
            foreach (var characteristic in await service.GetCharacteristicsAsync())
                logger.LogDebug("GATT   caractéristique {Characteristic} {Name} [{Properties}]", characteristic.Id, characteristic.Name, characteristic.Properties);
        }
    }

    void OnConnectionLost(object? sender, DeviceErrorEventArgs e)
    {
        if (e.Device?.Id != device.Id)
            return;
        logger.LogWarning("Liaison perdue avec {Device} : {Error}", device.Id, e.ErrorMessage);
        ConnectionLost?.Invoke(this, e.ErrorMessage ?? "liaison perdue");
    }

    public async ValueTask DisposeAsync()
    {
        adapter.DeviceConnectionLost -= OnConnectionLost;
        try
        {
            await adapter.DisconnectDeviceAsync(device);
        }
        catch (Exception)
        {
            // Déjà déconnecté (veille du bouton, réinitialisation…).
        }
    }

    async Task<ICharacteristic> GetCharacteristicAsync(Guid service, Guid characteristic, CancellationToken stoppingToken)
    {
        var owner = await GetServiceAsync(service, stoppingToken);
        if (await owner.GetCharacteristicAsync(characteristic) is { } found)
            return found;

        logger.LogInformation("Caractéristique {Characteristic} absente (firmware trop ancien ?)", characteristic);
        throw new GattException($"Caractéristique {characteristic} introuvable.");
    }

    async Task<IService> GetServiceAsync(Guid id, CancellationToken stoppingToken)
    {
        if (!services.TryGetValue(id, out var service))
            services[id] = service = await device.GetServiceAsync(id, stoppingToken);
        return service ?? throw new GattException(id == ShellyGatt.Service
            ? "Service de configuration Shelly introuvable : est-ce bien un Shelly BLU ?"
            : $"Service {id} introuvable.");
    }
}
