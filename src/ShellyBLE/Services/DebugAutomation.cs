#if DEBUG
namespace ShellyBLE.Services;

/// <summary>
/// Scénarios pilotés par adb, build Debug uniquement, pour tester sans toucher l'écran :
/// <c>adb shell am broadcast -a fr.mafyou.shelly.AUTO --es steps "connect,bond,beacon:1,read,disconnect"</c>.
/// Les étapes passent par le même code que l'application (PluginBleGattChannel, ShellyButtonClient) ;
/// chaque résultat est journalisé (logcat -s ShellyBLE).
/// </summary>
public sealed class DebugAutomation(BleScanner scanner, ButtonsService buttons, ILogger<DebugAutomation> logger, ILogger<PluginBleGattChannel> gattLogger)
{
    static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(40);

    readonly SemaphoreSlim gate = new(1, 1);
    PluginBleGattChannel? channel;
    ShellyButtonClient? client;
    ButtonItem? target;

    PluginBleGattChannel Channel => channel ?? throw new InvalidOperationException("Pas connecté : commencez par « connect ».");

    ShellyButtonClient Client => client ?? throw new InvalidOperationException("Pas connecté : commencez par « connect ».");

    public async Task RunAsync(string steps)
    {
        await gate.WaitAsync();
        try
        {
            foreach (var step in steps.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    var result = await RunStepAsync(step);
                    logger.LogInformation("AUTO {Step} OK ({Elapsed} ms) : {Result}", step, watch.ElapsedMilliseconds, result);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("AUTO {Step} ÉCHEC ({Elapsed} ms) : {Error}", step, watch.ElapsedMilliseconds, ex.Message);
                    break;
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    async Task<string> RunStepAsync(string step)
    {
        var (name, argument) = step.Split(':', 2) is [var n, var a] ? (n, a) : (step, null);
        using var timeout = new CancellationTokenSource(StepTimeout);
        var stoppingToken = timeout.Token;

        return name switch
        {
            "connect" => await ConnectAsync(argument, stoppingToken),
            "bond" => $"lié : {await Channel.EnsureBondedAsync()}",
            "read" => $"{await Client.ReadSettingsAsync(stoppingToken)}",
            "beacon" => await ApplyAsync(s => s with { BeaconMode = argument == "1" }, stoppingToken),
            "buzzer" => await ApplyAsync(s => s with { BuzzerEnabled = argument == "1" }, stoppingToken),
            "ring" => await DoAsync(Client.SetBuzzerActiveAsync(argument != "0", stoppingToken)),
            "time" => $"{await Client.SyncTimeAsync(stoppingToken):O}",
            "key" => Convert.ToHexStringLower((await Client.ReadEncryptionKeyAsync(stoppingToken)).AsSpan()),
            "prov" => await Client.ReadProvisioningDataAsync(stoppingToken) ?? "(vide)",
            "sample" => Describe(await Client.ReadSampleBTHomeDataAsync(stoppingToken)),
            "wait" => await WaitAsync(argument, stoppingToken),
            "disconnect" => await DisconnectAsync(),
            "actions" => await SaveActionsAsync(argument ?? throw new ArgumentException("URL de base attendue.")),
            "encrypt" => await EncryptAsync(argument, stoppingToken),
            "savekey" => await SaveKeyAsync(await Client.ReadEncryptionKeyAsync(stoppingToken)),
            "clearkey" => await SaveKeyAsync(null),
            "reset" => await FactoryResetAsync(stoppingToken),
            "status" => Status(),
            "rawscan" => await RawScanAsync(argument != "0"),
            "waitbutton" => await WaitForButtonAsync(),
            _ => throw new ArgumentException($"Étape inconnue : {name}"),
        };
    }

    async Task<string> ConnectAsync(string? mac, CancellationToken stoppingToken)
    {
        await DisconnectAsync();
        var item = buttons.Buttons.FirstOrDefault(b => b.Device is not null && (mac is null || $"{b.Mac}".Replace(":", "") == mac.Replace(":", "").ToUpperInvariant()))
            ?? throw new InvalidOperationException("Aucun bouton détecté en scan.");

        channel = await PluginBleGattChannel.ConnectAsync(scanner.Adapter, item.Device!, gattLogger, stoppingToken);
        client = new(channel);
        target = item;
        return $"{item.Mac}";
    }

    ButtonItem Target => target ?? buttons.Buttons.FirstOrDefault(b => b.Model?.IsButton == true)
        ?? throw new InvalidOperationException("Aucun bouton connu.");

    ButtonSettings CurrentSettings => buttons.GetSettings(Target) ?? new ButtonSettings { Id = $"{Target.Id}" };

    /// <summary>Une URL par type d'appui : base?event=Press, etc.</summary>
    async Task<string> SaveActionsAsync(string baseUrl)
    {
        var actions = ButtonEventExtensions.Actionable.ToFrozenDictionary(e => e, e => (ButtonAction)new HttpRequestAction($"{baseUrl}?event={e}"));
        await buttons.SaveAsync(Target, CurrentSettings with { Actions = actions });
        return string.Join(" ", actions.Values.Select(action => action.Summary));
    }

    /// <summary>Active le chiffrement avec ce PIN, lit la nouvelle clé et l'enregistre pour déchiffrer les annonces.</summary>
    async Task<string> EncryptAsync(string? pin, CancellationToken stoppingToken)
    {
        if (!int.TryParse(pin, out var passkey))
            throw new ArgumentException("PIN numérique attendu.");
        return await SaveKeyAsync(await Client.EnableEncryptionAsync(passkey, stoppingToken));
    }

    async Task<string> SaveKeyAsync(ImmutableArray<byte>? key)
    {
        var hex = key is { } bytes ? Convert.ToHexStringLower(bytes.AsSpan()) : null;
        await buttons.SaveAsync(Target, CurrentSettings with { Key = hex, Mac = $"{Target.Mac}" });
        return hex ?? "clé effacée";
    }

    async Task<string> FactoryResetAsync(CancellationToken stoppingToken)
    {
        await Client.FactoryResetAsync(stoppingToken);
        await DisconnectAsync();
        return await SaveKeyAsync(null);
    }

    /// <summary>Le bouton dort entre deux appuis : attend qu'il apparaisse dans la liste (un appui suffit).</summary>
    async Task<string> WaitForButtonAsync()
    {
        for (var elapsed = TimeSpan.Zero; elapsed < TimeSpan.FromMinutes(3); elapsed += TimeSpan.FromSeconds(1))
        {
            if (buttons.Buttons.FirstOrDefault(b => b.Model?.IsButton == true) is { } item)
                return $"{item.DisplayName} {item.Mac} après {elapsed.TotalSeconds:0} s";
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        throw new TimeoutException("Aucun bouton en 3 min.");
    }

    async Task<string> RawScanAsync(bool raw)
    {
        scanner.IsRawScan = raw;
        await scanner.RestartAsync();
        return raw ? "scan sans filtre" : "scan filtré";
    }

    string Status()
    {
        var item = Target;
        return $"{item.DisplayName} {item.Mac} batterie={item.BatteryText} chiffré={item.IsEncrypted} statut={item.Status} dernier={item.LastPressText} activité={item.Activity.Count}";
    }

    async Task<string> ApplyAsync(Func<ButtonDeviceSettings, ButtonDeviceSettings> change, CancellationToken stoppingToken)
    {
        var current = await Client.ReadSettingsAsync(stoppingToken);
        var desired = change(current);
        await Client.ApplySettingsAsync(current, desired, stoppingToken);
        return $"{desired}";
    }

    static string Describe(ImmutableArray<byte> payload) => $"{Convert.ToHexString(payload.AsSpan())} → {BTHomeParser.Parse(payload.AsSpan()) switch
    {
        BTHomePacket packet => string.Join(" ", packet.Objects.Select(o => $"{o}")),
        BTHomeError error => $"{error}",
    }}";

    static async Task<string> DoAsync(Task operation)
    {
        await operation;
        return "fait";
    }

    static async Task<string> WaitAsync(string? milliseconds, CancellationToken stoppingToken)
    {
        var delay = int.TryParse(milliseconds, out var ms) ? ms : 1000;
        await Task.Delay(delay, stoppingToken);
        return $"{delay} ms";
    }

    async Task<string> DisconnectAsync()
    {
        if (client is not { } current)
            return "déjà déconnecté";
        client = null;
        channel = null;
        await current.DisposeAsync();
        return "déconnecté";
    }
}
#endif
