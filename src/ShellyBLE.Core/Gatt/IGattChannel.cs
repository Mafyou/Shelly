namespace ShellyBLE.Core.Gatt;

/// <summary>Connexion GATT établie vers un appareil ; l'implémentation dépend de la pile BLE de la plateforme.</summary>
public interface IGattChannel : IAsyncDisposable
{
    Task<ImmutableArray<byte>> ReadAsync(Guid service, Guid characteristic, CancellationToken stoppingToken = default);

    Task WriteAsync(Guid service, Guid characteristic, ImmutableArray<byte> value, bool withResponse = true, CancellationToken stoppingToken = default);
}

public sealed class GattException(string message, Exception? inner = null) : Exception(message, inner);
