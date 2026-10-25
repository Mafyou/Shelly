#if DEBUG
using Android.Content;

namespace ShellyBLE;

/// <summary>Reçoit les scénarios adb (am broadcast -a fr.mafyou.shelly.AUTO --es steps "…").</summary>
public sealed class AutomationReceiver : BroadcastReceiver
{
    public const string Action = "fr.mafyou.shelly.AUTO";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.GetStringExtra("steps") is not { Length: > 0 } steps
            || IPlatformApplication.Current?.Services.GetService<DebugAutomation>() is not { } automation)
            return;

        MainThread.BeginInvokeOnMainThread(() => _ = automation.RunAsync(steps));
    }
}
#endif
