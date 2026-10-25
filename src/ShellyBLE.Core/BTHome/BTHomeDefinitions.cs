namespace ShellyBLE.Core.BTHome;

/// <summary>Codage de la valeur qui suit l'identifiant d'objet (entiers little-endian).</summary>
public enum BTHomeValueType
{
    UInt8,
    UInt16,
    UInt24,
    UInt32,
    SInt8,
    SInt16,
    SInt32,

    /// <summary>Texte (0x53) et données brutes (0x54) : un octet de longueur puis les données.</summary>
    LengthPrefixed,

    /// <summary>Commande (0x3B) : longueur des arguments (5 bits de poids faible), code d'opération, arguments.</summary>
    Command,
}

/// <summary>Sections de la spécification BTHome v2.</summary>
public enum BTHomeObjectKind
{
    Sensor,
    BinarySensor,
    Event,
    Device,
    Misc,
}

/// <summary>Libellés des deux états d'un capteur binaire.</summary>
public readonly record struct BinaryStates(string Off, string On);

/// <summary>
/// Définition d'un objet BTHome. Le facteur est décimal pour que 2506 × 0,01 donne exactement 25,06
/// une fois converti en double (en double, le produit vaudrait 25,060000000000002).
/// </summary>
public sealed record BTHomeObjectDefinition(
    byte Id, string Name, BTHomeObjectKind Kind, BTHomeValueType Type,
    decimal Factor = 1, string? Unit = null, BinaryStates? States = null);

/// <summary>Objets de la spécification BTHome v2 (https://bthome.io/format/), propriétés et unités telles qu'elle les donne.</summary>
public static class BTHomeDefinitions
{
    public static FrozenDictionary<byte, BTHomeObjectDefinition> All { get; } = new BTHomeObjectDefinition[]
    {
        new(0x00, "packet id", BTHomeObjectKind.Misc, BTHomeValueType.UInt8),
        U8(0x01, "battery", 1, "%"),
        S16(0x02, "temperature", 0.01m, "°C"),
        U16(0x03, "humidity", 0.01m, "%"),
        U24(0x04, "pressure", 0.01m, "hPa"),
        U24(0x05, "illuminance", 0.01m, "lx"),
        U16(0x06, "mass", 0.01m, "kg"),
        U16(0x07, "mass", 0.01m, "lb"),
        S16(0x08, "dewpoint", 0.01m, "°C"),
        U8(0x09, "count"),
        U24(0x0A, "energy", 0.001m, "kWh"),
        U24(0x0B, "power", 0.01m, "W"),
        U16(0x0C, "voltage", 0.001m, "V"),
        U16(0x0D, "pm2.5", 1, "ug/m3"),
        U16(0x0E, "pm10", 1, "ug/m3"),
        Binary(0x0F, "generic boolean", "Arrêt", "Marche"),
        Binary(0x10, "power", "Éteint", "Allumé"),
        Binary(0x11, "opening", "Fermé", "Ouvert"),
        U16(0x12, "co2", 1, "ppm"),
        U16(0x13, "tvoc", 1, "ug/m3"),
        U16(0x14, "moisture", 0.01m, "%"),
        Binary(0x15, "battery", "Normale", "Faible"),
        Binary(0x16, "battery charging", "Pas en charge", "En charge"),
        Binary(0x17, "carbon monoxide", "Non détecté", "Détecté"),
        Binary(0x18, "cold", "Normal", "Froid"),
        Binary(0x19, "connectivity", "Déconnecté", "Connecté"),
        Binary(0x1A, "door", "Fermée", "Ouverte"),
        Binary(0x1B, "garage door", "Fermée", "Ouverte"),
        Binary(0x1C, "gas", "Non détecté", "Détecté"),
        Binary(0x1D, "heat", "Normal", "Chaud"),
        Binary(0x1E, "light", "Pas de lumière", "Lumière détectée"),
        Binary(0x1F, "lock", "Verrouillé", "Déverrouillé"),
        Binary(0x20, "moisture", "Sec", "Humide"),
        Binary(0x21, "motion", "Non détecté", "Détecté"),
        Binary(0x22, "moving", "Immobile", "En mouvement"),
        Binary(0x23, "occupancy", "Libre", "Occupé"),
        Binary(0x24, "plug", "Débranché", "Branché"),
        Binary(0x25, "presence", "Absent", "Présent"),
        Binary(0x26, "problem", "OK", "Problème"),
        Binary(0x27, "running", "À l'arrêt", "En marche"),
        Binary(0x28, "safety", "Danger", "Sûr"),
        Binary(0x29, "smoke", "Non détectée", "Détectée"),
        Binary(0x2A, "sound", "Non détecté", "Détecté"),
        Binary(0x2B, "tamper", "Inactif", "Actif"),
        Binary(0x2C, "vibration", "Non détectée", "Détectée"),
        Binary(0x2D, "window", "Fermée", "Ouverte"),
        U8(0x2E, "humidity", 1, "%"),
        U8(0x2F, "moisture", 1, "%"),
        new(0x3A, "button", BTHomeObjectKind.Event, BTHomeValueType.UInt8),
        new(0x3B, "command", BTHomeObjectKind.Event, BTHomeValueType.Command),
        // Deux octets distincts : sens de rotation puis nombre de pas.
        new(0x3C, "dimmer", BTHomeObjectKind.Event, BTHomeValueType.UInt16),
        U16(0x3D, "count"),
        U32(0x3E, "count"),
        S16(0x3F, "rotation", 0.1m, "°"),
        U16(0x40, "distance", 1, "mm"),
        U16(0x41, "distance", 0.1m, "m"),
        U24(0x42, "duration", 0.001m, "s"),
        U16(0x43, "current", 0.001m, "A"),
        U16(0x44, "speed", 0.01m, "m/s"),
        S16(0x45, "temperature", 0.1m, "°C"),
        U8(0x46, "UV index", 0.1m),
        U16(0x47, "volume", 0.1m, "L"),
        U16(0x48, "volume", 1, "mL"),
        U16(0x49, "volume flow rate", 0.001m, "m3/hr"),
        U16(0x4A, "voltage", 0.1m, "V"),
        U24(0x4B, "gas", 0.001m, "m3"),
        U32(0x4C, "gas", 0.001m, "m3"),
        U32(0x4D, "energy", 0.001m, "kWh"),
        U32(0x4E, "volume", 0.001m, "L"),
        U32(0x4F, "water", 0.001m, "L"),
        // Secondes depuis le 1er janvier 1970 UTC.
        U32(0x50, "timestamp", 1, "s"),
        U16(0x51, "acceleration", 0.001m, "m/s²"),
        U16(0x52, "gyroscope", 0.001m, "°/s"),
        new(0x53, "text", BTHomeObjectKind.Sensor, BTHomeValueType.LengthPrefixed),
        new(0x54, "raw", BTHomeObjectKind.Sensor, BTHomeValueType.LengthPrefixed),
        U32(0x55, "volume storage", 0.001m, "L"),
        U16(0x56, "conductivity", 1, "µS/cm"),
        S8(0x57, "temperature", 1, "°C"),
        S8(0x58, "temperature", 0.35m, "°C"),
        S8(0x59, "count"),
        S16(0x5A, "count"),
        S32(0x5B, "count"),
        S32(0x5C, "power", 0.01m, "W"),
        S16(0x5D, "current", 0.001m, "A"),
        U16(0x5E, "direction", 0.01m, "°"),
        U16(0x5F, "precipitation", 0.1m, "mm"),
        U8(0x60, "channel"),
        U16(0x61, "rotational speed", 1, "rpm"),
        S32(0x62, "speed", 0.000001m, "m/s"),
        S32(0x63, "acceleration", 0.000001m, "m/s²"),
        // 0 sombre, 1 crépuscule, 2 lumineux.
        U8(0x64, "light level"),
        U8(0x65, "settings revision"),
        new(0xF0, "device type id", BTHomeObjectKind.Device, BTHomeValueType.UInt16),
        new(0xF1, "firmware version", BTHomeObjectKind.Device, BTHomeValueType.UInt32),
        new(0xF2, "firmware version", BTHomeObjectKind.Device, BTHomeValueType.UInt24),
    }
    .ToFrozenDictionary(definition => definition.Id);

    static BTHomeObjectDefinition U8(byte id, string name, decimal factor = 1, string? unit = null) => Sensor(id, name, BTHomeValueType.UInt8, factor, unit);
    static BTHomeObjectDefinition U16(byte id, string name, decimal factor = 1, string? unit = null) => Sensor(id, name, BTHomeValueType.UInt16, factor, unit);
    static BTHomeObjectDefinition U24(byte id, string name, decimal factor = 1, string? unit = null) => Sensor(id, name, BTHomeValueType.UInt24, factor, unit);
    static BTHomeObjectDefinition U32(byte id, string name, decimal factor = 1, string? unit = null) => Sensor(id, name, BTHomeValueType.UInt32, factor, unit);
    static BTHomeObjectDefinition S8(byte id, string name, decimal factor = 1, string? unit = null) => Sensor(id, name, BTHomeValueType.SInt8, factor, unit);
    static BTHomeObjectDefinition S16(byte id, string name, decimal factor = 1, string? unit = null) => Sensor(id, name, BTHomeValueType.SInt16, factor, unit);
    static BTHomeObjectDefinition S32(byte id, string name, decimal factor = 1, string? unit = null) => Sensor(id, name, BTHomeValueType.SInt32, factor, unit);

    static BTHomeObjectDefinition Sensor(byte id, string name, BTHomeValueType type, decimal factor, string? unit) =>
        new(id, name, BTHomeObjectKind.Sensor, type, factor, unit);

    static BTHomeObjectDefinition Binary(byte id, string name, string off, string on) =>
        new(id, name, BTHomeObjectKind.BinarySensor, BTHomeValueType.UInt8, States: new(off, on));
}

public static class BTHomeValueTypeExtensions
{
    extension(BTHomeValueType type)
    {
        /// <summary>Longueur de la valeur sans l'identifiant ; null si l'octet de longueur manque.</summary>
        public int? Length(ReadOnlySpan<byte> rest) => type switch
        {
            BTHomeValueType.UInt8 or BTHomeValueType.SInt8 => 1,
            BTHomeValueType.UInt16 or BTHomeValueType.SInt16 => 2,
            BTHomeValueType.UInt24 => 3,
            BTHomeValueType.UInt32 or BTHomeValueType.SInt32 => 4,
            BTHomeValueType.LengthPrefixed when !rest.IsEmpty => 1 + rest[0],
            // Les 3 bits de poids fort sont réservés.
            BTHomeValueType.Command when !rest.IsEmpty => 2 + (rest[0] & 0x1F),
            _ => null,
        };

        public long ReadInteger(ReadOnlySpan<byte> value) => type switch
        {
            BTHomeValueType.UInt8 => value[0],
            BTHomeValueType.UInt16 => BinaryPrimitives.ReadUInt16LittleEndian(value),
            BTHomeValueType.UInt24 => value[0] | value[1] << 8 | value[2] << 16,
            BTHomeValueType.UInt32 => BinaryPrimitives.ReadUInt32LittleEndian(value),
            BTHomeValueType.SInt8 => (sbyte)value[0],
            BTHomeValueType.SInt16 => BinaryPrimitives.ReadInt16LittleEndian(value),
            BTHomeValueType.SInt32 => BinaryPrimitives.ReadInt32LittleEndian(value),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"{type} n'est pas un entier."),
        };
    }
}
