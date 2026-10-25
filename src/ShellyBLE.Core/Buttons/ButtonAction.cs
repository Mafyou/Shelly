namespace ShellyBLE.Core.Buttons;

public enum ShellyRelayCommand { Toggle, On, Off }

/// <summary>Gen2 couvre aussi Gen3, Gen4 et Pro (API RPC) ; Gen1 est l'ancienne API REST.</summary>
public enum ShellyGeneration { Gen2, Gen1 }

/// <summary>Préréglage simple : piloter un relais Shelly du réseau local.</summary>
public sealed record ShellyRelayAction(string Host, int Channel, ShellyRelayCommand Command, ShellyGeneration Generation = ShellyGeneration.Gen2)
{
    /// <summary>Plus haut numéro de relais accepté (les Shelly Pro en comptent au plus quelques-uns).</summary>
    public const int MaxChannel = 15;
}

public enum HttpVerb { Get, Post }

/// <summary>Mode avancé : n'importe quelle requête HTTP.</summary>
public sealed record HttpRequestAction(string Url, HttpVerb Verb = HttpVerb.Get, string? Body = null, string ContentType = HttpRequestAction.DefaultContentType)
{
    public const string DefaultContentType = "application/json";
}

/// <summary>« Comme un QR code » : le téléphone ouvre ce lien (page web, lien d'application, tel:, geo:, sms:…).</summary>
public sealed record OpenLinkAction(string Url);

// Le convertisseur gère le discriminant « kind » et l'ancien format (URL seule) : le classifieur
// structurel de System.Text.Json, qui ne distingue pas trois objets JSON, ne sert pas ici.
#pragma warning disable SYSLIB1227
[JsonConverter(typeof(ButtonActionJsonConverter))]
public union ButtonAction(ShellyRelayAction, HttpRequestAction, OpenLinkAction);
#pragma warning restore SYSLIB1227

public static class ButtonActionExtensions
{
    extension(ButtonAction action)
    {
        /// <summary>Résumé court pour les listes.</summary>
        public string Summary => action switch
        {
            ShellyRelayAction relay => $"{Verb(relay.Command)} le relais {relay.Channel} de {relay.Host}",
            HttpRequestAction http => $"{Method(http.Verb)} {http.Url}",
            OpenLinkAction link => $"Ouvrir {link.Url}",
        };

        /// <summary>Ce qui sera réellement envoyé ou ouvert, sur plusieurs lignes.</summary>
        public string Preview => action switch
        {
            OpenLinkAction link => $"Ouvrir : {link.Url}",
            _ => Describe(action.Request!),
        };

        /// <summary>La requête à laquelle l'action se ramène (null pour un lien à ouvrir).</summary>
        public HttpRequestAction? Request => action switch
        {
            ShellyRelayAction relay => new HttpRequestAction(RelayUrl(relay)),
            HttpRequestAction http => http,
            OpenLinkAction => null,
        };

        /// <summary>Problèmes de saisie, sans le préfixe du type d'appui (vide si l'action est valide).</summary>
        public ImmutableArray<string> Validate()
        {
            var errors = ImmutableArray.CreateBuilder<string>();
            switch (action)
            {
                case ShellyRelayAction relay:
                    if (string.IsNullOrWhiteSpace(relay.Host))
                        errors.Add("Adresse du relais manquante.");
                    else if (!IsValidHost(relay.Host))
                        errors.Add($"Adresse du relais invalide ({relay.Host}) : nom d'hôte ou IP attendu, éventuellement suivi de :port.");
                    if (relay.Channel is < 0 or > ShellyRelayAction.MaxChannel)
                        errors.Add($"Le numéro de relais doit être compris entre 0 et {ShellyRelayAction.MaxChannel}.");
                    break;

                case HttpRequestAction http:
                    if (!HttpActionRunner.IsValidUrl(http.Url))
                        errors.Add("URL http(s) invalide.");
                    if (http.Body is not null && http.Verb is not HttpVerb.Post)
                        errors.Add("Un corps de requête n'est possible qu'en POST.");
                    if (http.Body is not null && !MediaTypeHeaderValue.TryParse(http.ContentType, out _))
                        errors.Add($"Type de contenu invalide ({http.ContentType}).");
                    break;

                case OpenLinkAction link:
                    if (!HttpActionRunner.IsValidLink(link.Url))
                        errors.Add("Lien invalide : adresse complète attendue (https://…, tel:…, geo:…, sms:…).");
                    break;
            }
            return errors.ToImmutable();
        }

        /// <summary>Retire les espaces superflus ; un corps vide équivaut à aucun corps.</summary>
        internal ButtonAction Normalized => action switch
        {
            ShellyRelayAction relay => relay with { Host = relay.Host.Trim() },
            HttpRequestAction http => http with
            {
                Url = http.Url.Trim(),
                Body = string.IsNullOrWhiteSpace(http.Body) ? null : http.Body,
                ContentType = string.IsNullOrWhiteSpace(http.ContentType) ? HttpRequestAction.DefaultContentType : http.ContentType.Trim(),
            },
            OpenLinkAction link => link with { Url = link.Url.Trim() },
        };
    }

    static string Verb(ShellyRelayCommand command) => command switch
    {
        ShellyRelayCommand.Toggle => "Basculer",
        ShellyRelayCommand.On => "Allumer",
        ShellyRelayCommand.Off => "Éteindre",
        _ => $"{command}",
    };

    internal static string Method(HttpVerb verb) => verb is HttpVerb.Post ? "POST" : "GET";

    static string RelayUrl(ShellyRelayAction relay) => (relay.Generation, relay.Command) switch
    {
        (ShellyGeneration.Gen1, ShellyRelayCommand.On) => $"http://{relay.Host}/relay/{relay.Channel}?turn=on",
        (ShellyGeneration.Gen1, ShellyRelayCommand.Off) => $"http://{relay.Host}/relay/{relay.Channel}?turn=off",
        (ShellyGeneration.Gen1, _) => $"http://{relay.Host}/relay/{relay.Channel}?turn=toggle",
        (_, ShellyRelayCommand.On) => $"http://{relay.Host}/rpc/Switch.Set?id={relay.Channel}&on=true",
        (_, ShellyRelayCommand.Off) => $"http://{relay.Host}/rpc/Switch.Set?id={relay.Channel}&on=false",
        _ => $"http://{relay.Host}/rpc/Switch.Toggle?id={relay.Channel}",
    };

    static string Describe(HttpRequestAction request) => request is { Verb: HttpVerb.Post, Body: { } body }
        ? $"POST {request.Url}\nContent-Type: {request.ContentType}\n\n{body}"
        : $"{Method(request.Verb)} {request.Url}";

    /// <summary>Nom d'hôte, IPv4 ou [IPv6], avec un port facultatif : rien d'autre (ni chemin, ni identifiants).</summary>
    static bool IsValidHost(string host) =>
        !host.Any(c => char.IsWhiteSpace(c) || c is '/' or '\\' or '@' or '?' or '#')
        && Uri.TryCreate($"http://{host}/", UriKind.Absolute, out var uri)
        && uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6
        && uri.PathAndQuery == "/";
}
