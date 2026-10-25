namespace ShellyBLE.Core.Tests.Buttons;

public class ButtonMonitorTests
{
    static readonly MacAddress Mac = MacAddress.Parse("3C:2E:F5:11:22:33");
    static readonly Guid AndroidId = Guid.Parse("00000000-0000-0000-0000-3c2ef5112233");
    static readonly Guid IosId = Guid.Parse("8d3f6a52-0c1e-4b7a-9f21-5e4c3b2a1908");
    static readonly ImmutableArray<byte> Key = [.. Convert.FromHexString("00112233445566778899aabbccddeeff")];
    static readonly ShellyRelayAction Relay = new("192.168.1.50", 0, ShellyRelayCommand.Toggle);

    readonly Mock<IButtonSettingsStore> settings = new();
    readonly Mock<IActionRunner> actions = new();
    readonly ButtonMonitor monitor;

    public ButtonMonitorTests()
    {
        monitor = new(settings.Object, actions.Object);
    }

    [Fact]
    public void Ignores_devices_that_are_neither_shelly_buttons_nor_saved()
    {
        var advertisement = Advertisement(name: "Mi Band 8", bthome: null);

        monitor.Process(AndroidId, advertisement).ShouldBeNull();
    }

    /// <summary>
    /// Annonce du jeu « OTA » qu'un BLU Button1 en 1.0.2 émet en parallèle de ses annonces normales
    /// (structure capturée sur un vrai appareil, adresses remplacées par des adresses fictives).
    /// </summary>
    [Fact]
    public void Ignores_the_parallel_ota_advertisement_set()
    {
        var advertisement = ShellyAdvertisement.From(
        [
            new(0x01, [0x06]),
            new(AdRecord.CompleteLocalName, [.. "OTA"u8]),
            new(0x1B, [.. Convert.FromHexString("00D4E5F6071829")]),
            new(0x0A, [0x00]),
            new(AdRecord.ManufacturerSpecific, [.. Convert.FromHexString("A90B0B01000A332211CCBBAA")]),
        ]);

        advertisement.IsOta.ShouldBeTrue();
        advertisement.Manufacturer.ShouldNotBeNull().Mac.ShouldBe(MacAddress.Parse("AA:BB:CC:11:22:33"));
        monitor.Process(Guid.Parse("00000000-0000-0000-0000-d4e5f6071829"), advertisement).ShouldBeNull();
    }

    [Fact]
    public void Ignores_other_shelly_models()
    {
        var advertisement = Advertisement(name: "SBHT-003C", bthome: [0x44, 0x00, 0x01, 0x01, 0x50]);

        monitor.Process(AndroidId, advertisement).ShouldBeNull();
    }

    [Fact]
    public void Reports_press_with_battery_mac_and_configured_action()
    {
        Save(new ButtonSettings { Id = $"{AndroidId}", Actions = new Dictionary<ButtonEvent, ButtonAction> { [ButtonEvent.DoublePress] = Relay } });

        var observation = monitor.Process(AndroidId, Advertisement(bthome: Press(packetId: 1, ButtonEvent.DoublePress))).ShouldNotBeNull();

        observation.Model.ShouldNotBeNull().Name.ShouldBe("Shelly BLU Button1");
        observation.Mac.ShouldBe(Mac);
        var reading = observation.Reading.ShouldBeReading();
        reading.IsEncrypted.ShouldBeFalse();
        reading.Battery.ShouldBe((byte)87);
        reading.Presses.ShouldBeSequence(new ButtonPress(0, ButtonEvent.DoublePress, "Double appui", Relay));
    }

    [Fact]
    public void Reports_each_press_once_although_it_is_advertised_several_times()
    {
        var advertisement = Advertisement(bthome: Press(packetId: 9, ButtonEvent.Press));

        monitor.Process(AndroidId, advertisement)!.Reading.ShouldBeReading().Presses.Length.ShouldBe(1);
        monitor.Process(AndroidId, advertisement)!.Reading.ShouldBeReading().Presses.ShouldBeEmpty();
        monitor.Process(AndroidId, Advertisement(bthome: Press(packetId: 10, ButtonEvent.Press)))!.Reading.ShouldBeReading().Presses.Length.ShouldBe(1);
    }

    [Fact]
    public void Encrypted_press_without_saved_key_reports_key_required()
    {
        var payload = EncryptedPayload(Key, Mac, [0x00, 0x01, 0x3A, 0x01], counter: 1);

        var observation = monitor.Process(AndroidId, Advertisement(bthome: payload)).ShouldNotBeNull();

        observation.Reading.ShouldBeError().Kind.ShouldBe(BTHomeErrorKind.KeyRequired);
    }

    [Fact]
    public void Decrypts_with_saved_key_and_mac_derived_from_device_id()
    {
        Save(new ButtonSettings { Id = $"{AndroidId}", Key = Convert.ToHexString(Key.AsSpan()) });
        var payload = EncryptedPayload(Key, Mac, [0x00, 0x01, 0x01, 0x40, 0x3A, 0x04], counter: 42);

        var reading = monitor.Process(AndroidId, Advertisement(bthome: payload))!.Reading.ShouldBeReading();

        reading.IsEncrypted.ShouldBeTrue();
        reading.Battery.ShouldBe((byte)0x40);
        reading.Presses.Single().Event.ShouldBe(ButtonEvent.LongPress);
    }

    [Fact]
    public void On_ios_shows_and_decrypts_with_the_mac_learnt_from_manufacturer_data()
    {
        Save(new ButtonSettings { Id = $"{IosId}", Key = Convert.ToHexString(Key.AsSpan()) });
        // Données constructeur seules (l'identifiant iOS est opaque), puis un appui chiffré sans elles.
        monitor.Process(IosId, Advertisement(manufacturerMac: Mac, bthome: null)).ShouldNotBeNull().Mac.ShouldBe(Mac);

        var payload = EncryptedPayload(Key, Mac, [0x00, 0x02, 0x3A, 0x01], counter: 7);
        var reading = monitor.Process(IosId, Advertisement(name: null, bthome: payload))!.Reading.ShouldBeReading();

        reading.Presses.Single().Event.ShouldBe(ButtonEvent.Press);
    }

    [Fact]
    public void Wrong_key_reports_decryption_failure()
    {
        Save(new ButtonSettings { Id = $"{AndroidId}", Key = new string('a', 32) });
        var payload = EncryptedPayload(Key, Mac, [0x00, 0x01, 0x3A, 0x01], counter: 1);

        monitor.Process(AndroidId, Advertisement(bthome: payload))!.Reading.ShouldBeError().Kind.ShouldBe(BTHomeErrorKind.DecryptionFailed);
    }

    [Fact]
    public void Recognises_saved_device_from_a_bare_bthome_advertisement()
    {
        Save(new ButtonSettings { Id = $"{IosId}" });

        monitor.Process(IosId, Advertisement(name: null, bthome: [0x40, 0x00, 0x01, 0x01, 0x10])).ShouldNotBeNull();
    }

    [Fact]
    public void Reports_pairing_mode_only_when_manufacturer_flags_are_present()
    {
        monitor.Process(AndroidId, Advertisement(manufacturerMac: Mac, pairing: true, bthome: null))!.IsInPairingMode.ShouldBe(true);
        monitor.Process(AndroidId, Advertisement(manufacturerMac: Mac, pairing: false, bthome: null))!.IsInPairingMode.ShouldBe(false);
        monitor.Process(AndroidId, Advertisement(bthome: Press(packetId: 1, ButtonEvent.Press)))!.IsInPairingMode.ShouldBeNull();
    }

    [Fact]
    public void Labels_presses_of_multi_button_devices_and_only_maps_actions_of_the_first_button()
    {
        Save(new ButtonSettings { Id = $"{AndroidId}", Actions = new Dictionary<ButtonEvent, ButtonAction> { [ButtonEvent.Press] = new HttpRequestAction("http://x/1") } });
        ImmutableArray<byte> payload = [0x44, 0x00, 0x03, 0x3A, 0x01, 0x3A, 0x01, 0x3A, 0x00, 0x3A, 0x00];

        var presses = monitor.Process(AndroidId, Advertisement(name: "SBBT-004CEU", bthome: payload))!.Reading.ShouldBeReading().Presses;

        presses.ShouldBeSequence(
        [
            new ButtonPress(0, ButtonEvent.Press, "Bouton 1 : Appui simple", new HttpRequestAction("http://x/1")),
            new ButtonPress(1, ButtonEvent.Press, "Bouton 2 : Appui simple", null),
        ]);
    }

    [Fact]
    public async Task Runs_the_action_of_each_press_that_has_one()
    {
        actions.Setup(a => a.RunAsync(It.IsAny<ButtonAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ButtonAction action, CancellationToken _) => new ActionSucceeded(200, action.Summary));
        var reading = new ButtonReading(false, null, null,
        [
            new ButtonPress(0, ButtonEvent.Press, "Appui simple", new HttpRequestAction("http://a")),
            new ButtonPress(1, ButtonEvent.Press, "Bouton 2", null),
            new ButtonPress(0, ButtonEvent.LongPress, "Appui long", Relay),
        ]);

        var results = await monitor.RunActionsAsync(reading, StoppingToken);

        results.Select(r => r.ShouldBeSucceeded().Target).ShouldBe(["GET http://a", "Basculer le relais 0 de 192.168.1.50"]);
        actions.Verify(a => a.RunAsync(It.IsAny<ButtonAction>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    void Save(ButtonSettings saved) => settings.Setup(s => s.Find(saved.Id)).Returns(saved);

    static ImmutableArray<byte> Press(byte packetId, ButtonEvent buttonEvent) => [0x44, 0x00, packetId, 0x01, 87, 0x3A, (byte)buttonEvent];

    static ShellyAdvertisement Advertisement(string? name = "SBBT-002C", ImmutableArray<byte>? bthome = null, MacAddress? manufacturerMac = null, bool pairing = false)
    {
        List<AdRecord> records = [];
        if (name is not null)
            records.Add(new(AdRecord.CompleteLocalName, [.. Encoding.UTF8.GetBytes(name)]));
        if (manufacturerMac is { } mac)
        {
            // Little-endian, comme dans les trames réelles.
            var macBytes = new byte[MacAddress.Length];
            mac.Reversed().CopyTo(macBytes);
            byte flags = (byte)(ShellyFeatures.Discoverable | (pairing ? ShellyFeatures.PairingMode : 0));
            records.Add(new(AdRecord.ManufacturerSpecific, [0xA9, 0x0B, 0x01, flags, 0x00, 0x0A, .. macBytes, 0x0B, 0x01, 0x00]));
        }
        if (bthome is { } payload)
            records.Add(new(AdRecord.ServiceData16, [0xD2, 0xFC, .. payload]));
        return ShellyAdvertisement.From(records);
    }
}
