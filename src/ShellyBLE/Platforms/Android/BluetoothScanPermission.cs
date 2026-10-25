namespace ShellyBLE;

/// <summary>
/// Scan et connexion BLE. Depuis Android 12, le scan est déclaré « neverForLocation » : la localisation
/// n'est plus demandée ni nécessaire. Avant, le scan BLE exige la localisation précise.
/// </summary>
public sealed class BluetoothScanPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(31)
            ? [(AndroidPermission.BluetoothScan, true), (AndroidPermission.BluetoothConnect, true), .. OperatingSystem.IsAndroidVersionAtLeast(33) ? [(AndroidPermission.PostNotifications, true)] : Array.Empty<(string, bool)>()]
            : [(AndroidPermission.AccessFineLocation, true)];
}
