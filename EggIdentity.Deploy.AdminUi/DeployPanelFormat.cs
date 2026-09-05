using System.Globalization;
using EggIdentity.Contract;

namespace EggIdentity.Deploy.AdminUi;

internal static class DeployPanelFormat {
    public static string Relative(DateTimeOffset? at, DateTimeOffset now) {
        if (at is not { } when) return "never";
        var age = now - when;
        if (age < TimeSpan.FromSeconds(5)) return "just now";
        if (age < TimeSpan.FromMinutes(1)) return $"{(int)age.TotalSeconds}s ago";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes}m ago";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    public static string ShortRevision(string? revision) {
        if (string.IsNullOrWhiteSpace(revision)) return "";
        var trimmed = revision.Trim();
        return trimmed.Length > 7 ? trimmed[..7] : trimmed;
    }

    public static string ShortDigest(string? digest) {
        if (string.IsNullOrWhiteSpace(digest)) return "";
        var trimmed = digest.Trim();
        var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
        var hex = colon >= 0 ? trimmed[(colon + 1)..] : trimmed;
        return hex.Length > 12 ? hex[..12] : hex;
    }

    public static string VersionLine(string? version, string? revision, string? digest) {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(version)) parts.Add(version.Trim());
        var rev = ShortRevision(revision);
        if (rev.Length > 0) parts.Add(rev);
        if (parts.Count == 0) {
            var dig = ShortDigest(digest);
            if (dig.Length > 0) parts.Add(dig);
        }
        return parts.Count == 0 ? "unknown" : string.Join(" ", parts);
    }

    public static string PhaseClass(DeployPhase phase) => "dp-phase-" + phase.ToString().ToLowerInvariant();

    public static string Timestamp(DateTimeOffset at) => at.ToString("u", CultureInfo.InvariantCulture);
}
