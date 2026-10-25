namespace ShellyBLE.Core.BTHome;

/// <summary>
/// Un appui est répété dans plusieurs annonces identiques : on ne garde que la première,
/// reconnue au compteur de chiffrement ou, à défaut, à l'identifiant de paquet.
/// </summary>
public sealed class PacketDeduplicator
{
    readonly Dictionary<string, uint> last = [];

    public bool IsNew(string deviceId, BTHomePacket packet)
    {
        if ((packet.Counter ?? packet.PacketId) is not { } marker)
            return true;

        if (last.TryGetValue(deviceId, out var previous) && previous == marker)
            return false;

        last[deviceId] = marker;
        return true;
    }
}
