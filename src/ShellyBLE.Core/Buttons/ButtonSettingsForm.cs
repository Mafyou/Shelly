namespace ShellyBLE.Core.Buttons;

public sealed record SettingsErrors(ImmutableArray<string> Messages);

public union SettingsValidation(ButtonSettings, SettingsErrors);

/// <summary>Saisie brute de l'écran de réglages, validée avant d'être enregistrée.</summary>
public sealed record ButtonSettingsForm(
    string Id,
    string? Name,
    string? Mac,
    string? Key,
    IReadOnlyDictionary<ButtonEvent, ButtonAction?> Actions)
{
    public SettingsValidation Validate()
    {
        var errors = ImmutableArray.CreateBuilder<string>();

        MacAddress? mac = null;
        if (!string.IsNullOrWhiteSpace(Mac))
        {
            if (MacAddress.TryParse(Mac, out var parsed))
                mac = parsed;
            else
                errors.Add("Adresse MAC invalide (format AA:BB:CC:DD:EE:FF).");
        }

        ImmutableArray<byte>? key = null;
        if (!string.IsNullOrWhiteSpace(Key))
        {
            if (TryParseKey(Key, out var parsed))
                key = parsed;
            else
                errors.Add($"La clé de chiffrement doit compter {BTHomeCipher.KeyLength * 2} caractères hexadécimaux.");
        }

        var actions = new Dictionary<ButtonEvent, ButtonAction>();
        foreach (var (buttonEvent, entered) in Actions)
        {
            if (entered is not { } action)
                continue;
            var normalized = action.Normalized;
            var problems = normalized.Validate();
            if (problems.IsEmpty)
                actions[buttonEvent] = normalized;
            else
                errors.AddRange(problems.Select(problem => $"{buttonEvent.Label} : {problem}"));
        }

        if (errors.Count > 0)
            return new SettingsErrors(errors.ToImmutable());

        return new ButtonSettings
        {
            Id = Id,
            Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
            Mac = mac is { } m ? $"{m}" : null,
            Key = key is { } k ? Convert.ToHexStringLower(k.AsSpan()) : null,
            Actions = actions.ToFrozenDictionary(),
        };
    }

    /// <summary>Accepte espaces, deux-points et tirets entre les octets.</summary>
    public static bool TryParseKey(string? text, out ImmutableArray<byte> key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var hex = text.Replace(" ", "").Replace(":", "").Replace("-", "");
        if (hex.Length != BTHomeCipher.KeyLength * 2 || !hex.All(char.IsAsciiHexDigit))
            return false;

        key = [.. Convert.FromHexString(hex)];
        return true;
    }
}
