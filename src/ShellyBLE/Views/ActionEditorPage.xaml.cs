namespace ShellyBLE.Views;

public partial class ActionEditorPage : ContentPage
{
    public ActionEditorPage(ActionEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
