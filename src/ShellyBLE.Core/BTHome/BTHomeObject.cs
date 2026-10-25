namespace ShellyBLE.Core.BTHome;

/// <summary>Événement bouton BTHome (objet 0x3A).</summary>
public enum ButtonEvent : byte
{
    None = 0x00,
    Press = 0x01,
    DoublePress = 0x02,
    TriplePress = 0x03,
    LongPress = 0x04,
    LongDoublePress = 0x05,
    LongTriplePress = 0x06,
    Hold = 0x80,
}

/// <summary>Sens de rotation d'un variateur (objet 0x3C).</summary>
public enum DimmerDirection : byte
{
    None = 0x00,
    Left = 0x01,
    Right = 0x02,
}

/// <summary>Code d'opération d'une commande (objet 0x3B).</summary>
public enum BTHomeCommand : byte
{
    Off = 0x00,
    On = 0x01,
    Toggle = 0x02,
    StepUp = 0x03,
    StepDown = 0x04,
}

/// <summary>Compteur tournant (0x00) qui permet d'écarter les répétitions d'une même annonce.</summary>
public sealed record PacketId(byte Value);

/// <summary>Niveau de batterie en pourcentage (0x01).</summary>
public sealed record Battery(byte Percent);

/// <summary>Mesure numérique, facteur de la spécification déjà appliqué (ex. 0x02 CA09 → 25,06 °C).</summary>
public sealed record Measurement(byte Id, string Name, double Value, string? Unit);

/// <summary>Capteur binaire (0x0F–0x11, 0x15–0x2D).</summary>
public sealed record BinarySensor(byte Id, string Name, bool Value)
{
    /// <summary>Libellé de l'état courant (ex. « Ouverte » pour une porte).</summary>
    public string? State => BTHomeDefinitions.All.GetValueOrDefault(Id)?.States is { } states ? (Value ? states.On : states.Off) : null;
}

/// <summary>Horodatage (0x50), en secondes depuis 1970 côté appareil.</summary>
public sealed record Timestamp(DateTimeOffset Value);

/// <summary>Appui bouton (0x3A). <paramref name="Index"/> distingue les boutons des modèles à plusieurs touches.</summary>
public sealed record ButtonPressed(int Index, ButtonEvent Event);

/// <summary>Commande (0x3B) ; le sens des arguments dépend du code d'opération et du fabricant.</summary>
public sealed record Command(BTHomeCommand Opcode, ImmutableArray<byte> Arguments);

/// <summary>Rotation de variateur (0x3C). <paramref name="Index"/> distingue les variateurs comme pour les boutons.</summary>
public sealed record DimmerRotated(int Index, DimmerDirection Direction, int Steps);

/// <summary>Texte UTF-8 (0x53).</summary>
public sealed record TextValue(string Text);

/// <summary>Identifiant interne du type d'appareil (0xF0).</summary>
public sealed record DeviceTypeId(ushort Value);

/// <summary>Version du firmware (0xF1 ou 0xF2).</summary>
public sealed record FirmwareVersion(Version Version);

/// <summary>Objet BTHome sans interprétation : données brutes (0x54), sans l'octet de longueur.</summary>
public sealed record OtherObject(byte Id, ImmutableArray<byte> Data);

public union BTHomeObject(
    PacketId, Battery, Measurement, BinarySensor, Timestamp, ButtonPressed, Command, DimmerRotated, TextValue,
    DeviceTypeId, FirmwareVersion, OtherObject);
