namespace ShellyBLE.Core.Buttons;

/// <summary>
/// Android refuse plus de cinq démarrages de scan par application sur 30 secondes, sans erreur visible :
/// le scan suivant ne reçoit simplement rien. On en garde un de marge et on fait attendre les relances
/// (nouveau bouton aperçu, adresse enregistrée) jusqu'à ce qu'un démarrage sorte de la fenêtre.
/// </summary>
public sealed class ScanStartBudget(TimeProvider? time = null)
{
    public const int MaxStarts = 4;

    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    readonly TimeProvider time = time ?? TimeProvider.System;
    readonly Queue<DateTimeOffset> starts = new();

    /// <summary>Note un démarrage de scan, qu'il ait attendu ou non.</summary>
    public void Record() => starts.Enqueue(time.GetUtcNow());

    /// <summary>Délai avant qu'un nouveau démarrage reste dans le quota (zéro s'il peut partir tout de suite).</summary>
    public TimeSpan WaitBeforeNextStart()
    {
        var now = time.GetUtcNow();
        while (starts.TryPeek(out var oldest) && now - oldest >= Window)
            starts.Dequeue();

        return starts.Count < MaxStarts ? TimeSpan.Zero : starts.Peek() + Window - now;
    }
}
