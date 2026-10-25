namespace ShellyBLE.Views;

public partial class ButtonDetailPage : ContentPage
{
    readonly ButtonDetailViewModel viewModel;

    public ButtonDetailPage(ButtonDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    /// <summary>Au retour de l'éditeur d'action, la programmation affichée doit refléter l'enregistrement.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.Reload();
    }

    /// <summary>
    /// On libère la connexion GATT en quittant la fiche (le bouton ne gère que peu de connexions),
    /// mais pas en ouvrant l'éditeur d'action, qui reste au-dessus de cette page.
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!Navigation.NavigationStack.Contains(this))
            viewModel.DisconnectCommand.Execute(null);
    }
}
