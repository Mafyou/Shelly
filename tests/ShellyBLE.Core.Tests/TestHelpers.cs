namespace ShellyBLE.Core.Tests;

public static class TestHelpers
{
    public static CancellationToken StoppingToken => TestContext.Current.CancellationToken;

    public static BTHomePacket ShouldBePacket(this BTHomeResult result) =>
        result is BTHomePacket packet ? packet : throw new ShouldAssertException($"Paquet attendu, obtenu : {result}");

    public static BTHomeError ShouldBeError(this BTHomeResult result) =>
        result is BTHomeError error ? error : throw new ShouldAssertException($"Erreur attendue, obtenu : {result}");

    public static OtherObject ShouldBeOther(this BTHomeObject obj, byte id) =>
        obj is OtherObject other && other.Id == id ? other : throw new ShouldAssertException($"Objet 0x{id:X2} attendu, obtenu : {obj}");

    public static Measurement ShouldBeMeasurement(this BTHomeObject obj) =>
        obj is Measurement measurement ? measurement : throw new ShouldAssertException($"Mesure attendue, obtenu : {obj}");

    public static BinarySensor ShouldBeBinarySensor(this BTHomeObject obj) =>
        obj is BinarySensor sensor ? sensor : throw new ShouldAssertException($"Capteur binaire attendu, obtenu : {obj}");

    public static Command ShouldBeCommand(this BTHomeObject obj) =>
        obj is Command command ? command : throw new ShouldAssertException($"Commande attendue, obtenu : {obj}");

    public static ButtonReading ShouldBeReading(this BTHomeReading? reading) =>
        reading is ButtonReading r ? r : throw new ShouldAssertException($"Lecture attendue, obtenu : {reading}");

    public static BTHomeError ShouldBeError(this BTHomeReading? reading) =>
        reading is BTHomeError e ? e : throw new ShouldAssertException($"Erreur attendue, obtenu : {reading}");

    public static ButtonSettings ShouldBeSettings(this SettingsValidation validation) =>
        validation is ButtonSettings s ? s : throw new ShouldAssertException($"Réglages attendus, obtenu : {validation}");

    public static SettingsErrors ShouldBeErrors(this SettingsValidation validation) =>
        validation is SettingsErrors e ? e : throw new ShouldAssertException($"Erreurs attendues, obtenu : {validation}");

    public static ActionSucceeded ShouldBeSucceeded(this ActionResult result) =>
        result is ActionSucceeded success ? success : throw new ShouldAssertException($"Succès attendu, obtenu : {result.Message}");

    public static ActionFailed ShouldBeFailed(this ActionResult result) =>
        result is ActionFailed failure ? failure : throw new ShouldAssertException($"Échec attendu, obtenu : {result.Message}");

    public static LinkToOpen ShouldBeLinkToOpen(this ActionResult result) =>
        result is LinkToOpen link ? link : throw new ShouldAssertException($"Lien à ouvrir attendu, obtenu : {result.Message}");

    /// <summary>
    /// <see cref="ImmutableArray{T}"/> implémente <see cref="IEquatable{T}"/> en comparant le tableau sous-jacent :
    /// Shouldly s'en servirait au lieu de comparer les éléments. On compare donc explicitement en séquence.
    /// </summary>
    public static void ShouldBeSequence<T>(this ImmutableArray<T> actual, params ReadOnlySpan<T> expected) =>
        actual.AsEnumerable().ShouldBe(expected.ToArray());

    /// <summary>Compare des octets en hexadécimal, ce qui rend les messages d'échec lisibles.</summary>
    public static void ShouldBeBytes(this ImmutableArray<byte> actual, params ReadOnlySpan<byte> expected) =>
        Convert.ToHexString(actual.AsSpan()).ShouldBe(Convert.ToHexString(expected));

    public static void ShouldBeBytes(this ImmutableArray<byte>? actual, params ReadOnlySpan<byte> expected) =>
        actual.ShouldNotBeNull().ShouldBeBytes(expected);

    /// <summary>Matcher Moq : l'égalité d'<see cref="ImmutableArray{T}"/> porte sur la référence du tableau, pas sur son contenu.</summary>
    public static ImmutableArray<byte> Bytes(params byte[] expected) =>
        Match.Create<ImmutableArray<byte>>(value => value.AsSpan().SequenceEqual(expected));

    public static ImmutableArray<byte> Bytes(ImmutableArray<byte> expected) =>
        Match.Create<ImmutableArray<byte>>(value => value.AsSpan().SequenceEqual(expected.AsSpan()));

    /// <summary>Données de service BTHome chiffrées (sans l'UUID), comme les émet un bouton dont le chiffrement est actif.</summary>
    public static ImmutableArray<byte> EncryptedPayload(ImmutableArray<byte> key, MacAddress mac, ReadOnlySpan<byte> plaintext, uint counter, byte deviceInfo = 0x45)
    {
        Span<byte> counterBytes = stackalloc byte[BTHomeCipher.CounterLength];
        BinaryPrimitives.WriteUInt32LittleEndian(counterBytes, counter);

        var ciphertext = new byte[plaintext.Length];
        Span<byte> mic = stackalloc byte[BTHomeCipher.MicLength];
        BTHomeCipher.Encrypt(key.AsSpan(), BTHomeCipher.BuildNonce(mac, deviceInfo, counterBytes).AsSpan(), plaintext, ciphertext, mic);
        return [deviceInfo, .. ciphertext, .. counterBytes, .. mic];
    }
}
