using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace ShellyBLE;

/// <summary>
/// Service de premier plan pendant le scan. Écran éteint, Android réduit le scan d'une application sans
/// service de premier plan à ~5 % du temps (SCAN_MODE_SCREEN_OFF) : les rafales d'un appui (~1 s) passent
/// entre les fenêtres. Avec ce service, il applique SCAN_MODE_SCREEN_OFF_BALANCED (~25 %, fenêtre toutes
/// les 730 ms) et l'application n'est pas suspendue.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeConnectedDevice)]
public sealed class ScanForegroundService : Service
{
    const string ChannelId = "scan";
    const int NotificationId = 1;

    public static void Start()
    {
        var intent = new Intent(Platform.AppContext, typeof(ScanForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            Platform.AppContext.StartForegroundService(intent);
        else
            Platform.AppContext.StartService(intent);
    }

    public static void Stop() =>
        Platform.AppContext.StopService(new Intent(Platform.AppContext, typeof(ScanForegroundService)));

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && GetSystemService(NotificationService) is NotificationManager manager)
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Écoute des boutons", NotificationImportance.Low));

        var openApp = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable);
        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetContentTitle("Écoute des boutons Shelly BLU");
        builder.SetContentText("Les appuis sont détectés même écran éteint.");
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetContentIntent(openApp);
        builder.SetOngoing(true);
        var notification = builder.Build()!;

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification, ForegroundService.TypeConnectedDevice);
        else
            StartForeground(NotificationId, notification);
        return StartCommandResult.Sticky;
    }
}
