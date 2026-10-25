namespace ShellyBLE.Services;

/// <summary>
/// Ouvre le lien d'une action « Ouvrir un lien », comme le ferait un QR code scanné.
/// Android n'autorise une application à ouvrir une page que si elle est visible : en arrière-plan,
/// on affiche une notification à toucher.
/// </summary>
public sealed class LinkOpener(ILogger<LinkOpener> logger)
{
    /// <summary>Mis à jour par la fenêtre de l'application (App.CreateWindow).</summary>
    public static bool IsInForeground { get; set; } = true;

    public async Task<bool> OpenAsync(Uri uri, string title)
    {
        if (IsInForeground)
        {
            var opened = await Launcher.Default.TryOpenAsync(uri);
            logger.LogInformation("Lien {Uri} ouvert : {Opened}", uri, opened);
            return opened;
        }

#if ANDROID
        LinkNotification.Show(uri, title);
        logger.LogInformation("Lien {Uri} proposé en notification (application en arrière-plan)", uri);
        return true;
#else
        return false;
#endif
    }
}
