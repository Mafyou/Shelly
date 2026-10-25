namespace ShellyBLE.Views;

public partial class LivePressPage : ContentPage
{
    readonly LivePressViewModel viewModel;

    public LivePressPage(LivePressViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = true;
        viewModel.PropertyChanged += OnViewModelChanged;
        viewModel.Start();
    }

    protected override void OnDisappearing()
    {
        viewModel.Stop();
        viewModel.PropertyChanged -= OnViewModelChanged;
        DeviceDisplay.Current.KeepScreenOn = false;
        base.OnDisappearing();
    }

    /// <summary>Flash et pulsation à chaque appui, pour qu'on voie aussi deux appuis identiques de suite.</summary>
    async void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LivePressViewModel.PressCount))
            return;

        Flash.Opacity = 0.35;
        WordLabel.Scale = 1.2;
        IconLabel.Scale = 1.3;
        await Task.WhenAll(
            Flash.FadeToAsync(0, 450),
            WordLabel.ScaleToAsync(1, 260, Easing.CubicOut),
            IconLabel.ScaleToAsync(1, 320, Easing.CubicOut));
    }
}
