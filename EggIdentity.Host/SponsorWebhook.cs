using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EggIdentity.Host;

public sealed record SponsorshipWebhookEvent(string Action, string SponsorSubject);

public static class SponsorWebhook {
    public static bool VerifySignature(string secret, byte[] body, string? signatureHeader) {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.Ordinal))
            return false;

        var expectedHex = signatureHeader["sha256=".Length..];
        var computedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        var computedHex = Convert.ToHexStringLower(computedHash);

        var expectedBytes = Encoding.ASCII.GetBytes(expectedHex);
        var computedBytes = Encoding.ASCII.GetBytes(computedHex);
        if (expectedBytes.Length != computedBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(expectedBytes, computedBytes);
    }

    public static SponsorshipWebhookEvent? ParsePayload(string json) {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var action = root.TryGetProperty("action", out var actionEl) ? actionEl.GetString() : null;
        if (string.IsNullOrEmpty(action)) return null;

        if (!root.TryGetProperty("sponsorship", out var sponsorship)) return null;
        if (!sponsorship.TryGetProperty("sponsor", out var sponsor)) return null;
        if (!sponsor.TryGetProperty("id", out var idEl)) return null;

        var subject = idEl.ValueKind switch {
            JsonValueKind.Number => idEl.GetRawText(),
            JsonValueKind.String => idEl.GetString(),
            _ => null,
        };
        if (string.IsNullOrEmpty(subject)) return null;

        return new SponsorshipWebhookEvent(action, subject);
    }

    public static bool? ResolveIsSponsor(string action) => action switch {
        "created" => true,
        "cancelled" => false,
        _ => null,
    };
}
