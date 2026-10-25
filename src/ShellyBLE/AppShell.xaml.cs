namespace ShellyBLE
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ButtonDetailPage), typeof(ButtonDetailPage));
            Routing.RegisterRoute(nameof(LivePressPage), typeof(LivePressPage));
            Routing.RegisterRoute(nameof(ActionEditorPage), typeof(ActionEditorPage));
        }
    }
}
