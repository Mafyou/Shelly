namespace ShellyBLE.Services;

public static class BlePermissions
{
    /// <summary>Android exige des autorisations explicites ; iOS et macOS les demandent au premier scan.</summary>
    public static async Task<bool> RequestAsync()
    {
#if ANDROID
        return await Permissions.RequestAsync<BluetoothScanPermission>() == PermissionStatus.Granted;
#else
        return await Task.FromResult(true);
#endif
    }
}
