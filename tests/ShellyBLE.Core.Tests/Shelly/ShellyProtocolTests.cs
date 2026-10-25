namespace ShellyBLE.Core.Tests.Shelly;

public class ShellyProtocolTests
{
    [Fact]
    public void Parses_manufacturer_data_blocks()
    {
        // A9 0B = Allterco ; flags = découvrable + appairage ; MAC ; modèle 0x0001.
        byte[] data = [0xA9, 0x0B, 0x01, 0x11, 0x00, 0x0A, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x0B, 0x01, 0x00];

        var parsed = ShellyManufacturerData.TryParse(data).ShouldNotBeNull();

        parsed.Features.ShouldBe(ShellyFeatures.Discoverable | ShellyFeatures.PairingMode);
        parsed.Model.ShouldNotBeNull().Name.ShouldBe("Shelly BLU Button1");
        // MAC en little-endian dans la trame.
        parsed.Mac.ShouldBe(MacAddress.Parse("06:05:04:03:02:01"));
    }

    /// <summary>
    /// Annonce d'un BLU Button1 en mode appairage, structure capturée sur un vrai appareil sous Android (logcat).
    /// L'adresse MAC a été remplacée par une adresse fictive.
    /// </summary>
    [Fact]
    public void Decodes_a_real_button1_pairing_beacon()
    {
        var advertisement = ShellyAdvertisement.From(
        [
            new(0x01, [0x06]),
            new(AdRecord.ServiceData16, [.. Convert.FromHexString("D2FC40000101003A00")]),
            new(AdRecord.ShortenedLocalName, [.. Convert.FromHexString("534242542D30303243")]),
            new(AdRecord.ManufacturerSpecific, [.. Convert.FromHexString("A90B0119000B01000A332211CCBBAA")]),
        ]);

        advertisement.LocalName.ShouldBe("SBBT-002C");
        var manufacturer = advertisement.Manufacturer.ShouldNotBeNull();
        manufacturer.Model.ShouldNotBeNull().Name.ShouldBe("Shelly BLU Button1");
        manufacturer.Mac.ShouldBe(MacAddress.Parse("AA:BB:CC:11:22:33"));
        manufacturer.Features.ShouldBe(ShellyFeatures.Discoverable | ShellyFeatures.BuzzerEnabled | ShellyFeatures.PairingMode);

        var packet = BTHomeParser.Parse(advertisement.BTHomePayload.ShouldNotBeNull().AsSpan()).ShouldBePacket();
        packet.Info.IsEncrypted.ShouldBeFalse();
        packet.Info.IsTriggerBased.ShouldBeFalse();
        packet.PacketId.ShouldBe((byte)1);
        packet.Battery.ShouldBe((byte)0);
        packet.Buttons.ShouldBe([new ButtonPressed(0, ButtonEvent.None)]);
    }

    /// <summary>
    /// Trame du même bouton après activation du chiffrement (PIN 1234), capturée sur un vrai appareil puis anonymisée :
    /// le clair réel (déchiffré avec la clé relue en GATT) a été rechiffré avec une clé et une MAC fictives, même compteur.
    /// Valide le nonce (MAC + UUID + octet d'info + compteur) et l'AES-CCM de bout en bout.
    /// </summary>
    [Fact]
    public void Decrypts_a_real_encrypted_button1_beacon()
    {
        ImmutableArray<byte> key = [.. Convert.FromHexString("a919f7d936b3d79a527c2917428f1918")];
        var payload = Convert.FromHexString("4108D2E4D6330400000000C780FAEB");

        var packet = BTHomeParser.Parse(payload, new(key, [MacAddress.Parse("AA:BB:CC:11:22:33")])).ShouldBePacket();

        packet.Info.IsEncrypted.ShouldBeTrue();
        packet.Counter.ShouldBe(0u);
        packet.IsComplete.ShouldBeTrue();
        packet.Buttons.ShouldBe([new ButtonPressed(0, ButtonEvent.None)]);
    }

    [Fact]
    public void Ignores_other_manufacturers()
    {
        ShellyManufacturerData.TryParse([0x4C, 0x00, 0x02, 0x15]).ShouldBeNull();
    }

    [Theory]
    [InlineData(new byte[] { 0xD2, 0xFC, 0x44, 0x00, 0x01 })]
    [InlineData(new byte[] { 0xFC, 0xD2, 0x44, 0x00, 0x01 })]
    public void Extracts_bthome_service_data_in_either_byte_order(byte[] serviceData)
    {
        var advertisement = ShellyAdvertisement.From(
        [
            new(AdRecord.CompleteLocalName, [.. "SBBT-002C"u8]),
            new(AdRecord.ServiceData16, [.. serviceData]),
        ]);

        advertisement.IsShelly.ShouldBeTrue();
        advertisement.Model.ShouldNotBeNull().IsButton.ShouldBeTrue();
        advertisement.BTHomePayload.ShouldBeBytes(0x44, 0x00, 0x01);
    }

    [Fact]
    public void Mac_round_trips_through_text_and_device_id()
    {
        var mac = MacAddress.Parse("aa-bb-cc-dd-ee-ff");

        $"{mac}".ShouldBe("AA:BB:CC:DD:EE:FF");
        MacAddress.FromDeviceId(Guid.Parse("00000000-0000-0000-0000-aabbccddeeff")).ShouldBe(mac);
        MacAddress.FromDeviceId(Guid.Parse("6f1c2c0e-91a4-4c55-9a52-0c7d5b0b5e11")).ShouldBeNull();
        MacAddress.TryParse("AA:BB", out _).ShouldBeFalse();
    }

    [Fact]
    public void Encodes_gatt_values_little_endian()
    {
        ShellyGatt.EncodePasskey(999_999).ShouldBeBytes(0x3F, 0x42, 0x0F, 0x00);
        Should.Throw<ArgumentOutOfRangeException>(() => ShellyGatt.EncodePasskey(1_000_000));

        var time = DateTimeOffset.FromUnixTimeSeconds(1_761_300_000);
        ShellyGatt.DecodeUnixTime(ShellyGatt.EncodeUnixTime(time).AsSpan()).ShouldBe(time);
        MulticlickSpeed.Ms300.Milliseconds.ShouldBe(300);
    }
}
