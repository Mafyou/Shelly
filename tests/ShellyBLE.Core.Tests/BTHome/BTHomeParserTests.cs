namespace ShellyBLE.Core.Tests.BTHome;

public class BTHomeParserTests
{
    // Exemple officiel : https://bthome.io/encryption/
    static readonly ImmutableArray<byte> SpecKey = [.. Convert.FromHexString("231d39c1d7cc1ab1aee224cd096db932")];
    static readonly MacAddress SpecMac = MacAddress.Parse("54:48:E6:8F:80:A5");
    static readonly ImmutableArray<byte> SpecServiceData = [.. Convert.FromHexString("d2fc41e445f3c9962b332211006c7c4519")];

    /// <summary>Données de service sans l'UUID D2 FC.</summary>
    static ReadOnlySpan<byte> SpecPayload => SpecServiceData.AsSpan()[2..];

    [Fact]
    public void Cipher_matches_bthome_specification_vector()
    {
        var nonce = BTHomeCipher.BuildNonce(SpecMac, 0x41, Convert.FromHexString("33221100"));
        Convert.ToHexStringLower(nonce.AsSpan()).ShouldBe("5448e68f80a5d2fc4133221100");

        var ciphertext = new byte[6];
        var mic = new byte[4];
        BTHomeCipher.Encrypt(SpecKey.AsSpan(), nonce.AsSpan(), Convert.FromHexString("02CA0903BF13"), ciphertext, mic);

        Convert.ToHexStringLower(ciphertext).ShouldBe("e445f3c9962b");
        Convert.ToHexStringLower(mic).ShouldBe("6c7c4519");
    }

    [Fact]
    public void Cipher_agrees_with_platform_AesCcm_on_multi_block_payloads()
    {
        Assert.SkipUnless(AesCcm.IsSupported, "AesCcm indisponible sur cette plateforme.");

        var key = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(13);
        var plaintext = RandomNumberGenerator.GetBytes(37);
        var expected = new byte[plaintext.Length];
        var expectedTag = new byte[4];
        using (var ccm = new AesCcm(key))
            ccm.Encrypt(nonce, plaintext, expected, expectedTag);

        var actual = new byte[plaintext.Length];
        var tag = new byte[4];
        BTHomeCipher.Encrypt(key, nonce, plaintext, actual, tag);

        actual.ShouldBe(expected);
        tag.ShouldBe(expectedTag);
    }

    [Fact]
    public void Decrypts_specification_service_data()
    {
        var packet = BTHomeParser.Parse(SpecPayload, new(SpecKey, [SpecMac])).ShouldBePacket();

        packet.Info.IsEncrypted.ShouldBeTrue();
        packet.Counter.ShouldBe(0x00112233u);
        packet.IsComplete.ShouldBeTrue();
        packet.Objects.Length.ShouldBe(2);
        // 0x02 CA09 : 2506 × 0,01 ; 0x03 BF13 : 5055 × 0,01.
        packet.Objects[0].ShouldBeMeasurement().ShouldBe(new Measurement(0x02, "temperature", 25.06, "°C"));
        packet.Objects[1].ShouldBeMeasurement().ShouldBe(new Measurement(0x03, "humidity", 50.55, "%"));
    }

    [Fact]
    public void Tries_each_mac_candidate_until_the_mic_verifies()
    {
        BTHomeParser.Parse(SpecPayload, new(SpecKey, [SpecMac.Reversed(), SpecMac])).ShouldBePacket();
    }

    [Fact]
    public void Rejects_tampered_payload()
    {
        byte[] tampered = [.. SpecPayload];
        tampered[2] ^= 0x01;

        BTHomeParser.Parse(tampered, new(SpecKey, [SpecMac])).ShouldBeError().Kind.ShouldBe(BTHomeErrorKind.DecryptionFailed);
    }

    [Fact]
    public void Encrypted_payload_without_key_requires_key()
    {
        BTHomeParser.Parse(SpecPayload).ShouldBeError().Kind.ShouldBe(BTHomeErrorKind.KeyRequired);
    }

    [Theory]
    [InlineData(0x01, ButtonEvent.Press)]
    [InlineData(0x02, ButtonEvent.DoublePress)]
    [InlineData(0x03, ButtonEvent.TriplePress)]
    [InlineData(0x04, ButtonEvent.LongPress)]
    [InlineData(0x80, ButtonEvent.Hold)]
    [InlineData(0xFE, ButtonEvent.Hold)]
    public void Parses_unencrypted_button_advertisement(byte raw, ButtonEvent expected)
    {
        // Info v2 sur événement, paquet 0x2A, batterie 100 %, bouton.
        byte[] payload = [0x44, 0x00, 0x2A, 0x01, 0x64, 0x3A, raw];

        var packet = BTHomeParser.Parse(payload).ShouldBePacket();

        packet.Info.IsTriggerBased.ShouldBeTrue();
        packet.Counter.ShouldBeNull();
        packet.PacketId.ShouldBe((byte)0x2A);
        packet.Battery.ShouldBe((byte)100);
        packet.Buttons.ShouldBe([new ButtonPressed(0, expected)]);
    }

    [Fact]
    public void Numbers_buttons_of_multi_button_devices()
    {
        byte[] payload = [0x44, 0x00, 0x01, 0x3A, 0x00, 0x3A, 0x02, 0x3A, 0x00, 0x3A, 0x00];

        var packet = BTHomeParser.Parse(payload).ShouldBePacket();

        packet.Buttons.Count().ShouldBe(4);
        packet.Buttons.ShouldContain(new ButtonPressed(1, ButtonEvent.DoublePress));
    }

    [Fact]
    public void Parses_firmware_version()
    {
        byte[] payload = [0x40, 0x00, 0x05, 0xF0, 0x01, 0x00, 0xF1, 0x14, 0x00, 0x01, 0x01];

        BTHomeParser.Parse(payload).ShouldBePacket().Firmware.ShouldBe(new Version(1, 1, 0, 20));
    }

    [Fact]
    public void Stops_at_unknown_object_and_keeps_what_was_read()
    {
        byte[] payload = [0x40, 0x00, 0x07, 0xEE, 0x01, 0x01, 0x50];

        var packet = BTHomeParser.Parse(payload).ShouldBePacket();

        packet.IsComplete.ShouldBeFalse();
        packet.PacketId.ShouldBe((byte)7);
        packet.Battery.ShouldBeNull();
    }

    // Exemples du tableau des capteurs de https://bthome.io/format/ (sans 0x01, 0x50, 0x53 et 0x54, décodés à part).
    [Theory]
    [InlineData("518756", "acceleration", 22.151, "m/s²")]
    [InlineData("630057D0FF", "acceleration", -3.123456, "m/s²")]
    [InlineData("6001", "channel", 1d, null)]
    [InlineData("12E204", "co2", 1250d, "ppm")]
    [InlineData("56E803", "conductivity", 1000d, "µS/cm")]
    [InlineData("0960", "count", 96d, null)]
    [InlineData("3D0960", "count", 24585d, null)]
    [InlineData("3E2A2C0960", "count", 1611213866d, null)]
    [InlineData("59EA", "count", -22d, null)]
    [InlineData("5AEAEA", "count", -5398d, null)]
    [InlineData("5BEA0234EA", "count", -365690134d, null)]
    [InlineData("434E34", "current", 13.39, "A")]
    [InlineData("5D02EA", "current", -5.63, "A")]
    [InlineData("08CA06", "dewpoint", 17.38, "°C")]
    [InlineData("5E9F8C", "direction", 359.99, "°")]
    [InlineData("400C00", "distance", 12d, "mm")]
    [InlineData("414E00", "distance", 7.8, "m")]
    [InlineData("424E3400", "duration", 13.39, "s")]
    [InlineData("4D12138A14", "energy", 344593.17, "kWh")]
    [InlineData("0A138A14", "energy", 1346.067, "kWh")]
    [InlineData("4B138A14", "gas", 1346.067, "m3")]
    [InlineData("4C41018A01", "gas", 25821.505, "m3")]
    [InlineData("528756", "gyroscope", 22.151, "°/s")]
    [InlineData("03BF13", "humidity", 50.55, "%")]
    [InlineData("2E23", "humidity", 35d, "%")]
    [InlineData("05138A14", "illuminance", 13460.67, "lx")]
    [InlineData("6402", "light level", 2d, null)]
    [InlineData("065E1F", "mass", 80.3, "kg")]
    [InlineData("073E1D", "mass", 74.86, "lb")]
    [InlineData("14020C", "moisture", 30.74, "%")]
    [InlineData("2F23", "moisture", 35d, "%")]
    [InlineData("0D120C", "pm2.5", 3090d, "ug/m3")]
    [InlineData("0E021C", "pm10", 7170d, "ug/m3")]
    [InlineData("0B021B00", "power", 69.14, "W")]
    [InlineData("5C02FBFFFF", "power", -12.78, "W")]
    [InlineData("5FA00F", "precipitation", 400d, "mm")]
    [InlineData("04138A01", "pressure", 1008.83, "hPa")]
    [InlineData("3F020C", "rotation", 307.4, "°")]
    [InlineData("61AC0D", "rotational speed", 3500d, "rpm")]
    [InlineData("6503", "settings revision", 3d, null)]
    [InlineData("444E34", "speed", 133.9, "m/s")]
    [InlineData("624099DFFF", "speed", -2.123456, "m/s")]
    [InlineData("57EA", "temperature", -22d, "°C")]
    [InlineData("58EA", "temperature", -7.7, "°C")]
    [InlineData("451101", "temperature", 27.3, "°C")]
    [InlineData("02CA09", "temperature", 25.06, "°C")]
    [InlineData("0218FC", "temperature", -10d, "°C")]
    [InlineData("133301", "tvoc", 307d, "ug/m3")]
    [InlineData("0C020C", "voltage", 3.074, "V")]
    [InlineData("4A020C", "voltage", 307.4, "V")]
    [InlineData("4E87562A01", "volume", 19551.879, "L")]
    [InlineData("478756", "volume", 2215.1, "L")]
    [InlineData("48DC87", "volume", 34780d, "mL")]
    [InlineData("5587562A01", "volume storage", 19551.879, "L")]
    [InlineData("49DC87", "volume flow rate", 34.78, "m3/hr")]
    [InlineData("4632", "UV index", 5d, null)]
    [InlineData("4F87562A01", "water", 19551.879, "L")]
    public void Decodes_specification_sensor_examples(string hex, string name, double value, string? unit)
    {
        var packet = ParseObjects(hex);

        packet.IsComplete.ShouldBeTrue();
        var measurement = packet.Objects.ShouldHaveSingleItem().ShouldBeMeasurement();
        measurement.ShouldBe(new Measurement(Convert.FromHexString(hex)[0], name, value, unit));
    }

    [Fact]
    public void Decodes_little_endian_uint24()
    {
        // 0x04 pression : 0x0186A0 = 100000 → 1000 hPa.
        ParseObjects("04A08601").Measurements.ShouldHaveSingleItem().Value.ShouldBe(1000d);
    }

    [Fact]
    public void Keeps_every_measurement_of_the_same_type_in_order()
    {
        var packet = ParseObjects("02CA09" + "0218FC" + "03BF13");

        packet.Measurements.Select(m => (m.Name, m.Value)).ShouldBe([("temperature", 25.06), ("temperature", -10d), ("humidity", 50.55)]);
    }

    [Theory]
    [InlineData("1A01", 0x1A, "door", true, "Ouverte")]
    [InlineData("2100", 0x21, "motion", false, "Non détecté")]
    [InlineData("1501", 0x15, "battery", true, "Faible")]
    public void Decodes_binary_sensors(string hex, byte id, string name, bool value, string state)
    {
        var sensor = ParseObjects(hex).Objects.ShouldHaveSingleItem().ShouldBeBinarySensor();

        sensor.ShouldBe(new BinarySensor(id, name, value));
        sensor.State.ShouldBe(state);
    }

    [Fact]
    public void Decodes_command_without_arguments()
    {
        var command = ParseObjects("3B0001").Objects.ShouldHaveSingleItem().ShouldBeCommand();

        command.Opcode.ShouldBe(BTHomeCommand.On);
        command.Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void Decodes_command_arguments_and_continues_after_them()
    {
        // Pas de 5 vers le haut, puis variateur tourné à gauche de 3 pas.
        var packet = ParseObjects("3B010305" + "3C0103");

        packet.IsComplete.ShouldBeTrue();
        var command = packet.Objects[0].ShouldBeCommand();
        command.Opcode.ShouldBe(BTHomeCommand.StepUp);
        command.Arguments.ShouldBeBytes(0x05);
        (packet.Objects[1] is DimmerRotated dimmer ? dimmer : null).ShouldBe(new DimmerRotated(0, DimmerDirection.Left, 3));
    }

    [Fact]
    public void Truncated_command_arguments_stop_parsing()
    {
        var packet = ParseObjects("0009" + "3B0203FF");

        packet.IsComplete.ShouldBeFalse();
        packet.PacketId.ShouldBe((byte)9);
        packet.Objects.Length.ShouldBe(1);
    }

    [Fact]
    public void Numbers_dimmer_events()
    {
        var packet = ParseObjects("3C0000" + "3C020A");

        packet.Objects.Select(o => o is DimmerRotated d ? d : null)
            .ShouldBe([new DimmerRotated(0, DimmerDirection.None, 0), new DimmerRotated(1, DimmerDirection.Right, 10)]);
    }

    [Fact]
    public void Decodes_utf8_text_and_raw_data()
    {
        var packet = ParseObjects("530C48656C6C6F20576F726C6421" + "540C48656C6C6F20576F726C6421");

        packet.IsComplete.ShouldBeTrue();
        (packet.Objects[0] is TextValue text ? text : null).ShouldBe(new TextValue("Hello World!"));
        packet.Objects[1].ShouldBeOther(0x54).Data.ShouldBeBytes("Hello World!"u8);
    }

    [Fact]
    public void Decodes_timestamp_as_utc_date()
    {
        var timestamp = ParseObjects("505D396164").Objects.ShouldHaveSingleItem() is Timestamp t ? t : null;

        timestamp.ShouldBe(new Timestamp(new DateTimeOffset(2023, 5, 14, 19, 41, 17, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData("F100010204", "4.2.1.0")]
    [InlineData("F2000106", "6.1.0")]
    public void Decodes_specification_firmware_examples(string hex, string version)
    {
        ParseObjects(hex).Firmware.ShouldBe(Version.Parse(version));
    }

    [Fact]
    public void Stops_at_an_id_missing_from_the_specification()
    {
        // 0x30 n'est pas défini : sa taille est inconnue, l'humidité qui suit est perdue.
        var packet = ParseObjects("02CA09" + "3001" + "03BF13");

        packet.IsComplete.ShouldBeFalse();
        packet.Measurements.ShouldHaveSingleItem().Name.ShouldBe("temperature");
    }

    [Fact]
    public void Rejects_bthome_v1()
    {
        BTHomeParser.Parse([0x20, 0x00, 0x01]).ShouldBeError().Kind.ShouldBe(BTHomeErrorKind.UnsupportedVersion);
    }

    [Fact]
    public void Deduplicates_repeated_advertisements()
    {
        var deduplicator = new PacketDeduplicator();
        var first = Packet(packetId: 5);

        deduplicator.IsNew("a", first).ShouldBeTrue();
        deduplicator.IsNew("a", first).ShouldBeFalse();
        deduplicator.IsNew("b", first).ShouldBeTrue();
        deduplicator.IsNew("a", Packet(packetId: 6)).ShouldBeTrue();
    }

    /// <summary>Objets en hexadécimal, précédés d'un octet d'info v2 non chiffré.</summary>
    static BTHomePacket ParseObjects(string hex) => BTHomeParser.Parse([0x40, .. Convert.FromHexString(hex)]).ShouldBePacket();

    static BTHomePacket Packet(byte packetId) =>
        new(new(0x44), null, [new PacketId(packetId), new ButtonPressed(0, ButtonEvent.Press)], IsComplete: true);
}
