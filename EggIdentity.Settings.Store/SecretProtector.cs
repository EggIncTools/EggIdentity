using System.Security.Cryptography;
using System.Text;

namespace EggIdentity.Settings.Store;

public sealed class SecretProtector {
    private const string Prefix = "egis1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    private SecretProtector(byte[] key) => _key = key;

    public static SecretProtector? FromEnvironment() =>
        FromKey(Environment.GetEnvironmentVariable("EGGIDENTITY_SETTINGS_KEY"));

    public static SecretProtector? FromKey(string? base64Key) {
        if (string.IsNullOrWhiteSpace(base64Key)) return null;
        byte[] key;
        try { key = Convert.FromBase64String(base64Key); } catch (FormatException) { return null; }
        return key.Length == 32 ? new SecretProtector(key) : null;
    }

    public static bool IsProtected(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Protect(string plaintext) {
        ArgumentNullException.ThrowIfNull(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[bytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, bytes, cipher, tag);

        var packed = new byte[NonceSize + cipher.Length + TagSize];
        nonce.CopyTo(packed, 0);
        cipher.CopyTo(packed, NonceSize);
        tag.CopyTo(packed, NonceSize + cipher.Length);
        return Prefix + Convert.ToBase64String(packed);
    }

    public string? Unprotect(string? stored) {
        if (!IsProtected(stored)) return stored;

        byte[] packed;
        try { packed = Convert.FromBase64String(stored![Prefix.Length..]); } catch (FormatException) { return null; }
        if (packed.Length < NonceSize + TagSize) return null;

        var nonce = packed.AsSpan(0, NonceSize);
        var cipher = packed.AsSpan(NonceSize, packed.Length - NonceSize - TagSize);
        var tag = packed.AsSpan(packed.Length - TagSize, TagSize);
        var plain = new byte[cipher.Length];

        try {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
        } catch (CryptographicException) {
            return null;
        }
        return Encoding.UTF8.GetString(plain);
    }
}
