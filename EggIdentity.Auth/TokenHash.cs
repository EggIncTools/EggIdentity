using System.Security.Cryptography;
using System.Text;

namespace EggIdentity.Auth;

public static class TokenHash {
    public const int TokenBytes = 32;

    public static string Mint() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenBytes));

    public static string Of(string token) {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public static bool Matches(string presented, string storedHash) {
        ArgumentNullException.ThrowIfNull(storedHash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Of(presented)), Encoding.UTF8.GetBytes(storedHash));
    }
}
