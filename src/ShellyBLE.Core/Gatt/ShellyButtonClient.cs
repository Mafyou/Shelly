namespace ShellyBLE.Core.Gatt;

/// <summary>
/// Configuration lue sur le bouton à la connexion. Les réglages absents du firmware valent null :
/// relevé sur un 1.0.2 : heure et mode balise présents, vitesse multi-clic absente (ajoutée en 1.0.11).
/// </summary>
public sealed record ButtonDeviceSettings(
    ShellyFirmware? Firmware,
    bool? BeaconMode,
    bool? BuzzerEnabled,
    MulticlickSpeed? MulticlickSpeed,
    DateTimeOffset? DeviceTime);

/// <summary>Opérations de configuration d'un Shelly BLU Button1 via son service GATT.</summary>
public sealed class ShellyButtonClient(IGattChannel channel, TimeProvider? time = null) : IAsyncDisposable
{
    readonly TimeProvider time = time ?? TimeProvider.System;

    public async Task<ButtonDeviceSettings> ReadSettingsAsync(CancellationToken stoppingToken = default) => new(
        await GetFirmwareAsync(stoppingToken),
        await OptionalAsync(() => ReadBoolAsync(ShellyGatt.BeaconMode, stoppingToken)),
        await OptionalAsync(() => ReadBoolAsync(ShellyGatt.BuzzerEnabled, stoppingToken)),
        await OptionalAsync(() => GetMulticlickSpeedAsync(stoppingToken)),
        await OptionalAsync(() => GetTimeAsync(stoppingToken)));

    /// <summary>Le service Device Information est facultatif : null s'il est absent.</summary>
    public async Task<ShellyFirmware?> GetFirmwareAsync(CancellationToken stoppingToken = default)
    {
        try
        {
            var bytes = await channel.ReadAsync(ShellyGatt.DeviceInformationService, ShellyGatt.FirmwareRevision, stoppingToken);
            return ShellyFirmware.Parse(Encoding.UTF8.GetString(bytes.AsSpan()).TrimEnd('\0'));
        }
        catch (GattException)
        {
            return null;
        }
    }

    public async Task<MulticlickSpeed> GetMulticlickSpeedAsync(CancellationToken stoppingToken = default)
    {
        var value = Single(await Read(ShellyGatt.MulticlickSpeed, stoppingToken), "vitesse multi-clic");
        return Enum.IsDefined((MulticlickSpeed)value)
            ? (MulticlickSpeed)value
            : throw new GattException($"Vitesse multi-clic inattendue : {value}.");
    }

    public async Task<DateTimeOffset> GetTimeAsync(CancellationToken stoppingToken = default)
    {
        var bytes = await Read(ShellyGatt.UnixTime, stoppingToken);
        return bytes.Length >= 4 ? ShellyGatt.DecodeUnixTime(bytes.AsSpan()) : throw new GattException("Heure illisible.");
    }

    /// <summary>Écrit uniquement les réglages pris en charge (lus non null) qui diffèrent de ceux lus.</summary>
    public async Task ApplySettingsAsync(ButtonDeviceSettings current, ButtonDeviceSettings desired, CancellationToken stoppingToken = default)
    {
        if (Changed(current.BeaconMode, desired.BeaconMode) is { } beaconMode)
            await Write(ShellyGatt.BeaconMode, ShellyGatt.EncodeBool(beaconMode), stoppingToken);
        if (Changed(current.BuzzerEnabled, desired.BuzzerEnabled) is { } buzzerEnabled)
            await Write(ShellyGatt.BuzzerEnabled, ShellyGatt.EncodeBool(buzzerEnabled), stoppingToken);
        if (Changed(current.MulticlickSpeed, desired.MulticlickSpeed) is { } speed)
            await Write(ShellyGatt.MulticlickSpeed, [(byte)speed], stoppingToken);
    }

    /// <summary>
    /// Lecture légère qui maintient la liaison : sans activité, le firmware 1.0.2 la coupe ~30 s après
    /// la connexion ; avec une lecture toutes les 20 s, elle a tenu plus d'une minute.
    /// </summary>
    public Task KeepAliveAsync(CancellationToken stoppingToken = default) =>
        ReadBoolAsync(ShellyGatt.BuzzerEnabled, stoppingToken);

    /// <summary>Fait sonner le bouton (30 s maximum). Nécessite le mode balise et le buzzer activés.</summary>
    public Task SetBuzzerActiveAsync(bool active, CancellationToken stoppingToken = default) =>
        Write(ShellyGatt.ActivateBuzzer, ShellyGatt.EncodeBool(active), stoppingToken);

    public async Task<DateTimeOffset> SyncTimeAsync(CancellationToken stoppingToken = default)
    {
        var now = time.GetUtcNow();
        await Write(ShellyGatt.UnixTime, ShellyGatt.EncodeUnixTime(now), stoppingToken);
        return now;
    }

    /// <summary>Écart d'horloge au-delà duquel la connexion remet le bouton à l'heure.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Remet l'horloge à l'heure si elle dérive : elle repart de 1970 à chaque redémarrage du bouton,
    /// et elle sert de compteur au chiffrement. Renvoie la nouvelle heure, ou null si rien n'a été écrit
    /// (horloge juste, ou absente du firmware).
    /// </summary>
    public async Task<DateTimeOffset?> SyncTimeIfDriftedAsync(ButtonDeviceSettings settings, CancellationToken stoppingToken = default) =>
        settings.DeviceTime is { } clock && (clock - time.GetUtcNow()).Duration() > ClockTolerance
            ? await SyncTimeAsync(stoppingToken)
            : null;

    /// <summary>Définit le code PIN, ce qui active le chiffrement et régénère la clé, puis lit la nouvelle clé.</summary>
    public async Task<ImmutableArray<byte>> EnableEncryptionAsync(int passkey, CancellationToken stoppingToken = default)
    {
        await Write(ShellyGatt.Passkey, ShellyGatt.EncodePasskey(passkey), stoppingToken);
        return await ReadEncryptionKeyAsync(stoppingToken);
    }

    public async Task<ImmutableArray<byte>> ReadEncryptionKeyAsync(CancellationToken stoppingToken = default)
    {
        var key = await Read(ShellyGatt.EncryptionKey, stoppingToken);
        return key.Length == BTHomeCipher.KeyLength
            ? key
            : throw new GattException($"Clé de {key.Length} octets reçue, {BTHomeCipher.KeyLength} attendus.");
    }

    /// <summary>Exemple de trame BTHome telle que l'appareil la diffuserait (charge utile des données de service).</summary>
    public Task<ImmutableArray<byte>> ReadSampleBTHomeDataAsync(CancellationToken stoppingToken = default) =>
        Read(ShellyGatt.SampleBTHomeData, stoppingToken);

    /// <summary>Efface les appairages et désactive le chiffrement. L'appareil redémarre sans répondre.</summary>
    public Task FactoryResetAsync(CancellationToken stoppingToken = default) =>
        channel.WriteAsync(ShellyGatt.Service, ShellyGatt.FactoryReset, [1], withResponse: false, stoppingToken);

    public ValueTask DisposeAsync() => channel.DisposeAsync();

    static T? Changed<T>(T? current, T? desired) where T : struct =>
        current is { } before && desired is { } after && !EqualityComparer<T>.Default.Equals(before, after) ? after : null;

    /// <summary>Caractéristique absente du firmware ou illisible : réglage non pris en charge.</summary>
    static async Task<T?> OptionalAsync<T>(Func<Task<T>> read) where T : struct
    {
        try
        {
            return await read();
        }
        catch (GattException)
        {
            return null;
        }
    }

    async Task<bool> ReadBoolAsync(Guid characteristic, CancellationToken stoppingToken) =>
        Single(await Read(characteristic, stoppingToken), "booléen") != 0;

    Task<ImmutableArray<byte>> Read(Guid characteristic, CancellationToken stoppingToken) =>
        channel.ReadAsync(ShellyGatt.Service, characteristic, stoppingToken);

    Task Write(Guid characteristic, ImmutableArray<byte> value, CancellationToken stoppingToken) =>
        characteristic == ShellyGatt.SilabsOtaControl
            ? throw new InvalidOperationException("Écrire le contrôle OTA bascule le bouton dans son chargeur d'amorçage.")
            : channel.WriteAsync(ShellyGatt.Service, characteristic, value, withResponse: true, stoppingToken);

    /// <summary>Données de provisionnement cloud (concaténation des deux moitiés), null si absentes.</summary>
    public async Task<string?> ReadProvisioningDataAsync(CancellationToken stoppingToken = default)
    {
        var low = await OptionalAsync(async () => await Read(ShellyGatt.ProvisionDataLow, stoppingToken));
        var high = await OptionalAsync(async () => await Read(ShellyGatt.ProvisionDataHigh, stoppingToken));
        return low is null && high is null
            ? null
            : Encoding.UTF8.GetString([.. low ?? [], .. high ?? []]).TrimEnd('\0');
    }

    static byte Single(ImmutableArray<byte> bytes, string what) =>
        bytes is [var value, ..] ? value : throw new GattException($"Valeur vide pour {what}.");
}
