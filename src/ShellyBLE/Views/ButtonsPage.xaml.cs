namespace ShellyBLE.Views;

public partial class ButtonsPage : ContentPage
{
    readonly ButtonsViewModel viewModel;

    public ButtonsPage(ButtonsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.AppearingCommand.Execute(null);
    }

    void OnButtonTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ButtonItem item)
            viewModel.OpenCommand.Execute(item);
    }
}
