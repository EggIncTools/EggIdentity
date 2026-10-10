using System.Security.Cryptography;

namespace EggIdentity.Auth;

public static class OAuthCrypto {
    public static string GenerateEncryptionKey() => RandomHex(32);

    public static string RandomHex(int n) {
        var b = RandomNumberGenerator.GetBytes(n);
        return Convert.ToHexStringLower(b);
    }
}
