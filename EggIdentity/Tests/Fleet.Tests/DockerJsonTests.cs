using System.Text;
using System.Text.Json;

namespace EggIdentity.Fleet.Tests;

public class DockerJsonTests {
    private const string ContainerInspect = """
    { "Id": "0123456789abcdef0123456789abcdef",
      "Name": "/eggledger",
      "Image": "sha256:imageid",
      "State": { "Running": true, "StartedAt": "2026-09-26T10:00:00Z" },
      "Config": { "Image": "ghcr.io/x/eggledger:latest",
        "Env": ["A=1", "B=2"],
        "Labels": { "com.docker.compose.project": "stack" } } }
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
    public void ParseContainer_ReadsIdentityEnvLabelsDigestsAndStart() {
        var info = DockerJson.ParseContainer(Root(ContainerInspect), Root(ImageInspect));

        Assert.Equal("eggledger", info.Name);
        Assert.Equal("ghcr.io/x/eggledger:latest", info.Image);
        Assert.Equal("sha256:imageid", info.ImageId);
        Assert.True(info.Running);
        Assert.Equal(["A=1", "B=2"], info.Env);
        Assert.Equal(["ghcr.io/x/eggledger@sha256:deadbeef"], info.RepoDigests);
        Assert.Equal("abc1234def", info.Revision);
        Assert.Equal("v2.0.0", info.Version);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero), info.StartedAt);
    }

    [Fact]
    public void ParseContainer_Stopped_HasNoStartTime() {
        var info = DockerJson.ParseContainer(Root(ContainerInspect.Replace("\"Running\": true", "\"Running\": false", StringComparison.Ordinal)), null);

        Assert.False(info.Running);
        Assert.Null(info.StartedAt);
    }

    [Fact]
    public void DemuxLogStream_StripsFrameHeadersAndConcatenatesStreams() {
        var data = Frame(1, "hello ").Concat(Frame(2, "warn\n")).Concat(Frame(1, "world\n")).ToArray();

        Assert.Equal("hello warn\nworld\n", DockerJson.DemuxLogStream(data));
    }

    [Fact]
    public void DemuxLogStream_RawTtyOutput_PassesThrough() =>
        Assert.Equal("plain tty text\n", DockerJson.DemuxLogStream(Encoding.UTF8.GetBytes("plain tty text\n")));

    [Fact]
    public void ParsePullProgress_Error_IsSurfaced() {
        var progress = DockerJson.ParsePullProgress("""{"errorDetail":{"message":"manifest unknown"},"error":"manifest unknown"}""");

        Assert.Equal("error: manifest unknown", progress!.Format());
    }

    [Fact]
    public void ReadErrorMessage_ExtractsDockerMessage() =>
        Assert.Equal("No such container: nope", DockerJson.ReadErrorMessage("""{"message":"No such container: nope"}"""));

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
