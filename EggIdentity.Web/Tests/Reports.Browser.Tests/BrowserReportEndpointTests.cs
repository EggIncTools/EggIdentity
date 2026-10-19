using System.Net;
using EggIdentity.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Reports.Browser.Tests;

public class BrowserReportEndpointTests {
    private sealed record Drop(string Ship, double Amount);

    private sealed class FakeRowProvider : IReportRowProvider<Drop> {
        public ValueTask<IReadOnlyList<Drop>> ReadAsync(string? partition, CancellationToken ct) {
            IReadOnlyList<Drop> rows = [new("henerprise", 5), new("atreggies", 7)];
            return ValueTask.FromResult(rows);
        }

        public ValueTask<string> VersionAsync(string? partition, CancellationToken ct) => ValueTask.FromResult("v1");
    }

    private static readonly ReportDimension Ship = new(new FilterFieldDef("ship", "Ship", FilterValueKind.Select, FilterOps.Equality));

    private static readonly ReportMeasure Value = new("value", "Value");

    private static readonly ReportSource<Drop> Source = new(
        "drops", "Drops", [Ship], [Value], (d, key) => key switch {
            "ship" => FilterSubjectValue.Of(d.Ship),
            "value" => FilterSubjectValue.Of(d.Amount),
            _ => FilterSubjectValue.Absent,
        });

    private static readonly SnapshotSchema Schema = new("drops", [new("ship")], ["value"]);

    private static async Task<WebApplication> BuildAppAsync(SnapshotSchema? schema) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddReportSource<Drop, FakeRowProvider>(Source, schema);
        builder.Services.AddEggIdentityBrowserReports(o => o.ScopeKey = _ => "test-user");

        var app = builder.Build();
        app.MapEggIdentityBrowserReports();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Descriptor_ReturnsJson() {
        await using var app = await BuildAppAsync(Schema);
        using var client = app.GetTestClient();

        using var res = await client.GetAsync("/_reports/descriptor/drops");

        res.EnsureSuccessStatusCode();
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"drops\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Descriptor_UnknownIs404() {
        await using var app = await BuildAppAsync(Schema);
        using var client = app.GetTestClient();

        using var res = await client.GetAsync("/_reports/descriptor/missing");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Schema_NoneIs204() {
        await using var app = await BuildAppAsync(null);
        using var client = app.GetTestClient();

        using var res = await client.GetAsync("/_reports/schema/drops");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Caps_ReturnsBothTiers() {
        await using var app = await BuildAppAsync(Schema);
        using var client = app.GetTestClient();

        using var res = await client.GetAsync("/_reports/caps");

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("Live", body, StringComparison.Ordinal);
        Assert.Contains("Snapshot", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Snapshot_ReturnsOctetStreamWithETag() {
        await using var app = await BuildAppAsync(Schema);
        using var client = app.GetTestClient();

        using var res = await client.GetAsync("/_reports/snapshot/drops");

        res.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(SnapshotCache.ETagFor("v1"), res.Headers.ETag?.Tag);
        Assert.True(res.Headers.CacheControl is { Private: true, NoCache: true });
    }

    [Fact]
    public async Task Snapshot_PartitionIsPassedToProvider() {
        await using var app = await BuildAppAsync(Schema);
        using var client = app.GetTestClient();

        using var res = await client.GetAsync("/_reports/snapshot/drops?p=acct-9");

        res.EnsureSuccessStatusCode();
        Assert.NotNull(app.Services.GetRequiredService<SnapshotCache>().CachedVersion("test-user", "acct-9", "drops"));
        Assert.Null(app.Services.GetRequiredService<SnapshotCache>().CachedVersion("test-user", null, "drops"));
    }

    [Fact]
    public async Task Snapshot_MatchingIfNoneMatchIs304WithoutCapture() {
        await using var app = await BuildAppAsync(Schema);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/_reports/snapshot/drops");
        request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(SnapshotCache.ETagFor("v1")));
        using var res = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, res.StatusCode);
        Assert.Null(app.Services.GetRequiredService<SnapshotCache>().CachedVersion("test-user", null, "drops"));
    }
}
