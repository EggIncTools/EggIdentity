using System.Text.Json;
using System.Text.Json.Serialization;

namespace EggIdentity.Settings.Tests;

public class CollectionBinderTests {
    private sealed record DeployApp(string Name, string Image, bool AutoDeploy, int Replicas, string? RepoUrl, bool? Enabled);

    private sealed class Renamed {
        [JsonPropertyName("client_id")] public string ClientId { get; init; } = "";
        public string Origin { get; init; } = "";
    }

    private static Dictionary<string, string?> Values(params (string Field, string? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Field, p => p.Value, StringComparer.Ordinal);

    [Fact]
    public void Bind_MapsSnakeCaseFieldsToPascalCaseProperties() {
        var app = CollectionBinder.Bind<DeployApp>(Values(
            ("name", "eggledger"), ("image", "ghcr.io/x/y"), ("auto_deploy", "true"), ("replicas", "3"), ("repo_url", "https://x")));

        Assert.Equal("eggledger", app.Name);
        Assert.Equal("ghcr.io/x/y", app.Image);
        Assert.True(app.AutoDeploy);
        Assert.Equal(3, app.Replicas);
        Assert.Equal("https://x", app.RepoUrl);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void Bind_ReadsBoolsFromStrings(string raw, bool expected) {
        var app = CollectionBinder.Bind<DeployApp>(Values(("name", "a"), ("image", "b"), ("auto_deploy", raw), ("enabled", raw)));

        Assert.Equal(expected, app.AutoDeploy);
        Assert.Equal(expected, app.Enabled);
    }

    [Fact]
    public void Bind_RejectsNonBoolStrings() {
        Assert.Throws<JsonException>(() => CollectionBinder.Bind<DeployApp>(Values(("name", "a"), ("image", "b"), ("auto_deploy", "maybe"))));
    }

    [Fact]
    public void Bind_TreatsBlankAndNullAsAbsent() {
        var app = CollectionBinder.Bind<DeployApp>(Values(("name", "a"), ("image", "b"), ("replicas", ""), ("repo_url", null), ("enabled", "")));

        Assert.Equal(0, app.Replicas);
        Assert.Null(app.RepoUrl);
        Assert.Null(app.Enabled);
        Assert.False(app.AutoDeploy);
    }

    [Fact]
    public void Bind_HonoursExplicitPropertyNames() {
        var reg = CollectionBinder.Bind<Renamed>(Values(("client_id", "abc"), ("origin", "https://o")));

        Assert.Equal("abc", reg.ClientId);
        Assert.Equal("https://o", reg.Origin);
    }
}
