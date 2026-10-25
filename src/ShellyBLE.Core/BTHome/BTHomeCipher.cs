namespace ShellyBLE.Core.BTHome;

/// <summary>
/// AES-CCM tel qu'utilisé par BTHome v2 : nonce de 13 octets, MIC de 4 octets, sans données associées.
/// Implémenté au-dessus d'AES-ECB car <see cref="AesCcm"/> n'existe pas sur iOS ni sur Mac Catalyst.
/// </summary>
public static class BTHomeCipher
{
    public const int KeyLength = 16;
    public const int NonceLength = 13;
    public const int CounterLength = 4;
    public const int MicLength = 4;

    // Taille du champ longueur dans CCM : 15 - taille du nonce.
    const int L = 15 - NonceLength;
    const int Block = 16;

    /// <summary>MAC (ordre d'affichage) + UUID 0xFCD2 en little-endian + octet d'info + compteur tel que transmis.</summary>
    public static ImmutableArray<byte> BuildNonce(MacAddress mac, byte deviceInfo, ReadOnlySpan<byte> counter)
    {
        var nonce = new byte[NonceLength];
        mac.CopyTo(nonce);
        BinaryPrimitives.WriteUInt16LittleEndian(nonce.AsSpan(6), BTHomeParser.ServiceUuid);
        nonce[8] = deviceInfo;
        counter[..CounterLength].CopyTo(nonce.AsSpan(9));
        return ImmutableCollectionsMarshal.AsImmutableArray(nonce);
    }

    public static void Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> mic)
    {
        using var aes = CreateAes(key);
        Span<byte> tag = stackalloc byte[MicLength];
        ComputeTag(aes, nonce, plaintext, tag);
        ApplyKeystream(aes, nonce, plaintext, ciphertext);
        tag.CopyTo(mic);
    }

    public static bool TryDecrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> mic, Span<byte> plaintext)
    {
        using var aes = CreateAes(key);
        ApplyKeystream(aes, nonce, ciphertext, plaintext);

        Span<byte> expected = stackalloc byte[MicLength];
        ComputeTag(aes, nonce, plaintext[..ciphertext.Length], expected);
        if (CryptographicOperations.FixedTimeEquals(expected, mic))
            return true;

        plaintext.Clear();
        return false;
    }

    static Aes CreateAes(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeyLength)
            throw new ArgumentException($"La clé BTHome fait {KeyLength} octets.", nameof(key));

        var aes = Aes.Create();
        aes.Key = key.ToArray();
        return aes;
    }

    /// <summary>Chiffrement CTR à partir du bloc compteur 1 (le bloc 0 sert à masquer le MIC).</summary>
    static void ApplyKeystream(Aes aes, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> input, Span<byte> output)
    {
        Span<byte> counter = stackalloc byte[Block];
        Span<byte> keystream = stackalloc byte[Block];
        InitCounter(counter, nonce);

        for (int offset = 0, index = 1; offset < input.Length; offset += Block, index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(counter[^L..], (ushort)index);
            aes.EncryptEcb(counter, keystream, PaddingMode.None);

            var count = Math.Min(Block, input.Length - offset);
            for (var i = 0; i < count; i++)
                output[offset + i] = (byte)(input[offset + i] ^ keystream[i]);
        }
    }

    /// <summary>CBC-MAC sur B0 puis sur le clair, masqué par E(A0).</summary>
    static void ComputeTag(Aes aes, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, Span<byte> tag)
    {
        Span<byte> block = stackalloc byte[Block];
        Span<byte> mac = stackalloc byte[Block];

        block[0] = (byte)((((MicLength - 2) / 2) << 3) | (L - 1));
        nonce.CopyTo(block[1..]);
        BinaryPrimitives.WriteUInt16BigEndian(block[^L..], (ushort)plaintext.Length);
        aes.EncryptEcb(block, mac, PaddingMode.None);

        for (var offset = 0; offset < plaintext.Length; offset += Block)
        {
            block.Clear();
            plaintext.Slice(offset, Math.Min(Block, plaintext.Length - offset)).CopyTo(block);
            for (var i = 0; i < Block; i++)
                block[i] ^= mac[i];
            aes.EncryptEcb(block, mac, PaddingMode.None);
        }

        InitCounter(block, nonce);
        Span<byte> s0 = stackalloc byte[Block];
        aes.EncryptEcb(block, s0, PaddingMode.None);
        for (var i = 0; i < MicLength; i++)
            tag[i] = (byte)(mac[i] ^ s0[i]);
    }

    static void InitCounter(Span<byte> counter, ReadOnlySpan<byte> nonce)
    {
        counter.Clear();
        counter[0] = L - 1;
        nonce.CopyTo(counter[1..]);
    }
}
