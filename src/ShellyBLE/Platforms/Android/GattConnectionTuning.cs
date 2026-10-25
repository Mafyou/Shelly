using System.Reflection;
using Android.Bluetooth;

namespace ShellyBLE;

/// <summary>
/// Capture HCI (OnePlus 7 Pro / Android 17, BLU Button1 en 1.0.2) : le bouton perd la liaison ~200 ms après
/// toute procédure de mise à jour des paramètres de connexion, quels qu'ils soient (latence 1 à 15 ms,
/// 30 ms sans latence…). Seule parade côté téléphone : ne jamais déclencher cette procédure.
/// Android en lance une de lui-même (relâchement des paramètres initiaux) sauf si les paramètres demandés
/// par l'application sont déjà ceux de la liaison : on les demande donc à l'identique, via la méthode
/// cachée BluetoothGatt.requestLeConnectionUpdate (aucune API publique ne prend de paramètres exacts).
/// </summary>
public static class GattConnectionTuning
{
    /// <summary>Paramètres initiaux observés : intervalle 12 × 1,25 ms, latence 0, supervision 500 × 10 ms.</summary>
    const int Interval = 12;
    const int Latency = 0;
    const int SupervisionTimeout = 500;

    public static bool KeepInitialParameters(object plugInDevice, ILogger logger)
    {
        if (plugInDevice.GetType().GetField("_gatt", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(plugInDevice) is not BluetoothGatt gatt)
        {
            logger.LogWarning("BluetoothGatt introuvable dans Plugin.BLE");
            return false;
        }

        try
        {
            var intType = Java.Lang.Integer.Type!;
            var method = gatt.Class.GetMethod("requestLeConnectionUpdate", intType, intType, intType, intType, intType, intType);
            var result = method.Invoke(gatt,
                Java.Lang.Integer.ValueOf(Interval), Java.Lang.Integer.ValueOf(Interval), Java.Lang.Integer.ValueOf(Latency),
                Java.Lang.Integer.ValueOf(SupervisionTimeout), Java.Lang.Integer.ValueOf(0), Java.Lang.Integer.ValueOf(0));
            var accepted = result is Java.Lang.Boolean b && b.BooleanValue();
            logger.LogInformation("requestLeConnectionUpdate({Interval}, {Interval}, {Latency}, {Timeout}) : {Accepted}", Interval, Interval, Latency, SupervisionTimeout, accepted);
            return accepted;
        }
        catch (Java.Lang.Exception ex)
        {
            // Méthode filtrée par les restrictions d'API cachées de cette version d'Android.
            logger.LogWarning("requestLeConnectionUpdate indisponible : {Error}", ex.Message);
            return false;
        }
    }
}
