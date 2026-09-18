using System.Security.Cryptography;
using System.Text;

namespace EggIdentity.Host;

public static class AvatarResolver {
    private const string DataPrefix = "data:";
    private const string ImagePrefix = "image/";

    public static bool TryParseDataUri(string value, out byte[] bytes, out string mediaType) {
        bytes = [];
        mediaType = "";
        if (!value.StartsWith(DataPrefix, StringComparison.Ordinal)) return false;
        var comma = value.IndexOf(',');
        if (comma < 0) return false;

        var parts = value[DataPrefix.Length..comma].Split(';');
        if (parts.Length < 2 || !parts[^1].Equals("base64", StringComparison.OrdinalIgnoreCase)) return false;
        var type = parts[0].Trim();
        if (type.Length <= ImagePrefix.Length || !type.StartsWith(ImagePrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var payload = value[(comma + 1)..];
        if (payload.Length == 0) return false;
        var buffer = new byte[(payload.Length / 4 + 1) * 3];
        if (!Convert.TryFromBase64String(payload, buffer, out var written) || written == 0) return false;

        bytes = buffer[..written];
        mediaType = type.ToLowerInvariant();
        return true;
    }

    public static string ComputeETag(string stored) =>
        $"\"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stored)))}\"";

    public static string ComputeETag(string path, long length, DateTime lastWriteUtc) =>
        ComputeETag($"{path}|{length}|{lastWriteUtc.Ticks}");

    public static bool IfNoneMatchHits(string? header, string etag) {
        if (string.IsNullOrEmpty(header)) return false;
        return header.Split(',')
            .Select(raw => raw.Trim())
            .Select(tag => tag.StartsWith("W/", StringComparison.Ordinal) ? tag[2..] : tag)
            .Any(tag => tag == "*" || tag == etag);
    }
}
