namespace ShellyBLE.Models;

/// <summary>Identité visuelle d'un type d'appui : icône, intitulé, mot du mode affichage.</summary>
public sealed record PressVisual(ButtonEvent Event, string Icon, string Title, string Word, string Hint)
{
    static readonly FrozenDictionary<ButtonEvent, PressVisual> All = new PressVisual[]
    {
        new(ButtonEvent.Press, Icons.One, "Appui simple", "CLICK", "Un appui bref"),
        new(ButtonEvent.DoublePress, Icons.Two, "Double appui", "DOUBLE CLICK", "Deux appuis rapprochés"),
        new(ButtonEvent.TriplePress, Icons.Three, "Triple appui", "TRIPLE CLICK", "Trois appuis rapprochés"),
        new(ButtonEvent.LongPress, Icons.TapHold, "Appui long", "LONG", "Maintenir environ une seconde"),
        new(ButtonEvent.LongDoublePress, Icons.TapHold, "Double appui long", "LONG", "Deux appuis, le second maintenu"),
        new(ButtonEvent.LongTriplePress, Icons.TapHold, "Triple appui long", "LONG", "Trois appuis, le dernier maintenu"),
        new(ButtonEvent.Hold, Icons.TapHold, "Maintien", "LONG", "Maintenir plusieurs secondes"),
    }.ToFrozenDictionary(v => v.Event);

    /// <summary>Types d'appui programmables, dans l'ordre d'affichage.</summary>
    public static ImmutableArray<PressVisual> Programmable { get; } = [.. ButtonEventExtensions.Actionable.Select(Of)];

    public static PressVisual Of(ButtonEvent buttonEvent) =>
        All.GetValueOrDefault(buttonEvent) ?? new(buttonEvent, Icons.Tap, buttonEvent.Label, buttonEvent.Label.ToUpperInvariant(), "");
}
