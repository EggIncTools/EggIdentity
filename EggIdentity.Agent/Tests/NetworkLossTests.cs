using System.Text.Json;

namespace EggIdentity.Agent.Tests;

public class NetworkLossTests {
    private const string StoppedOnABridge = """
    { "Id": "0123456789abcdef0123456789abcdef",
      "Name": "/eggidentity",
      "Image": "sha256:imageid",
      "State": { "Running": false },
      "Config": { "Image": "ghcr.io/x/eggidentity:latest" },
      "HostConfig": { "NetworkMode": "proxy-v6" },
      "NetworkSettings": { "Networks": {} } }
    """;

    private const string RunningOnABridge = """
    { "Id": "0123456789abcdef0123456789abcdef",
      "Name": "/eggidentity",
      "Image": "sha256:imageid",
      "State": { "Running": true },
      "Config": { "Image": "ghcr.io/x/eggidentity:latest" },
      "HostConfig": { "NetworkMode": "proxy-v6" },
      "NetworkSettings": { "Networks": { "proxy-v6": { "NetworkID": "netid", "Aliases": ["eggidentity"] } } } }
    """;

    private const string HostNetworked = """
    { "Id": "0123456789abcdef0123456789abcdef",
      "Name": "/eggincognito",
      "Image": "sha256:imageid",
      "State": { "Running": false },
      "Config": { "Image": "ghcr.io/x/eggincognito:latest" },
      "HostConfig": { "NetworkMode": "host" },
      "NetworkSettings": { "Networks": {} } }
    """;

    private static ContainerInfo Parse(string json) {
        using var doc = JsonDocument.Parse(json);
        return DockerJson.ParseContainer(doc.RootElement.Clone(), null);
    }

    [Fact]
    public void AStoppedBridgeContainerReportsNoEndpoints_AndTheDeployIsRefused() {
        var info = Parse(StoppedOnABridge);

        var refusal = DeployService.DescribeNetworkLoss(info);

        Assert.NotNull(refusal);
        Assert.Contains("eggidentity", refusal, StringComparison.Ordinal);
        Assert.Contains("attach the new container to nothing", refusal, StringComparison.Ordinal);
        Assert.Contains("proxy-v6", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void ARunningBridgeContainerIsAllowed() =>
        Assert.Null(DeployService.DescribeNetworkLoss(Parse(RunningOnABridge)));

    [Fact]
    public void AHostNetworkedContainerIsAllowed_BecauseItLegitimatelyHasNoEndpoints() =>
        Assert.Null(DeployService.DescribeNetworkLoss(Parse(HostNetworked)));

    [Fact]
    public void ANoneNetworkedContainerIsAllowed() {
        var info = Parse(HostNetworked.Replace("\"host\"", "\"none\"", StringComparison.Ordinal));

        Assert.Null(DeployService.DescribeNetworkLoss(info));
    }

    [Fact]
    public void AContainerSharingAnotherNetworkNamespaceIsAllowed() {
        var info = Parse(HostNetworked.Replace("\"host\"", "\"container:abc123\"", StringComparison.Ordinal));

        Assert.Null(DeployService.DescribeNetworkLoss(info));
    }

    [Fact]
    public void ARunningContainerStillCarriesItsEndpointsIntoTheCreateBody() {
        var info = Parse(RunningOnABridge);

        var body = DockerJson.BuildCreateBody(
            new ContainerSpec("eggidentity", "img:2", info.Config, info.HostConfig, info.Networks));

        using var doc = JsonDocument.Parse(body);
        var endpoints = doc.RootElement.GetProperty("NetworkingConfig").GetProperty("EndpointsConfig");
        Assert.True(endpoints.TryGetProperty("proxy-v6", out _));
    }

    [Theory]
    [InlineData("host")]
    [InlineData("none")]
    [InlineData("container:abc")]
    public void NonAttachableModesAreRecognized(string mode) =>
        Assert.True(DockerJson.IsNonAttachable(mode));

    [Theory]
    [InlineData("proxy-v6")]
    [InlineData("bridge")]
    [InlineData("stack_default")]
    public void AttachableModesAreNotMistakenForNonAttachable(string mode) =>
        Assert.False(DockerJson.IsNonAttachable(mode));

    [Fact]
    public void AContainerWithNoHostConfigAtAll_IsNotBlocked_BecauseUnknownIsNotDetached() {
        var unknown = new ContainerInfo(
            "id", "eggledger", "img", "sha256:img", [], [], new Dictionary<string, string>(),
            true, default, default, default);

        Assert.Null(DeployService.DescribeNetworkLoss(unknown));
    }
}
