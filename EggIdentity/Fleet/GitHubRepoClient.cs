using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EggIdentity.Fleet;

public sealed partial record RepoRef(string Owner, string Name) {
    [GeneratedRegex("""^https?://github\.com/(?<o>[A-Za-z0-9_.-]+)/(?<n>[A-Za-z0-9_.-]+?)(?:\.git)?/?$""", RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    public static RepoRef? Parse(string? repoUrl) =>
        repoUrl is not null && Url().Match(repoUrl.Trim()) is { Success: true } m ? new RepoRef(m.Groups["o"].Value, m.Groups["n"].Value) : null;
}

public sealed record RepoFile(string Text, string Sha);

public sealed record RepoCommit(string CommitSha, string BlobSha);

public sealed class GitHubRejectedException(string message) : Exception(message);

public interface IGitHubRepo {
    Task<RepoFile?> GetFileAsync(RepoRef repo, string path, string branch, CancellationToken ct);
    Task<RepoCommit> PutFileAsync(RepoRef repo, string path, string branch, string text, string blobSha, string message, CancellationToken ct);
    Task<bool> IsAncestorAsync(RepoRef repo, string ancestor, string head, CancellationToken ct);
}

public sealed class GitHubRepoClient(HttpClient http, Func<string?> token) : IGitHubRepo {
    public static readonly Uri BaseAddress = new("https://api.github.com/");
    private const string CommitterName = "EggIdentity Fleet";
    private const string CommitterEmail = "fleet@egginc.tools";

    public async Task<RepoFile?> GetFileAsync(RepoRef repo, string path, string branch, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(repo);
        using var request = Request(HttpMethod.Get, $"{Contents(repo, path)}?ref={Uri.EscapeDataString(branch)}");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureAsync(response, ct);
        var body = await response.Content.ReadFromJsonAsync<ContentsBody>(ct)
            ?? throw new GitHubRejectedException("GitHub returned an empty contents body");
        var bytes = Convert.FromBase64String(body.Content?.Replace("\n", "", StringComparison.Ordinal) ?? "");
        return new RepoFile(Encoding.UTF8.GetString(bytes), body.Sha ?? "");
    }

    public async Task<RepoCommit> PutFileAsync(
        RepoRef repo, string path, string branch, string text, string blobSha, string message, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(repo);
        using var request = Request(HttpMethod.Put, Contents(repo, path));
        request.Content = JsonContent.Create(new PutBody(
            message, Convert.ToBase64String(Encoding.UTF8.GetBytes(text)), blobSha, branch, new Committer(CommitterName, CommitterEmail)));
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        var body = await response.Content.ReadFromJsonAsync<PutResult>(ct);
        if (body?.Commit?.Sha is not { Length: > 0 } commit || body.Content?.Sha is not { Length: > 0 } blob)
            throw new GitHubRejectedException("GitHub accepted the commit but returned no sha");
        return new RepoCommit(commit, blob);
    }

    public async Task<bool> IsAncestorAsync(RepoRef repo, string ancestor, string head, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(repo);
        using var request = Request(HttpMethod.Get,
            $"repos/{repo.Owner}/{repo.Name}/compare/{Uri.EscapeDataString(ancestor)}...{Uri.EscapeDataString(head)}");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureAsync(response, ct);
        var body = await response.Content.ReadFromJsonAsync<CompareBody>(ct);
        return body?.Status is "ahead" or "identical";
    }

    private static string Contents(RepoRef repo, string path) =>
        $"repos/{repo.Owner}/{repo.Name}/contents/{string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString))}";

    private HttpRequestMessage Request(HttpMethod method, string relative) {
        var request = new HttpRequestMessage(method, new Uri(BaseAddress, relative));
        request.Headers.UserAgent.ParseAdd("eggidentity-fleet");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        if (token() is { Length: > 0 } t) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
        return request;
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct) {
        if (response.IsSuccessStatusCode) return;
        var message = ErrorMessage(await response.Content.ReadAsStringAsync(ct));
        throw new GitHubRejectedException($"GitHub {(int)response.StatusCode}: {message ?? response.ReasonPhrase ?? "request failed"}");
    }

    private static string? ErrorMessage(string text) {
        try {
            return JsonSerializer.Deserialize<ErrorBody>(text)?.Message;
        } catch (JsonException) {
            return null;
        }
    }

    private sealed record ContentsBody(
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("sha")] string? Sha);

    private sealed record PutBody(
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("sha")] string Sha,
        [property: JsonPropertyName("branch")] string Branch,
        [property: JsonPropertyName("committer")] Committer Committer);

    private sealed record Committer(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email);

    private sealed record PutResult(
        [property: JsonPropertyName("commit")] ShaRef? Commit,
        [property: JsonPropertyName("content")] ShaRef? Content);

    private sealed record ShaRef([property: JsonPropertyName("sha")] string? Sha);

    private sealed record CompareBody([property: JsonPropertyName("status")] string? Status);

    private sealed record ErrorBody([property: JsonPropertyName("message")] string? Message);
}
