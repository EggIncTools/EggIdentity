namespace EggIdentity.Testing;

public sealed class StubHttpFactory(HttpMessageHandler handler, string baseAddress = "https://stub.test/") : IHttpClientFactory {
    private readonly Dictionary<string, Uri> _bases = [];

    public StubHttpFactory WithBase(string name, string baseAddress) {
        _bases[name] = new Uri(baseAddress);
        return this;
    }

    public HttpClient CreateClient(string name) =>
        new(handler, false) { BaseAddress = _bases.GetValueOrDefault(name) ?? new Uri(baseAddress) };
}
