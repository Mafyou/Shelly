namespace ShellyBLE.Core.Tests.Buttons;

public class HttpActionRunnerTests
{
    readonly Mock<HttpMessageHandler> handler = new();
    readonly HttpActionRunner runner;

    public HttpActionRunnerTests()
    {
        runner = new(new HttpClient(handler.Object));
    }

    [Fact]
    public async Task Sends_a_get_request_and_reports_the_status()
    {
        SetupResponse(HttpStatusCode.OK);

        var result = await runner.RunAsync(new HttpRequestAction("http://192.168.1.50/rpc/Switch.Toggle?id=0"), StoppingToken);

        var success = result.ShouldBeSucceeded();
        success.StatusCode.ShouldBe(200);
        success.Target.ShouldBe("192.168.1.50/rpc/Switch.Toggle?id=0");
        result.Succeeded.ShouldBeTrue();
        result.Message.ShouldBe("200 ← 192.168.1.50/rpc/Switch.Toggle?id=0");
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.Host == "192.168.1.50" && r.Content == null),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Shelly_relay_sends_the_request_it_resolves_to()
    {
        SetupResponse(HttpStatusCode.OK);

        var result = await runner.RunAsync(new ShellyRelayAction("192.168.1.50", 1, ShellyRelayCommand.On), StoppingToken);

        result.ShouldBeSucceeded().Target.ShouldBe("192.168.1.50/rpc/Switch.Set?id=1&on=true");
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && $"{r.RequestUri}" == "http://192.168.1.50/rpc/Switch.Set?id=1&on=true"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Post_sends_the_body_with_its_content_type()
    {
        HttpMethod? method = null;
        string? body = null;
        string? mediaType = null;
        // Le message est libéré après l'envoi : on relève son contenu pendant l'appel.
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken stoppingToken) =>
            {
                method = request.Method;
                mediaType = request.Content!.Headers.ContentType!.MediaType;
                body = request.Content.ReadAsStringAsync(stoppingToken).Result;
                return new HttpResponseMessage(HttpStatusCode.Created);
            });

        var result = await runner.RunAsync(new HttpRequestAction("https://nas.local/api/hook", HttpVerb.Post, """{"porte":"ouverte"}""", "application/json"), StoppingToken);

        result.ShouldBeSucceeded().StatusCode.ShouldBe(201);
        method.ShouldBe(HttpMethod.Post);
        mediaType.ShouldBe("application/json");
        body.ShouldBe("""{"porte":"ouverte"}""");
    }

    [Fact]
    public async Task Open_link_is_handed_back_to_the_app_without_any_request()
    {
        var result = await runner.RunAsync(new OpenLinkAction("tel:+33639981234"), StoppingToken);

        result.ShouldBeLinkToOpen().Uri.ShouldBe(new Uri("tel:+33639981234"));
        result.Succeeded.ShouldBeTrue();
        result.Message.ShouldBe("Lien à ouvrir : tel:+33639981234");
        handler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_link_is_a_failure()
    {
        (await runner.RunAsync(new OpenLinkAction("pas un lien"), StoppingToken)).ShouldBeFailed().Reason.ShouldBe("Lien invalide : pas un lien");
    }

    [Fact]
    public async Task Http_error_is_a_failure()
    {
        SetupResponse(HttpStatusCode.Unauthorized);

        var result = await runner.RunAsync(new ShellyRelayAction("relay", 0, ShellyRelayCommand.Toggle, ShellyGeneration.Gen1), StoppingToken);

        result.Succeeded.ShouldBeFalse();
        result.ShouldBeFailed().Reason.ShouldBe("401 Unauthorized ← relay/relay/0?turn=toggle");
    }

    [Fact]
    public async Task Network_error_is_reported_instead_of_thrown()
    {
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Hôte injoignable"));

        var result = await runner.RunAsync(new HttpRequestAction("http://relay/"), StoppingToken);

        result.ShouldBeFailed().Reason.ShouldContain("Hôte injoignable");
        result.Message.ShouldContain("Hôte injoignable");
    }

    [Theory]
    [InlineData("pas une url")]
    [InlineData("ftp://nas/file")]
    public async Task Invalid_url_is_rejected_without_any_request(string url)
    {
        (await runner.RunAsync(new HttpRequestAction(url), StoppingToken)).ShouldBeFailed().Reason.ShouldContain(url);

        handler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public void Result_extensions_describe_every_case()
    {
        ActionResult success = new ActionSucceeded(204, "relay/toggle");
        ActionResult failure = new ActionFailed("Hôte injoignable");
        ActionResult link = new LinkToOpen(new Uri("https://example.com/page"));

        success.Succeeded.ShouldBeTrue();
        success.Message.ShouldBe("204 ← relay/toggle");
        failure.Succeeded.ShouldBeFalse();
        failure.Message.ShouldBe("Hôte injoignable");
        link.Succeeded.ShouldBeTrue();
        link.Message.ShouldBe("Lien à ouvrir : https://example.com/page");
    }

    void SetupResponse(HttpStatusCode status) =>
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(status));
}
