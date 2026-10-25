namespace ShellyBLE.Core.Buttons;

public sealed record ActionSucceeded(int StatusCode, string Target)
{
    public string Message => $"{StatusCode} ← {Target}";
}

public sealed record ActionFailed(string Reason)
{
    public string Message => Reason;
}

/// <summary>Le Core ne sait pas ouvrir un lien : il le rend à l'application, qui l'ouvre.</summary>
public sealed record LinkToOpen(Uri Uri)
{
    public string Message => $"Lien à ouvrir : {Uri.OriginalString}";
}

public union ActionResult(ActionSucceeded, ActionFailed, LinkToOpen);

public interface IActionRunner
{
    Task<ActionResult> RunAsync(ButtonAction action, CancellationToken stoppingToken = default);
}

/// <summary>
/// Exécute l'action d'un appui : la requête HTTP d'un relais Shelly ou d'une requête avancée,
/// par exemple « GET http://192.168.1.50/rpc/Switch.Toggle?id=0 ». Un lien est rendu tel quel à l'application.
/// </summary>
public sealed class HttpActionRunner(HttpClient http) : IActionRunner
{
    public static bool IsValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    /// <summary>Toute adresse absolue, sauf les chemins de fichiers que <see cref="Uri"/> accepte aussi comme absolus.</summary>
    public static bool IsValidLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsLink(uri);

    static bool IsLink(Uri uri) => !uri.IsFile && !uri.IsUnc;

    public async Task<ActionResult> RunAsync(ButtonAction action, CancellationToken stoppingToken = default)
    {
        if (action is OpenLinkAction link)
        {
            return Uri.TryCreate(link.Url, UriKind.Absolute, out var linkUri) && IsLink(linkUri)
                ? new LinkToOpen(linkUri)
                : new ActionFailed($"Lien invalide : {link.Url}");
        }

        var request = action.Request!;
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return new ActionFailed($"URL invalide : {request.Url}");

        var target = $"{uri.Host}{uri.PathAndQuery}";
        try
        {
            using var message = new HttpRequestMessage(request.Verb is HttpVerb.Post ? HttpMethod.Post : HttpMethod.Get, uri);
            if (request is { Verb: HttpVerb.Post, Body: { } body })
            {
                if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType))
                    return new ActionFailed($"Type de contenu invalide : {request.ContentType}");
                message.Content = new StringContent(body, contentType);
            }

            using var response = await http.SendAsync(message, stoppingToken);
            return response.IsSuccessStatusCode
                ? new ActionSucceeded((int)response.StatusCode, target)
                : new ActionFailed($"{(int)response.StatusCode} {response.ReasonPhrase} ← {target}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ActionFailed($"Échec de l'action ({uri.Host}) : {ex.Message}");
        }
    }
}

public static class ActionResultExtensions
{
    extension(ActionResult result)
    {
        /// <summary>Un lien à ouvrir compte comme un succès : l'application s'en charge.</summary>
        public bool Succeeded => result is ActionSucceeded or LinkToOpen;

        public string Message => result switch
        {
            ActionSucceeded success => success.Message,
            ActionFailed failure => failure.Message,
            LinkToOpen link => link.Message,
        };
    }
}
