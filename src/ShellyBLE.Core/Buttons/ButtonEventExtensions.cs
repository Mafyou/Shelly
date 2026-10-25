namespace ShellyBLE.Core.Buttons;

public static class ButtonEventExtensions
{
    /// <summary>Événements qu'un BLU Button1 peut associer à une action.</summary>
    public static ImmutableArray<ButtonEvent> Actionable { get; } =
        [ButtonEvent.Press, ButtonEvent.DoublePress, ButtonEvent.TriplePress, ButtonEvent.LongPress];

    static readonly FrozenDictionary<ButtonEvent, string> Labels = new Dictionary<ButtonEvent, string>
    {
        [ButtonEvent.None] = "Aucun",
        [ButtonEvent.Press] = "Appui simple",
        [ButtonEvent.DoublePress] = "Double appui",
        [ButtonEvent.TriplePress] = "Triple appui",
        [ButtonEvent.LongPress] = "Appui long",
        [ButtonEvent.LongDoublePress] = "Double appui long",
        [ButtonEvent.LongTriplePress] = "Triple appui long",
        [ButtonEvent.Hold] = "Maintien",
    }.ToFrozenDictionary();

    extension(ButtonEvent buttonEvent)
    {
        public string Label => Labels.GetValueOrDefault(buttonEvent) ?? $"Événement 0x{(byte)buttonEvent:X2}";
    }
}
