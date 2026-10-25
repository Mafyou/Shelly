namespace ShellyBLE.ViewModels;

/// <summary>Programmation d'un type d'appui, telle que résumée dans l'onglet « Programmation ».</summary>
public sealed record ProgramSlot(PressVisual Visual, ButtonAction? Action)
{
    public bool HasAction => Action is not null;

    public string Summary => Action?.Summary ?? "Aucune action";

    public string ActionIcon => Action switch
    {
        ShellyRelayAction => Icons.Power,
        OpenLinkAction => Icons.QrCode,
        HttpRequestAction => Icons.Api,
        _ => Icons.None,
    };
}

/// <summary>Choix du délai multi-clic dans le Picker (affiché via ToString).</summary>
public sealed record SpeedOption(MulticlickSpeed Value)
{
    public static ImmutableArray<SpeedOption> All { get; } = [.. Enum.GetValues<MulticlickSpeed>().Select(s => new SpeedOption(s))];

    public override string ToString() => $"{Value.Milliseconds} ms";
}
