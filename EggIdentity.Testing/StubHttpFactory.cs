using System.Net;

namespace EggIdentity.Testing;

public sealed class StubHttpFactory(HttpMessageHandler? handler = null, string baseAddress = "https://stub.test/") : IHttpClientFactory {
    private readonly HttpMessageHandler _handler = handler ?? new StubHttpMessageHandler(HttpStatusCode.OK);
    private readonly Dictionary<string, Uri> _bases = [];
    private readonly List<string> _names = [];

    public IReadOnlyList<string> Names => _names;

    public string? LastName => _names.Count == 0 ? null : _names[^1];

    public StubHttpFactory WithBase(string name, string baseAddress) {
        _bases[name] = new Uri(baseAddress);
        return this;
    }

    public HttpClient CreateClient(string name) {
        _names.Add(name);
        return new HttpClient(_handler, false) { BaseAddress = _bases.GetValueOrDefault(name) ?? new Uri(baseAddress) };
    }
}
