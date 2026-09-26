using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using EggIdentity.Resilience;

namespace EggIdentity.Fleet;

public sealed class DockerEngineClient(HttpClient http, string prefix, TimeSpan callTimeout, Func<TimeSpan> pullTimeout) : IDockerEngine {
    private readonly string _prefix = prefix.EndsWith('/') ? prefix : prefix + "/";

    public Task<ContainerInfo?> InspectContainerAsync(string name, CancellationToken ct) =>
        Deadline.RunAsync($"inspect container {name}", async token => {
            using var container = await GetJsonAsync($"containers/{Uri.EscapeDataString(name)}/json", token);
            if (container is null) return null;
            var imageId = container.RootElement.TryGetProperty("Image", out var img) ? img.GetString() ?? "" : "";
            using var image = imageId.Length == 0 ? null : await GetJsonAsync($"images/{Uri.EscapeDataString(imageId)}/json", token);
            return DockerJson.ParseContainer(container.RootElement, image?.RootElement);
        }, callTimeout, ct: ct);

    public Task<ImageInfo?> InspectImageAsync(string reference, CancellationToken ct) =>
        Deadline.RunAsync($"inspect image {reference}", async token => {
            using var image = await GetJsonAsync($"images/{Uri.EscapeDataString(reference)}/json", token);
            return image is null ? null : DockerJson.ParseImage(image.RootElement);
        }, callTimeout, ct: ct);

    public Task PullImageAsync(string reference, IProgress<string>? progress, CancellationToken ct) =>
        Deadline.RunAsync($"pull {reference}", async token => {
            var image = ImageRef.Parse(reference);
            var path = $"images/create?fromImage={Uri.EscapeDataString(image.Name)}&tag={Uri.EscapeDataString(image.Tag)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, Relative(path));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode) throw await FailureAsync("pull", response, token);

            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (await reader.ReadLineAsync(token) is { } line) {
                var parsed = DockerJson.ParsePullProgress(line);
                if (parsed is null) continue;
                if (parsed.Error is not null) throw new InvalidOperationException($"pull {reference}: {parsed.Error}");
                progress?.Report(parsed.Format());
            }
        }, pullTimeout(), ct: ct);

    public Task RestartAsync(string name, CancellationToken ct) =>
        Deadline.RunAsync($"restart {name}", token => PostAsync($"containers/{Uri.EscapeDataString(name)}/restart?t=30", token), callTimeout + TimeSpan.FromSeconds(30), ct: ct);

    public Task<string> LogsTailAsync(string name, int lines, CancellationToken ct) =>
        Deadline.RunAsync($"logs {name}", async token => {
            var tail = lines.ToString(CultureInfo.InvariantCulture);
            using var response = await http.GetAsync(Relative($"containers/{Uri.EscapeDataString(name)}/logs?stdout=1&stderr=1&tail={tail}"), token);
            if (!response.IsSuccessStatusCode) throw await FailureAsync("logs", response, token);
            var bytes = await response.Content.ReadAsByteArrayAsync(token);
            return DockerJson.DemuxLogStream(bytes);
        }, callTimeout, ct: ct);

    private Uri Relative(string path) => new(_prefix + path, UriKind.Relative);

    private async Task<JsonDocument?> GetJsonAsync(string path, CancellationToken ct) {
        using var response = await http.GetAsync(Relative(path), ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return response.IsSuccessStatusCode
            ? JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct))
            : throw await FailureAsync("GET " + path, response, ct);
    }

    private async Task PostAsync(string path, CancellationToken ct) {
        using var response = await http.PostAsync(Relative(path), null, ct);
        if (response.StatusCode == HttpStatusCode.NotModified) return;
        if (!response.IsSuccessStatusCode) throw await FailureAsync("POST " + path, response, ct);
    }

    private static async Task<InvalidOperationException> FailureAsync(string what, HttpResponseMessage response, CancellationToken ct) {
        var body = await response.Content.ReadAsStringAsync(ct);
        var message = DockerJson.ReadErrorMessage(body) ?? response.ReasonPhrase ?? "";
        return new InvalidOperationException($"docker {what}: {(int)response.StatusCode} {message}".TrimEnd());
    }
}
