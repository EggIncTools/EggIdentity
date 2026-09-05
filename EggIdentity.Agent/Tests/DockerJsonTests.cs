using System.Text;
using System.Text.Json;

namespace EggIdentity.Agent.Tests;

public class DockerJsonTests {
    private const string ContainerInspect = """
    { "Id": "0123456789abcdef0123456789abcdef",
      "Name": "/eggledger",
      "Image": "sha256:imageid",
      "State": { "Running": true },
      "Config": { "Hostname": "0123456789ab",
        "Image": "ghcr.io/x/eggledger:latest",
        "Env": ["A=1", "B=2"],
        "Labels": { "com.docker.compose.project": "stack", "com.docker.compose.service": "eggledger" },
        "MacAddress": "02:42:ac:11:00:02" },
      "HostConfig": { "RestartPolicy": { "Name": "always" }, "Binds": ["/data:/data"] },
      "NetworkSettings": { "Networks": { "stack_default": { "IPAMConfig": null,
        "Links": null,
        "Aliases": ["eggledger", "0123456789ab"],
        "NetworkID": "netid",
        "EndpointID": "epid",
        "Gateway": "172.18.0.1",
        "IPAddress": "172.18.0.5",
        "MacAddress": "02:42:ac:12:00:05",
        "DriverOpts": null } } } }
    """;

    private const string ImageInspect = """
    { "Id": "sha256:imageid",
      "RepoDigests": ["ghcr.io/x/eggledger@sha256:deadbeef"],
      "Config": { "Env": ["PATH=/usr/bin"],
        "Labels": { "org.opencontainers.image.revision": "abc1234def", "org.opencontainers.image.version": "v2.0.0" } } }
    """;

    private static JsonElement Root(string json) {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ParseContainer_ReadsIdentityEnvLabelsAndDigests() {
        var info = DockerJson.ParseContainer(Root(ContainerInspect), Root(ImageInspect));

        Assert.Equal("0123456789abcdef0123456789abcdef", info.Id);
        Assert.Equal("eggledger", info.Name);
        Assert.Equal("ghcr.io/x/eggledger:latest", info.Image);
        Assert.Equal("sha256:imageid", info.ImageId);
        Assert.True(info.Running);
        Assert.Equal(["A=1", "B=2"], info.Env);
        Assert.Equal(["ghcr.io/x/eggledger@sha256:deadbeef"], info.RepoDigests);
        Assert.Equal("stack", info.Labels["com.docker.compose.project"]);
        Assert.Equal("abc1234def", info.Revision);
        Assert.Equal("v2.0.0", info.Version);
    }

    [Fact]
    public void ParseContainer_WithoutImage_HasNoDigests() {
        var info = DockerJson.ParseContainer(Root(ContainerInspect), null);

        Assert.Empty(info.RepoDigests);
        Assert.Null(info.Revision);
    }

    [Fact]
    public void ParseImage_ReadsLabelsAndDigests() {
        var image = DockerJson.ParseImage(Root(ImageInspect));

        Assert.Equal("sha256:imageid", image.Id);
        Assert.Equal("abc1234def", image.Revision);
        Assert.Equal("v2.0.0", image.Version);
        Assert.Equal(["PATH=/usr/bin"], image.Env);
        Assert.Single(image.RepoDigests);
    }

    [Fact]
    public void BuildCreateBody_CarriesConfigOverAndReplacesImage() {
        var info = DockerJson.ParseContainer(Root(ContainerInspect), Root(ImageInspect));

        var body = DockerJson.BuildCreateBody(new ContainerSpec("eggledger", "ghcr.io/x/eggledger:2.0.0", info.Config, info.HostConfig, info.Networks));
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal("ghcr.io/x/eggledger:2.0.0", root.GetProperty("Image").GetString());
        Assert.Equal(["A=1", "B=2"], root.GetProperty("Env").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("eggledger", root.GetProperty("Labels").GetProperty("com.docker.compose.service").GetString());
        Assert.Equal("always", root.GetProperty("HostConfig").GetProperty("RestartPolicy").GetProperty("Name").GetString());
        Assert.Equal("/data:/data", root.GetProperty("HostConfig").GetProperty("Binds")[0].GetString());
        Assert.False(root.TryGetProperty("Hostname", out _));
        Assert.False(root.TryGetProperty("MacAddress", out _));
    }

    [Fact]
    public void BuildCreateBody_ShapesEndpointsToCreateInputs() {
        var info = DockerJson.ParseContainer(Root(ContainerInspect), Root(ImageInspect));

        var body = DockerJson.BuildCreateBody(new ContainerSpec("eggledger", "img:2", info.Config, info.HostConfig, info.Networks));
        using var doc = JsonDocument.Parse(body);
        var endpoint = doc.RootElement.GetProperty("NetworkingConfig").GetProperty("EndpointsConfig").GetProperty("stack_default");

        Assert.Equal(["eggledger"], endpoint.GetProperty("Aliases").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("netid", endpoint.GetProperty("NetworkID").GetString());
        Assert.False(endpoint.TryGetProperty("IPAddress", out _));
        Assert.False(endpoint.TryGetProperty("MacAddress", out _));
        Assert.False(endpoint.TryGetProperty("EndpointID", out _));
        Assert.False(endpoint.TryGetProperty("IPAMConfig", out _));
    }

    [Fact]
    public void BuildCreateBody_KeepsExplicitHostname() {
        var config = Root("""{ "Hostname": "web-1", "Image": "old" }""");
        var body = DockerJson.BuildCreateBody(new ContainerSpec("web", "new", config, Root("{}"), Root("{}")));
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("web-1", doc.RootElement.GetProperty("Hostname").GetString());
        Assert.Equal("new", doc.RootElement.GetProperty("Image").GetString());
    }

    [Fact]
    public void BuildCreateBody_AppliesSpecOverrides() {
        var info = DockerJson.ParseContainer(Root(ContainerInspect), Root(ImageInspect));
        var spec = new ContainerSpec("eggledger-swap", "img:2", info.Config, info.HostConfig, Root("{}")) {
            Cmd = ["swap", "old", "new", "eggledger"],
            Binds = ["/var/run/docker.sock:/var/run/docker.sock"],
            AutoRemove = true,
            NetworkMode = "none",
        };

        using var doc = JsonDocument.Parse(DockerJson.BuildCreateBody(spec));
        var root = doc.RootElement;
        var host = root.GetProperty("HostConfig");

        Assert.Equal(["swap", "old", "new", "eggledger"], root.GetProperty("Cmd").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["/var/run/docker.sock:/var/run/docker.sock"], host.GetProperty("Binds").EnumerateArray().Select(e => e.GetString()));
        Assert.True(host.GetProperty("AutoRemove").GetBoolean());
        Assert.Equal("none", host.GetProperty("NetworkMode").GetString());
        Assert.Equal("always", host.GetProperty("RestartPolicy").GetProperty("Name").GetString());
        Assert.Empty(root.GetProperty("NetworkingConfig").GetProperty("EndpointsConfig").EnumerateObject());
    }

    [Fact]
    public void BuildCreateBody_WithoutOverrides_LeavesCmdAndHostConfigUntouched() {
        var info = DockerJson.ParseContainer(Root(ContainerInspect), Root(ImageInspect));

        using var doc = JsonDocument.Parse(DockerJson.BuildCreateBody(new ContainerSpec("eggledger", "img:2", info.Config, info.HostConfig, info.Networks)));
        var host = doc.RootElement.GetProperty("HostConfig");

        Assert.False(doc.RootElement.TryGetProperty("Cmd", out _));
        Assert.False(host.TryGetProperty("AutoRemove", out _));
        Assert.False(host.TryGetProperty("NetworkMode", out _));
        Assert.Equal(["/data:/data"], host.GetProperty("Binds").EnumerateArray().Select(e => e.GetString()));
    }

    [Theory]
    [InlineData("0123456789ab", true)]
    [InlineData("web-1", false)]
    [InlineData("0123456789abc", false)]
    [InlineData("0123456789AB", false)]
    [InlineData(null, false)]
    public void IsAutoContainerId_MatchesTwelveHexChars(string? text, bool expected) =>
        Assert.Equal(expected, DockerJson.IsAutoContainerId(text));

    [Fact]
    public void DemuxLogStream_StripsFrameHeadersAndConcatenatesStreams() {
        var data = Frame(1, "hello ").Concat(Frame(2, "warn\n")).Concat(Frame(1, "world\n")).ToArray();

        Assert.Equal("hello warn\nworld\n", DockerJson.DemuxLogStream(data));
    }

    [Fact]
    public void DemuxLogStream_TruncatedFinalFrame_KeepsWhatArrived() {
        var data = Frame(1, "abcdef").Take(8 + 3).ToArray();

        Assert.Equal("abc", DockerJson.DemuxLogStream(data));
    }

    [Fact]
    public void DemuxLogStream_RawTtyOutput_PassesThrough() {
        var raw = Encoding.UTF8.GetBytes("plain tty text\n");

        Assert.Equal("plain tty text\n", DockerJson.DemuxLogStream(raw));
    }

    [Fact]
    public void ParsePullProgress_FormatsStatusIdAndProgress() {
        var progress = DockerJson.ParsePullProgress("""{"status":"Downloading","progressDetail":{"current":10,"total":100},"progress":"[=>   ] 10B/100B","id":"a1b2c3"}""");

        Assert.NotNull(progress);
        Assert.Equal("a1b2c3: Downloading [=>   ] 10B/100B", progress.Format());
    }

    [Fact]
    public void ParsePullProgress_StatusOnly_FormatsBareStatus() {
        var progress = DockerJson.ParsePullProgress("""{"status":"Status: Image is up to date for ghcr.io/x/y:latest"}""");

        Assert.NotNull(progress);
        Assert.Null(progress.Id);
        Assert.Equal("Status: Image is up to date for ghcr.io/x/y:latest", progress.Format());
    }

    [Fact]
    public void ParsePullProgress_Error_IsSurfaced() {
        var progress = DockerJson.ParsePullProgress("""{"errorDetail":{"message":"manifest unknown"},"error":"manifest unknown"}""");

        Assert.NotNull(progress);
        Assert.Equal("manifest unknown", progress.Error);
        Assert.Equal("error: manifest unknown", progress.Format());
    }

    [Fact]
    public void ParsePullProgress_BlankLine_IsNull() =>
        Assert.Null(DockerJson.ParsePullProgress("   "));

    [Fact]
    public void ReadErrorMessage_ExtractsDockerMessage() =>
        Assert.Equal("No such container: nope", DockerJson.ReadErrorMessage("""{"message":"No such container: nope"}"""));

    [Fact]
    public void ReadErrorMessage_NonJson_ReturnsTrimmedBody() =>
        Assert.Equal("gateway timeout", DockerJson.ReadErrorMessage(" gateway timeout \n"));

    private static byte[] Frame(byte stream, string text) {
        var payload = Encoding.UTF8.GetBytes(text);
        var frame = new byte[8 + payload.Length];
        frame[0] = stream;
        frame[4] = (byte)(payload.Length >> 24);
        frame[5] = (byte)(payload.Length >> 16);
        frame[6] = (byte)(payload.Length >> 8);
        frame[7] = (byte)payload.Length;
        payload.CopyTo(frame, 8);
        return frame;
    }
}
