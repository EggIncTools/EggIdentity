using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.Auth;

public static class BearerSecret {
    private const string Prefix = "Bearer ";

    public static string? TryExtract(string? header) {
        if (string.IsNullOrEmpty(header)) return null;
        if (!header.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var token = header[Prefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    public static bool Matches(string? configured, string? presented) {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(presented)) return false;
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        return CryptographicOperations.FixedTimeEquals(configuredHash, presentedHash);
    }

    public static bool MatchesHeader(HttpRequest request, string? configured) {
        ArgumentNullException.ThrowIfNull(request);
        return Matches(configured, TryExtract(request.Headers.Authorization.ToString()));
    }
}
