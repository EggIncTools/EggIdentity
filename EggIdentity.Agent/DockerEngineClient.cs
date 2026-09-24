using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EggIdentity.Resilience;

namespace EggIdentity.Agent;

public sealed class DockerEngineClient(HttpClient http, TimeSpan callTimeout, Func<TimeSpan> pullTimeout) : IDockerEngine {
    public const string DefaultSocketPath = "/var/run/docker.sock";
    public const string ApiVersion = "v1.45";

    public static HttpClient CreateHttpClient() {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "";
        if (dockerHost.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase) || dockerHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) {
            var authority = dockerHost[(dockerHost.IndexOf("://", StringComparison.Ordinal) + 3)..].TrimEnd('/');
            return new HttpClient { BaseAddress = new Uri($"http://{authority}/{ApiVersion}/"), Timeout = Timeout.InfiniteTimeSpan };
        }

        var socketPath = dockerHost.StartsWith("unix://", StringComparison.OrdinalIgnoreCase) ? dockerHost["unix://".Length..] : DefaultSocketPath;
        var handler = new SocketsHttpHandler {
            ConnectCallback = async (_, ct) => {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                } catch {
                    socket.Dispose();
                    throw;
                }
            },
        };
        return new HttpClient(handler) { BaseAddress = new Uri($"http://localhost/{ApiVersion}/"), Timeout = Timeout.InfiniteTimeSpan };
    }

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
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));
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
            using var response = await http.GetAsync(new Uri($"containers/{Uri.EscapeDataString(name)}/logs?stdout=1&stderr=1&tail={lines}", UriKind.Relative), token);
            if (!response.IsSuccessStatusCode) throw await FailureAsync("logs", response, token);
            var bytes = await response.Content.ReadAsByteArrayAsync(token);
            return DockerJson.DemuxLogStream(bytes);
        }, callTimeout, ct: ct);

    private async Task<JsonDocument?> GetJsonAsync(string path, CancellationToken ct) {
        using var response = await http.GetAsync(new Uri(path, UriKind.Relative), ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return response.IsSuccessStatusCode
            ? JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct))
            : throw await FailureAsync("GET " + path, response, ct);
    }

    private async Task PostAsync(string path, CancellationToken ct) {
        using var response = await http.PostAsync(new Uri(path, UriKind.Relative), null, ct);
        if (response.StatusCode == HttpStatusCode.NotModified) return;
        if (!response.IsSuccessStatusCode) throw await FailureAsync("POST " + path, response, ct);
    }

    private static async Task<InvalidOperationException> FailureAsync(string what, HttpResponseMessage response, CancellationToken ct) {
        var body = await response.Content.ReadAsStringAsync(ct);
        var message = DockerJson.ReadErrorMessage(body) ?? response.ReasonPhrase ?? "";
        return new InvalidOperationException($"docker {what}: {(int)response.StatusCode} {message}".TrimEnd());
    }
}
