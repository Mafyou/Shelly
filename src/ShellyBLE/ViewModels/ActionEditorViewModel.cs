namespace ShellyBLE.ViewModels;

public enum ActionKind
{
    None,
    ShellyRelay,
    OpenLink,
    Http,
}

/// <summary>
/// Programmation d'un type d'appui. Trois niveaux : un préréglage simple (relais Shelly), l'ouverture
/// d'un lien (comme un QR code) et une requête HTTP libre. L'aperçu montre toujours ce qui sera envoyé.
/// </summary>
public sealed partial class ActionEditorViewModel(ButtonsService buttons, IActionRunner runner) : ObservableObject, IQueryAttributable
{
    public const string IdParameter = "id";
    public const string EventParameter = "event";

    ButtonItem? item;

    [ObservableProperty]
    public partial PressVisual? Visual { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNone), nameof(IsRelay), nameof(IsLink), nameof(IsHttp), nameof(Preview), nameof(ProblemText), nameof(HasProblems), nameof(CanTest))]
    public partial ActionKind Kind { get; set; }

    public bool IsNone => Kind == ActionKind.None;

    public bool IsRelay => Kind == ActionKind.ShellyRelay;

    public bool IsLink => Kind == ActionKind.OpenLink;

    public bool IsHttp => Kind == ActionKind.Http;

    // Relais Shelly.

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview), nameof(ProblemText), nameof(HasProblems), nameof(CanTest))]
    public partial string? RelayHost { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview), nameof(ProblemText), nameof(HasProblems), nameof(ChannelText))]
    public partial double RelayChannel { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview), nameof(IsToggle), nameof(IsOn), nameof(IsOff))]
    public partial ShellyRelayCommand RelayCommand { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview))]
    public partial bool IsGen1 { get; set; }

    public string ChannelText => $"Canal {(int)RelayChannel}";

    public bool IsToggle => RelayCommand == ShellyRelayCommand.Toggle;

    public bool IsOn => RelayCommand == ShellyRelayCommand.On;

    public bool IsOff => RelayCommand == ShellyRelayCommand.Off;

    // Lien.

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview), nameof(ProblemText), nameof(HasProblems), nameof(CanTest))]
    public partial string? LinkUrl { get; set; }

    // Requête HTTP.

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview), nameof(ProblemText), nameof(HasProblems), nameof(CanTest))]
    public partial string? HttpUrl { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview), nameof(ProblemText), nameof(HasProblems), nameof(IsPost), nameof(IsGet))]
    public partial HttpVerb Verb { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview), nameof(ProblemText), nameof(HasProblems))]
    public partial string? HttpBody { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview))]
    public partial string HttpContentType { get; set; } = HttpRequestAction.DefaultContentType;

    public bool IsGet => Verb == HttpVerb.Get;

    public bool IsPost => Verb == HttpVerb.Post;

    // Aperçu, validation, test.

    /// <summary>Action telle que saisie ; null pour « Aucune action ».</summary>
    public ButtonAction? CurrentAction => Kind switch
    {
        ActionKind.ShellyRelay => new ShellyRelayAction(RelayHost?.Trim() ?? "", (int)RelayChannel, RelayCommand, IsGen1 ? ShellyGeneration.Gen1 : ShellyGeneration.Gen2),
        ActionKind.OpenLink => new OpenLinkAction(LinkUrl?.Trim() ?? ""),
        ActionKind.Http => new HttpRequestAction(HttpUrl?.Trim() ?? "", Verb, IsPost && !string.IsNullOrWhiteSpace(HttpBody) ? HttpBody : null, HttpContentType),
        _ => null,
    };

    public string Preview => CurrentAction?.Preview ?? "Aucune action : l'appui est seulement affiché et journalisé.";

    ImmutableArray<string> Problems => CurrentAction?.Validate() ?? [];

    public bool HasProblems => !Problems.IsEmpty;

    public string ProblemText => string.Join(Environment.NewLine, Problems);

    public bool CanTest => CurrentAction is not null && !HasProblems;

    [ObservableProperty]
    public partial string? TestResult { get; set; }

    [ObservableProperty]
    public partial bool TestFailed { get; set; }

    [ObservableProperty]
    public partial bool IsTesting { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(IdParameter, out var id) || id is not Guid buttonId || buttons.Find(buttonId) is not { } found
            || !query.TryGetValue(EventParameter, out var e) || e is not ButtonEvent buttonEvent)
            return;

        item = found;
        Visual = PressVisual.Of(buttonEvent);
        Load(ButtonDetailViewModel.ActionFor(buttons.GetSettings(found), buttonEvent));
    }

    void Load(ButtonAction? action)
    {
        switch (action)
        {
            case ShellyRelayAction relay:
                (RelayHost, RelayChannel, RelayCommand, IsGen1) = (relay.Host, relay.Channel, relay.Command, relay.Generation == ShellyGeneration.Gen1);
                Kind = ActionKind.ShellyRelay;
                break;
            case OpenLinkAction link:
                LinkUrl = link.Url;
                Kind = ActionKind.OpenLink;
                break;
            case HttpRequestAction http:
                (HttpUrl, Verb, HttpBody, HttpContentType) = (http.Url, http.Verb, http.Body, http.ContentType);
                Kind = ActionKind.Http;
                break;
            default:
                Kind = ActionKind.None;
                break;
        }
    }

    [RelayCommand]
    void SelectKind(ActionKind kind)
    {
        Kind = kind;
        TestResult = null;
    }

    [RelayCommand]
    void SelectRelayCommand(ShellyRelayCommand command) => RelayCommand = command;

    [RelayCommand]
    void ChangeChannel(string delta) =>
        RelayChannel = Math.Clamp(RelayChannel + int.Parse(delta, CultureInfo.InvariantCulture), 0, ShellyRelayAction.MaxChannel);

    [RelayCommand]
    void SelectVerb(HttpVerb verb) => Verb = verb;

    /// <summary>Modèles de liens courants : on garde la saisie si elle commence déjà par ce préfixe.</summary>
    [RelayCommand]
    void UseLinkTemplate(string prefix)
    {
        if (LinkUrl?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) != true)
            LinkUrl = prefix;
    }

    [RelayCommand]
    async Task TestAsync()
    {
        if (CurrentAction is not { } action || HasProblems || item is null)
            return;

        IsTesting = true;
        TestResult = "Exécution…";
        try
        {
            var result = await runner.RunAsync(action);
            await buttons.RecordAsync(item, result, $"Test : {Visual?.Title}");
            TestFailed = !result.Succeeded;
            TestResult = result switch
            {
                LinkToOpen link => $"Lien ouvert : {link.Uri}",
                _ => result.Message,
            };
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        if (item is null || Visual is null)
            return;

        var current = buttons.GetSettings(item) ?? new ButtonSettings { Id = $"{item.Id}" };
        var actions = current.Actions.ToDictionary(pair => pair.Key, pair => (ButtonAction?)pair.Value);
        actions[Visual.Event] = CurrentAction;

        var form = new ButtonSettingsForm(current.Id, current.Name, current.Mac, current.Key, actions.ToFrozenDictionary());
        switch (form.Validate())
        {
            case ButtonSettings settings:
                await buttons.SaveAsync(item, settings);
                await Shell.Current.GoToAsync("..");
                break;
            case SettingsErrors errors:
                TestFailed = true;
                TestResult = string.Join(Environment.NewLine, errors.Messages);
                break;
        }
    }
}
