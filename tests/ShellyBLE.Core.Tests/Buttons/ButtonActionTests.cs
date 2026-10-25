namespace ShellyBLE.Core.Tests.Buttons;

public class ButtonActionTests
{
    [Theory]
    [InlineData(ShellyGeneration.Gen2, ShellyRelayCommand.Toggle, "http://192.168.1.50/rpc/Switch.Toggle?id=1")]
    [InlineData(ShellyGeneration.Gen2, ShellyRelayCommand.On, "http://192.168.1.50/rpc/Switch.Set?id=1&on=true")]
    [InlineData(ShellyGeneration.Gen2, ShellyRelayCommand.Off, "http://192.168.1.50/rpc/Switch.Set?id=1&on=false")]
    [InlineData(ShellyGeneration.Gen1, ShellyRelayCommand.Toggle, "http://192.168.1.50/relay/1?turn=toggle")]
    [InlineData(ShellyGeneration.Gen1, ShellyRelayCommand.On, "http://192.168.1.50/relay/1?turn=on")]
    [InlineData(ShellyGeneration.Gen1, ShellyRelayCommand.Off, "http://192.168.1.50/relay/1?turn=off")]
    public void Shelly_relay_resolves_to_a_get_request(ShellyGeneration generation, ShellyRelayCommand command, string expected)
    {
        ButtonAction action = new ShellyRelayAction("192.168.1.50", 1, command, generation);

        action.Request.ShouldBe(new HttpRequestAction(expected, HttpVerb.Get));
        action.Preview.ShouldBe($"GET {expected}");
    }

    [Fact]
    public void Shelly_relay_defaults_to_gen2()
    {
        new ShellyRelayAction("relais", 0, ShellyRelayCommand.Toggle).Generation.ShouldBe(ShellyGeneration.Gen2);
    }

    [Theory]
    [InlineData(ShellyRelayCommand.Toggle, "Basculer le relais 0 de 192.168.1.50")]
    [InlineData(ShellyRelayCommand.On, "Allumer le relais 0 de 192.168.1.50")]
    [InlineData(ShellyRelayCommand.Off, "Éteindre le relais 0 de 192.168.1.50")]
    public void Shelly_relay_summary_names_the_command(ShellyRelayCommand command, string expected)
    {
        ButtonAction action = new ShellyRelayAction("192.168.1.50", 0, command, ShellyGeneration.Gen1);

        action.Summary.ShouldBe(expected);
    }

    [Fact]
    public void Get_request_is_summarised_and_previewed_on_one_line()
    {
        ButtonAction action = new HttpRequestAction("http://nas.local/hook?event=press");

        action.Summary.ShouldBe("GET http://nas.local/hook?event=press");
        action.Preview.ShouldBe("GET http://nas.local/hook?event=press");
        action.Request.ShouldBe(new HttpRequestAction("http://nas.local/hook?event=press"));
    }

    [Fact]
    public void Post_preview_shows_content_type_and_body()
    {
        ButtonAction action = new HttpRequestAction("http://nas.local/hook", HttpVerb.Post, """{"porte":"ouverte"}""");

        action.Summary.ShouldBe("POST http://nas.local/hook");
        action.Preview.ShouldBe("POST http://nas.local/hook\nContent-Type: application/json\n\n{\"porte\":\"ouverte\"}");
    }

    [Fact]
    public void Post_without_body_previews_only_the_request_line()
    {
        ButtonAction action = new HttpRequestAction("http://nas.local/hook", HttpVerb.Post);

        action.Preview.ShouldBe("POST http://nas.local/hook");
    }

    [Fact]
    public void Open_link_has_no_request()
    {
        ButtonAction action = new OpenLinkAction("https://example.com/porte");

        action.Summary.ShouldBe("Ouvrir https://example.com/porte");
        action.Preview.ShouldBe("Ouvrir : https://example.com/porte");
        action.Request.ShouldBeNull();
    }
}
