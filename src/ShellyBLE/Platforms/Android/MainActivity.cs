using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace ShellyBLE
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
#if DEBUG
        readonly AutomationReceiver automation = new();

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var filter = new IntentFilter(AutomationReceiver.Action);
            // Exporté pour recevoir les diffusions d'adb ; build Debug uniquement.
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                RegisterReceiver(automation, filter, ReceiverFlags.Exported);
            else
                RegisterReceiver(automation, filter);
        }

        protected override void OnDestroy()
        {
            UnregisterReceiver(automation);
            base.OnDestroy();
        }
#endif
    }
}
