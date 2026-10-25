namespace ShellyBLE.Core.Buttons;

/// <summary>
/// System.Text.Json ne sait pas (dé)sérialiser une union : chaque action devient un objet
/// discriminé par « kind » (shellyRelay, http, openLink). Les anciens fichiers stockaient
/// une simple URL, relue comme une requête GET.
/// </summary>
internal sealed class ButtonActionJsonConverter : JsonConverter<ButtonAction>
{
    public override ButtonAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Ancien format : « "Press": "http://…" ».
        if (reader.TokenType is JsonTokenType.String)
            return new HttpRequestAction(reader.GetString() ?? "");

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new JsonException($"Action attendue, obtenu : {reader.TokenType}");

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        return Text(root, "kind") switch
        {
            "shellyRelay" => new ShellyRelayAction(
                Required(root, "host"),
                root.TryGetProperty("channel", out var channel) && channel.TryGetInt32(out var number) ? number : 0,
                EnumValue(root, "command", ShellyRelayCommand.Toggle),
                EnumValue(root, "generation", ShellyGeneration.Gen2)),
            "http" => new HttpRequestAction(
                Required(root, "url"),
                EnumValue(root, "verb", HttpVerb.Get),
                Text(root, "body"),
                Text(root, "contentType") ?? HttpRequestAction.DefaultContentType),
            "openLink" => new OpenLinkAction(Required(root, "url")),
            var kind => throw new JsonException($"Type d'action inconnu : {kind}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, ButtonAction value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case ShellyRelayAction relay:
                writer.WriteString("kind", "shellyRelay");
                writer.WriteString("host", relay.Host);
                writer.WriteNumber("channel", relay.Channel);
                writer.WriteString("command", $"{relay.Command}");
                writer.WriteString("generation", $"{relay.Generation}");
                break;

            case HttpRequestAction http:
                writer.WriteString("kind", "http");
                writer.WriteString("url", http.Url);
                writer.WriteString("verb", $"{http.Verb}");
                if (http.Body is { } body)
                    writer.WriteString("body", body);
                writer.WriteString("contentType", http.ContentType);
                break;

            case OpenLinkAction link:
                writer.WriteString("kind", "openLink");
                writer.WriteString("url", link.Url);
                break;
        }
        writer.WriteEndObject();
    }

    static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.String ? property.GetString() : null;

    static string Required(JsonElement element, string name) =>
        Text(element, name) ?? throw new JsonException($"Propriété « {name} » manquante.");

    /// <summary>Valeur absente : celle par défaut ; valeur inconnue : fichier invalide.</summary>
    static T EnumValue<T>(JsonElement element, string name, T fallback) where T : struct, Enum =>
        Text(element, name) switch
        {
            null => fallback,
            var text when Enum.TryParse<T>(text, ignoreCase: true, out var value) && Enum.IsDefined(value) => value,
            var text => throw new JsonException($"Valeur inconnue pour « {name} » : {text}"),
        };
}
