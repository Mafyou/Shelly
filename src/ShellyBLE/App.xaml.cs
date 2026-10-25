namespace ShellyBLE
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            // Les liens ne s'ouvrent directement que si l'application est visible (cf. LinkOpener).
            window.Resumed += (_, _) => LinkOpener.IsInForeground = true;
            window.Activated += (_, _) => LinkOpener.IsInForeground = true;
            window.Stopped += (_, _) => LinkOpener.IsInForeground = false;
            return window;
        }
    }
}
