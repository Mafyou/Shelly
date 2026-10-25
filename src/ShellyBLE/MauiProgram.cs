namespace ShellyBLE
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialDesignIcons.ttf", "Icons");
                });

            builder.Services
                .AddSingleton(_ => CrossBluetoothLE.Current)
                .AddSingleton(_ => CrossBluetoothLE.Current.Adapter)
                .AddSingleton<BleScanner>()
                .AddSingleton<IButtonSettingsStore>(_ => new JsonButtonSettingsStore(Path.Combine(FileSystem.AppDataDirectory, "buttons.json")))
                .AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                .AddSingleton<IActionRunner, HttpActionRunner>()
                .AddSingleton<ButtonMonitor>()
                .AddSingleton<ButtonsService>()
                .AddSingleton<ButtonsViewModel>()
                .AddSingleton<ButtonsPage>()
                .AddTransient<ButtonDetailViewModel>()
                .AddTransient<ButtonDetailPage>()
                .AddSingleton<LinkOpener>()
                .AddTransient<ActionEditorViewModel>()
                .AddTransient<ActionEditorPage>()
                .AddTransient<LivePressViewModel>()
                .AddTransient<LivePressPage>();

#if DEBUG
            builder.Logging.AddDebug().AddFilter("ShellyBLE", LogLevel.Debug);
            builder.Services.AddSingleton<DebugAutomation>();
#endif
#if ANDROID
            builder.Logging.AddProvider(new LogcatLoggerProvider());

            // Les champs sont dessinés dans un cadre (style « Field ») : pas de soulignement Android.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SansSoulignement", (handler, _) =>
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("SansSoulignement", (handler, _) =>
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

            return builder.Build();
        }
    }
}
