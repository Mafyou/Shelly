namespace ShellyBLE.Core.Tests.Buttons;

public class ButtonSettingsTests : IDisposable
{
    readonly string directory = Directory.CreateTempSubdirectory("shellyble-tests-").FullName;

    string FilePath => Path.Combine(directory, "buttons.json");

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Valid_form_is_normalised()
    {
        var form = new ButtonSettingsForm("id", "  Entrée  ", "3c-2e-f5-11-22-33", "00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF",
            new Dictionary<ButtonEvent, ButtonAction?>
            {
                [ButtonEvent.Press] = new ShellyRelayAction(" 192.168.1.50 ", 0, ShellyRelayCommand.Toggle),
                [ButtonEvent.DoublePress] = new HttpRequestAction(" http://192.168.1.60/rpc/Switch.Set?id=1&on=true ", Body: "  "),
                [ButtonEvent.TriplePress] = new OpenLinkAction(" tel:+33639981234 "),
                [ButtonEvent.LongPress] = null,
            });

        var settings = form.Validate().ShouldBeSettings();

        settings.Name.ShouldBe("Entrée");
        settings.Mac.ShouldBe("3C:2E:F5:11:22:33");
        settings.Key.ShouldBe("00112233445566778899aabbccddeeff");
        settings.KeyBytes.ShouldNotBeNull().Length.ShouldBe(16);
        settings.Actions.ShouldBe(new Dictionary<ButtonEvent, ButtonAction>
        {
            [ButtonEvent.Press] = new ShellyRelayAction("192.168.1.50", 0, ShellyRelayCommand.Toggle),
            [ButtonEvent.DoublePress] = new HttpRequestAction("http://192.168.1.60/rpc/Switch.Set?id=1&on=true"),
            [ButtonEvent.TriplePress] = new OpenLinkAction("tel:+33639981234"),
        }, ignoreOrder: true);
    }

    [Fact]
    public void Invalid_form_lists_every_problem()
    {
        var form = new ButtonSettingsForm("id", null, "pas une mac", "abcd",
            new Dictionary<ButtonEvent, ButtonAction?> { [ButtonEvent.DoublePress] = new HttpRequestAction("ftp://nas/x") });

        form.Validate().ShouldBeErrors().Messages.ShouldBeSequence(
            "Adresse MAC invalide (format AA:BB:CC:DD:EE:FF).",
            "La clé de chiffrement doit compter 32 caractères hexadécimaux.",
            "Double appui : URL http(s) invalide.");
    }

    [Theory]
    [InlineData("", 0, "Appui simple : Adresse du relais manquante.")]
    [InlineData("http://192.168.1.50", 0, "Appui simple : Adresse du relais invalide (http://192.168.1.50) : nom d'hôte ou IP attendu, éventuellement suivi de :port.")]
    [InlineData("192.168.1.50/rpc", 0, "Appui simple : Adresse du relais invalide (192.168.1.50/rpc) : nom d'hôte ou IP attendu, éventuellement suivi de :port.")]
    [InlineData("relais salon", 0, "Appui simple : Adresse du relais invalide (relais salon) : nom d'hôte ou IP attendu, éventuellement suivi de :port.")]
    [InlineData("192.168.1.50:port", 0, "Appui simple : Adresse du relais invalide (192.168.1.50:port) : nom d'hôte ou IP attendu, éventuellement suivi de :port.")]
    [InlineData("192.168.1.50", -1, "Appui simple : Le numéro de relais doit être compris entre 0 et 15.")]
    [InlineData("192.168.1.50", 16, "Appui simple : Le numéro de relais doit être compris entre 0 et 15.")]
    public void Invalid_relay_is_reported_with_the_press_label(string host, int channel, string expected)
    {
        Validate(ButtonEvent.Press, new ShellyRelayAction(host, channel, ShellyRelayCommand.On)).ShouldBeSequence(expected);
    }

    [Theory]
    [InlineData("192.168.1.50")]
    [InlineData("192.168.1.50:8080")]
    [InlineData("shellyplus1-a8032ab12345.local")]
    [InlineData("[fe80::1]:80")]
    public void Relay_accepts_host_names_and_ips_with_optional_port(string host)
    {
        Validate(ButtonEvent.Press, new ShellyRelayAction(host, ShellyRelayAction.MaxChannel, ShellyRelayCommand.Off, ShellyGeneration.Gen1)).ShouldBeEmpty();
    }

    [Fact]
    public void Http_request_body_requires_post_and_a_valid_content_type()
    {
        Validate(ButtonEvent.LongPress, new HttpRequestAction("http://nas/hook", HttpVerb.Get, "{}"))
            .ShouldBeSequence("Appui long : Un corps de requête n'est possible qu'en POST.");
        Validate(ButtonEvent.LongPress, new HttpRequestAction("https://nas/hook", HttpVerb.Post, "{}", "pas un type"))
            .ShouldBeSequence("Appui long : Type de contenu invalide (pas un type).");
        Validate(ButtonEvent.LongPress, new HttpRequestAction("https://nas/hook", HttpVerb.Post, "a=1", "application/x-www-form-urlencoded"))
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("tel:+33639981234")]
    [InlineData("sms:+33639981234?body=Bonjour")]
    [InlineData("geo:48.8584,2.2945")]
    [InlineData("mailto:moi@example.com")]
    [InlineData("monappli://ouvrir/porte")]
    public void Open_link_accepts_any_absolute_uri(string url)
    {
        Validate(ButtonEvent.TriplePress, new OpenLinkAction(url)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.com/page")]
    [InlineData("/data/fichier.txt")]
    public void Open_link_rejects_relative_addresses_and_files(string url)
    {
        Validate(ButtonEvent.TriplePress, new OpenLinkAction(url))
            .ShouldBeSequence("Triple appui : Lien invalide : adresse complète attendue (https://…, tel:…, geo:…, sms:…).");
    }

    [Fact]
    public async Task Json_store_round_trips_settings()
    {
        var store = new JsonButtonSettingsStore(FilePath);
        await store.SaveAsync(new ButtonSettings { Id = "a", Name = "Salon", Actions = new Dictionary<ButtonEvent, ButtonAction> { [ButtonEvent.TriplePress] = new HttpRequestAction("http://x") } }, StoppingToken);
        await store.SaveAsync(new ButtonSettings { Id = "b" }, StoppingToken);
        await store.RemoveAsync("b", StoppingToken);

        var reloaded = new JsonButtonSettingsStore(FilePath);

        reloaded.All.Count().ShouldBe(1);
        var a = reloaded.Find("a").ShouldNotBeNull();
        a.Name.ShouldBe("Salon");
        a.Actions[ButtonEvent.TriplePress].ShouldBe(new HttpRequestAction("http://x"));
        File.ReadAllText(FilePath).ShouldContain("\"TriplePress\"");
    }

    [Fact]
    public async Task Json_store_round_trips_each_kind_of_action()
    {
        var actions = new Dictionary<ButtonEvent, ButtonAction>
        {
            [ButtonEvent.Press] = new ShellyRelayAction("192.168.1.50", 2, ShellyRelayCommand.Off, ShellyGeneration.Gen1),
            [ButtonEvent.DoublePress] = new HttpRequestAction("https://nas/hook", HttpVerb.Post, """{"porte":"ouverte"}""", "application/json"),
            [ButtonEvent.TriplePress] = new OpenLinkAction("geo:48.8584,2.2945"),
        };
        await new JsonButtonSettingsStore(FilePath).SaveAsync(new ButtonSettings { Id = "a", Actions = actions }, StoppingToken);

        var reloaded = new JsonButtonSettingsStore(FilePath).Find("a").ShouldNotBeNull();

        reloaded.Actions.ShouldBe(actions, ignoreOrder: true);
        var json = File.ReadAllText(FilePath);
        json.ShouldContain("\"kind\": \"shellyRelay\"");
        json.ShouldContain("\"generation\": \"Gen1\"");
        json.ShouldContain("\"kind\": \"http\"");
        json.ShouldContain("\"verb\": \"Post\"");
        json.ShouldContain("\"kind\": \"openLink\"");
    }

    [Fact]
    public void Json_store_reads_actions_saved_as_plain_urls_as_get_requests()
    {
        // Format des versions précédentes : une URL appelée en GET par type d'appui.
        File.WriteAllText(FilePath, """
            [
              {
                "Id": "a",
                "Name": "Entrée",
                "Actions": {
                  "Press": "http://192.168.1.50/rpc/Switch.Toggle?id=0",
                  "LongPress": "http://192.168.1.51/relay/0?turn=off"
                }
              }
            ]
            """);

        var settings = new JsonButtonSettingsStore(FilePath).Find("a").ShouldNotBeNull();

        settings.Name.ShouldBe("Entrée");
        settings.Actions[ButtonEvent.Press].ShouldBe(new HttpRequestAction("http://192.168.1.50/rpc/Switch.Toggle?id=0"));
        settings.Actions[ButtonEvent.LongPress].ShouldBe(new HttpRequestAction("http://192.168.1.51/relay/0?turn=off", HttpVerb.Get));
    }

    [Fact]
    public void Json_store_fills_in_omitted_optional_fields()
    {
        File.WriteAllText(FilePath, """
            [{ "Id": "a", "Actions": {
              "Press": { "kind": "shellyRelay", "host": "relais", "channel": 1 },
              "DoublePress": { "kind": "http", "url": "http://x" } } }]
            """);

        var settings = new JsonButtonSettingsStore(FilePath).Find("a").ShouldNotBeNull();

        settings.Actions[ButtonEvent.Press].ShouldBe(new ShellyRelayAction("relais", 1, ShellyRelayCommand.Toggle, ShellyGeneration.Gen2));
        settings.Actions[ButtonEvent.DoublePress].ShouldBe(new HttpRequestAction("http://x", HttpVerb.Get, null, "application/json"));
    }

    [Fact]
    public void Json_store_starts_empty_when_file_is_corrupt()
    {
        File.WriteAllText(FilePath, "{ pas du json");

        new JsonButtonSettingsStore(FilePath).All.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abcd")]
    [InlineData("zz112233445566778899aabbccddeeff")]
    public void Key_bytes_are_null_when_key_is_missing_or_invalid(string? key)
    {
        new ButtonSettings { Id = "id", Key = key }.KeyBytes.ShouldBeNull();
    }

    [Fact]
    public void Key_bytes_decode_a_valid_key()
    {
        new ButtonSettings { Id = "id", Key = "00112233445566778899AABBCCDDEEFF" }.KeyBytes
            .ShouldBeBytes(0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF);
    }

    /// <summary>Messages d'erreur du formulaire pour une seule action (vide si elle est valide).</summary>
    static ImmutableArray<string> Validate(ButtonEvent buttonEvent, ButtonAction action) =>
        new ButtonSettingsForm("id", null, null, null, new Dictionary<ButtonEvent, ButtonAction?> { [buttonEvent] = action }).Validate() switch
        {
            SettingsErrors errors => errors.Messages,
            ButtonSettings => [],
        };
}
