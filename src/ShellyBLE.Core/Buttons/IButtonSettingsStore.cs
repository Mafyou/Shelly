namespace ShellyBLE.Core.Buttons;

public interface IButtonSettingsStore
{
    IEnumerable<ButtonSettings> All { get; }

    ButtonSettings? Find(string id);

    Task SaveAsync(ButtonSettings settings, CancellationToken stoppingToken = default);

    Task RemoveAsync(string id, CancellationToken stoppingToken = default);
}

/// <summary>Stockage dans un fichier JSON, réécrit entièrement à chaque modification.</summary>
public sealed class JsonButtonSettingsStore : IButtonSettingsStore
{
    readonly string path;
    readonly Dictionary<string, ButtonSettings> settings;

    public JsonButtonSettingsStore(string path)
    {
        this.path = path;
        settings = Load().ToDictionary(s => s.Id);
    }

    public IEnumerable<ButtonSettings> All => settings.Values;

    public ButtonSettings? Find(string id) => settings.GetValueOrDefault(id);

    public Task SaveAsync(ButtonSettings item, CancellationToken stoppingToken = default)
    {
        settings[item.Id] = item;
        return WriteAsync(stoppingToken);
    }

    public Task RemoveAsync(string id, CancellationToken stoppingToken = default) =>
        settings.Remove(id) ? WriteAsync(stoppingToken) : Task.CompletedTask;

    ImmutableArray<ButtonSettings> Load()
    {
        try
        {
            if (!File.Exists(path))
                return [];
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.ImmutableArrayButtonSettings);
        }
        catch (JsonException)
        {
            // Fichier corrompu : on repart d'une liste vide plutôt que d'empêcher le démarrage.
            return [];
        }
    }

    async Task WriteAsync(CancellationToken stoppingToken)
    {
        var temp = $"{path}.tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, [.. settings.Values], SettingsJsonContext.Default.ImmutableArrayButtonSettings, stoppingToken);
        File.Move(temp, path, overwrite: true);
    }
}
