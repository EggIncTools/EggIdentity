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
      "NetworkSettings": { "Networks": { "stack_default": { "NetworkID": "netid", "IPAddress": "172.18.0.5" } } } }
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
    public void ParseContainer_ContainerLabelWinsOverImageLabel() {
        var container = ContainerInspect.Replace(
            "\"com.docker.compose.service\": \"eggledger\"",
            "\"com.docker.compose.service\": \"eggledger\", \"org.opencontainers.image.revision\": \"container-rev\"",
            StringComparison.Ordinal);

        var info = DockerJson.ParseContainer(Root(container), Root(ImageInspect));

        Assert.Equal("container-rev", info.Revision);
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
