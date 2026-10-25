namespace ShellyBLE.Core.Tests.Gatt;

public class ShellyButtonClientTests
{
    readonly Mock<IGattChannel> channel = new(MockBehavior.Strict);
    readonly Mock<TimeProvider> time = new();
    readonly ShellyButtonClient client;

    public ShellyButtonClientTests()
    {
        client = new(channel.Object, time.Object);
    }

    [Fact]
    public async Task Reads_all_settings()
    {
        SetupRead(ShellyGatt.DeviceInformationService, ShellyGatt.FirmwareRevision, [.. "20240819-074337/v1.0.20@a0bb4b3b\0"u8]);
        SetupRead(ShellyGatt.BeaconMode, [1]);
        SetupRead(ShellyGatt.BuzzerEnabled, [0]);
        SetupRead(ShellyGatt.MulticlickSpeed, [2]);
        SetupRead(ShellyGatt.UnixTime, [0x20, 0xA1, 0xFB, 0x68]);

        var settings = await client.ReadSettingsAsync(StoppingToken);

        settings.ShouldBe(new ButtonDeviceSettings(
            new ShellyFirmware("20240819-074337/v1.0.20@a0bb4b3b", new Version(1, 0, 20), new DateOnly(2024, 8, 19)),
            BeaconMode: true, BuzzerEnabled: false, MulticlickSpeed.Ms300, DateTimeOffset.FromUnixTimeSeconds(0x68FBA120)));
    }

    /// <summary>Cas réel : BLU Button1 neuf livré en 1.0.2, sans heure ni vitesse multi-clic.</summary>
    [Fact]
    public async Task Old_firmware_reports_missing_settings_as_unsupported()
    {
        SetupRead(ShellyGatt.DeviceInformationService, ShellyGatt.FirmwareRevision, [.. "20230223-150544/v1.0.2@e85f66ff"u8]);
        SetupRead(ShellyGatt.BeaconMode, [0]);
        SetupRead(ShellyGatt.BuzzerEnabled, [1]);
        channel.Setup(c => c.ReadAsync(ShellyGatt.Service, ShellyGatt.MulticlickSpeed, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GattException("Caractéristique introuvable."));
        channel.Setup(c => c.ReadAsync(ShellyGatt.Service, ShellyGatt.UnixTime, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GattException("Caractéristique introuvable."));

        var settings = await client.ReadSettingsAsync(StoppingToken);

        var firmware = settings.Firmware.ShouldNotBeNull();
        firmware.Display.ShouldBe("1.0.2 (2023-02-23)");
        firmware.IsOutdated.ShouldBeTrue();
        settings.BeaconMode.ShouldBe(false);
        settings.BuzzerEnabled.ShouldBe(true);
        settings.MulticlickSpeed.ShouldBeNull();
        settings.DeviceTime.ShouldBeNull();
    }

    [Theory]
    [InlineData("20250818-101010/v1.0.23@0123abcd", "1.0.23 (2025-08-18)", false)]
    [InlineData("20240927-120000/v1.0.20-rc8@deadbeef", "1.0.20 (2024-09-27)", true)]
    [InlineData("format inconnu", "format inconnu", false)]
    public void Parses_firmware_revision(string raw, string display, bool outdated)
    {
        var firmware = ShellyFirmware.Parse(raw);

        firmware.Display.ShouldBe(display);
        firmware.IsOutdated.ShouldBe(outdated);
    }

    [Fact]
    public async Task Firmware_is_null_when_device_information_service_is_missing()
    {
        channel.Setup(c => c.ReadAsync(ShellyGatt.DeviceInformationService, ShellyGatt.FirmwareRevision, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GattException("absent"));

        (await client.GetFirmwareAsync(StoppingToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_unknown_multiclick_speed()
    {
        SetupRead(ShellyGatt.MulticlickSpeed, [9]);

        await Should.ThrowAsync<GattException>(() => client.GetMulticlickSpeedAsync(StoppingToken));
    }

    [Fact]
    public async Task Applies_only_the_settings_that_changed()
    {
        var current = new ButtonDeviceSettings(null, BeaconMode: false, BuzzerEnabled: true, MulticlickSpeed.Ms800, DateTimeOffset.UnixEpoch);
        SetupWrite(ShellyGatt.BeaconMode);
        SetupWrite(ShellyGatt.MulticlickSpeed);

        await client.ApplySettingsAsync(current, current with { BeaconMode = true, MulticlickSpeed = MulticlickSpeed.Ms150 }, StoppingToken);

        channel.Verify(c => c.WriteAsync(ShellyGatt.Service, ShellyGatt.BeaconMode, Bytes(1), true, It.IsAny<CancellationToken>()));
        channel.Verify(c => c.WriteAsync(ShellyGatt.Service, ShellyGatt.MulticlickSpeed, Bytes(3), true, It.IsAny<CancellationToken>()));
        channel.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Never_writes_a_setting_the_firmware_does_not_have()
    {
        var current = new ButtonDeviceSettings(null, BeaconMode: false, BuzzerEnabled: true, MulticlickSpeed: null, DeviceTime: null);

        await client.ApplySettingsAsync(current, current with { MulticlickSpeed = MulticlickSpeed.Ms150 }, StoppingToken);

        channel.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Enabling_encryption_writes_the_passkey_then_reads_the_new_key()
    {
        ImmutableArray<byte> key = [.. RandomNumberGenerator.GetBytes(16)];
        var sequence = new MockSequence();
        channel.InSequence(sequence)
            .Setup(c => c.WriteAsync(ShellyGatt.Service, ShellyGatt.Passkey, Bytes(0x40, 0xE2, 0x01, 0x00), true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        channel.InSequence(sequence)
            .Setup(c => c.ReadAsync(ShellyGatt.Service, ShellyGatt.EncryptionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        (await client.EnableEncryptionAsync(123_456, StoppingToken)).ShouldBeBytes(key.AsSpan());
    }

    [Fact]
    public async Task Refuses_out_of_range_passkey_without_touching_the_device()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => client.EnableEncryptionAsync(1_000_000, StoppingToken));

        channel.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Rejects_key_of_unexpected_length()
    {
        SetupRead(ShellyGatt.EncryptionKey, [.. new byte[8]]);

        await Should.ThrowAsync<GattException>(() => client.ReadEncryptionKeyAsync(StoppingToken));
    }

    [Fact]
    public async Task Reads_the_sample_BTHome_frame_as_is()
    {
        SetupRead(ShellyGatt.SampleBTHomeData, [0x44, 0x00, 0x01, 0x01, 0x64, 0x3A, 0x01]);

        var sample = await client.ReadSampleBTHomeDataAsync(StoppingToken);

        sample.ShouldBe([0x44, 0x00, 0x01, 0x01, 0x64, 0x3A, 0x01]);
    }

    [Fact]
    public async Task Syncs_device_clock_with_current_time()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_761_300_000);
        time.Setup(t => t.GetUtcNow()).Returns(now);
        SetupWrite(ShellyGatt.UnixTime);

        (await client.SyncTimeAsync(StoppingToken)).ShouldBe(now);

        channel.Verify(c => c.WriteAsync(ShellyGatt.Service, ShellyGatt.UnixTime, Bytes(0x20, 0x4E, 0xFB, 0x68), true, It.IsAny<CancellationToken>()));
    }

    /// <summary>Cas réel : après un redémarrage, l'horloge repart de 1970.</summary>
    [Fact]
    public async Task Resyncs_a_clock_reset_by_a_reboot()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_761_300_000);
        time.Setup(t => t.GetUtcNow()).Returns(now);
        SetupWrite(ShellyGatt.UnixTime);

        var synced = await client.SyncTimeIfDriftedAsync(SettingsWithClock(DateTimeOffset.FromUnixTimeSeconds(10)), StoppingToken);

        synced.ShouldBe(now);
        channel.Verify(c => c.WriteAsync(ShellyGatt.Service, ShellyGatt.UnixTime, Bytes(0x20, 0x4E, 0xFB, 0x68), true, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(-30)]
    public async Task Leaves_a_clock_within_tolerance_untouched(int driftSeconds)
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_761_300_000);
        time.Setup(t => t.GetUtcNow()).Returns(now);

        var synced = await client.SyncTimeIfDriftedAsync(SettingsWithClock(now.AddSeconds(driftSeconds)), StoppingToken);

        synced.ShouldBeNull();
    }

    [Fact]
    public async Task Never_writes_a_clock_the_firmware_does_not_have()
    {
        var synced = await client.SyncTimeIfDriftedAsync(SettingsWithClock(null), StoppingToken);

        synced.ShouldBeNull();
    }

    static ButtonDeviceSettings SettingsWithClock(DateTimeOffset? clock) => new(null, true, true, null, clock);

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Rings_and_stops_the_buzzer(bool active, byte expected)
    {
        SetupWrite(ShellyGatt.ActivateBuzzer);

        await client.SetBuzzerActiveAsync(active, StoppingToken);

        channel.Verify(c => c.WriteAsync(ShellyGatt.Service, ShellyGatt.ActivateBuzzer, Bytes(expected), true, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Factory_reset_writes_without_response_because_the_device_reboots()
    {
        channel.Setup(c => c.WriteAsync(ShellyGatt.Service, ShellyGatt.FactoryReset, Bytes(1), false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.FactoryResetAsync(StoppingToken);

        channel.VerifyAll();
    }

    [Fact]
    public async Task Disposing_the_client_closes_the_channel()
    {
        channel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        await client.DisposeAsync();

        channel.Verify(c => c.DisposeAsync(), Times.Once);
    }

    void SetupRead(Guid characteristic, ImmutableArray<byte> value) => SetupRead(ShellyGatt.Service, characteristic, value);

    void SetupRead(Guid service, Guid characteristic, ImmutableArray<byte> value) =>
        channel.Setup(c => c.ReadAsync(service, characteristic, It.IsAny<CancellationToken>())).ReturnsAsync(value);

    void SetupWrite(Guid characteristic) =>
        channel.Setup(c => c.WriteAsync(ShellyGatt.Service, characteristic, It.IsAny<ImmutableArray<byte>>(), true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
}
