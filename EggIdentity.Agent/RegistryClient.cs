using System.Net.Http.Headers;
using System.Text.Json;

namespace EggIdentity.Agent;

public interface IImageRegistry {
    Task<string> GetDigestAsync(ImageRef image, CancellationToken ct);
}

public sealed class RegistryClient(HttpClient http) : IImageRegistry {
    private static readonly string[] ManifestMediaTypes = [
        "application/vnd.oci.image.index.v1+json",
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.docker.distribution.manifest.v2+json",
    ];

    public async Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(image);

        var token = await FetchTokenAsync(image, ct);

        using var request = new HttpRequestMessage(HttpMethod.Head, image.ManifestUri);
        foreach (var mediaType in ManifestMediaTypes)
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"registry HEAD {image} returned {(int)response.StatusCode}", null, response.StatusCode);

        if (!response.Headers.TryGetValues("Docker-Content-Digest", out var values))
            throw new HttpRequestException($"registry HEAD {image} returned no Docker-Content-Digest header");

        var digest = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(digest))
            throw new HttpRequestException($"registry HEAD {image} returned an empty Docker-Content-Digest header");
        return digest;
    }

    private async Task<string?> FetchTokenAsync(ImageRef image, CancellationToken ct) {
        using var response = await http.GetAsync(image.TokenUri, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"registry token for {image} returned {(int)response.StatusCode}", null, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String)
            return token.GetString();
        if (doc.RootElement.TryGetProperty("access_token", out var accessToken) && accessToken.ValueKind == JsonValueKind.String)
            return accessToken.GetString();
        return null;
    }
}
