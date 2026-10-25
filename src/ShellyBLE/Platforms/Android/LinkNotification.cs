using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace ShellyBLE;

/// <summary>Notification « Appui détecté → toucher pour ouvrir » quand l'application n'est pas visible.</summary>
public static class LinkNotification
{
    const string ChannelId = "links";
    static int nextId = 100;

    public static void Show(Uri uri, string title)
    {
        var context = Platform.AppContext;
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && context.GetSystemService(Context.NotificationService) is NotificationManager manager)
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Liens des boutons", NotificationImportance.High));

        var open = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse($"{uri}"));
        open.AddFlags(ActivityFlags.NewTask);
        var id = Interlocked.Increment(ref nextId);
        var pending = PendingIntent.GetActivity(context, id, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetContentTitle(title);
        builder.SetContentText($"Toucher pour ouvrir {uri}");
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetContentIntent(pending);
        builder.SetAutoCancel(true);
        builder.SetPriority(NotificationCompat.PriorityHigh);
        NotificationManagerCompat.From(context)!.Notify(id, builder.Build()!);
    }
}
