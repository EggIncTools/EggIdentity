using System.Net;

namespace EggIdentity.Fleet.Tests;

public class PortainerApiTests {
    private const string GitRedeployPath = "/api/stacks/56/git/redeploy?endpointId=9";

    private static Dictionary<string, string?> Changes(string name, string? value) => new(StringComparer.Ordinal) { [name] = value };

    [Fact]
    public void ReadStack_ParsesGitStackWithWebhook() {
        var stack = PortainerFakes.Parse(PortainerFakes.GitStack);

        Assert.Equal(56, stack.Id);
        Assert.Equal("egg-apps", stack.Name);
        Assert.Equal(9, stack.EndpointId);
        Assert.Equal([new StackEnvEntry("A", "1"), new StackEnvEntry("B", "")], stack.Env);
        Assert.Equal(new PortainerAutoUpdate(PortainerFakes.Webhook, true, false), stack.AutoUpdate);
        Assert.True(stack.GitBacked);
        Assert.Equal("portainer", stack.GitConfig!.Username);
        Assert.True(stack.Prune);
    }

    [Fact]
    public async Task ListStacks_ReadsArrayAndSendsApiKey() {
        var (api, handler) = PortainerFakes.Api();
        handler.On("GET", "/api/stacks", $"[{PortainerFakes.GitStack},{PortainerFakes.WebEditorStack()}]");

        var stacks = await api.ListStacksAsync(CancellationToken.None);

        Assert.Equal(["egg-apps", "db"], stacks.Select(s => s.Name));
        Assert.Equal("key", Assert.Single(handler.Requests[0].Headers.GetValues("X-API-Key")));
    }

    [Fact]
    public async Task Docker_RoutesThroughEndpointProxy() {
        var (api, handler) = PortainerFakes.Api();
        handler.On("POST", "/api/endpoints/9/docker/containers/eggledger/restart?t=30", status: HttpStatusCode.NoContent);

        await api.Docker(9).RestartAsync("eggledger", CancellationToken.None);

        Assert.Equal(["POST /api/endpoints/9/docker/containers/eggledger/restart?t=30"], handler.Calls);
        Assert.Equal("key", Assert.Single(handler.Requests[0].Headers.GetValues("X-API-Key")));
    }

    [Fact]
    public async Task Docker_InspectMissingContainer_IsNull() {
        var (api, _) = PortainerFakes.Api();

        Assert.Null(await api.Docker(9).InspectContainerAsync("ghost", CancellationToken.None));
    }

    [Fact]
    public async Task Docker_InspectImageByName_ResolvesIdSoNoSlashReachesThePath() {
        const string id = "sha256:abc";
        var filters = Uri.EscapeDataString("""{"reference":["ghcr.io/egginctools/eggledger:latest"]}""");
        var (api, handler) = PortainerFakes.Api();
        handler.On("GET", $"/api/endpoints/9/docker/images/json?filters={filters}", $$"""[{"Id":"{{id}}"}]""")
            .On("GET", $"/api/endpoints/9/docker/images/{Uri.EscapeDataString(id)}/json", $$"""{"Id":"{{id}}","RepoDigests":["ghcr.io/egginctools/eggledger@sha256:def"]}""");

        var image = await api.Docker(9).InspectImageAsync("ghcr.io/egginctools/eggledger:latest", CancellationToken.None);

        Assert.NotNull(image);
        Assert.Equal(id, image.Id);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri?.AbsolutePath is { } path && path.Contains("%2F", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Redeploy_GitStack_PostsWebhookWithoutApiRequirement() {
        var (api, handler) = PortainerFakes.Api();
        handler.On("GET", "/api/stacks/56", PortainerFakes.GitStack)
            .On("POST", $"/api/stacks/webhooks/{PortainerFakes.Webhook}", status: HttpStatusCode.Accepted);

        await api.RedeployAsync(PortainerFakes.Parse(PortainerFakes.GitStack), CancellationToken.None);

        Assert.Equal(["GET /api/stacks/56", $"POST /api/stacks/webhooks/{PortainerFakes.Webhook}"], handler.Calls);
        Assert.DoesNotContain(handler.Calls, c => c.StartsWith("PUT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Redeploy_GitStackWithoutForce_RefusesBeforeAnyWrite() {
        var unforced = PortainerFakes.GitStack.Replace("\"ForceUpdate\":true", "\"ForceUpdate\":false", StringComparison.Ordinal);
        var (api, handler) = PortainerFakes.Api();
        handler.On("GET", "/api/stacks/56", unforced);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => api.RedeployAsync(PortainerFakes.Parse(unforced), CancellationToken.None));

        Assert.Contains("force redeploy", error.Message, StringComparison.Ordinal);
        Assert.Null(handler.Write);
    }

    [Fact]
    public async Task Redeploy_WebhookConflict_ThrowsStackBusy() {
        var (api, handler) = PortainerFakes.Api();
        handler.On("GET", "/api/stacks/56", PortainerFakes.GitStack)
            .On("POST", $"/api/stacks/webhooks/{PortainerFakes.Webhook}", status: HttpStatusCode.Conflict);

        await Assert.ThrowsAsync<StackBusyException>(() => api.RedeployAsync(PortainerFakes.Parse(PortainerFakes.GitStack), CancellationToken.None));
    }

    [Fact]
    public async Task Redeploy_WebEditorStack_PutsStackWithoutRepull() {
        var (api, handler) = PortainerFakes.Api();
        handler.On("GET", "/api/stacks/3", PortainerFakes.WebEditorStack())
            .On("GET", "/api/stacks/3/file", """{"StackFileContent":"services: {}"}""")
            .On("PUT", "/api/stacks/3?endpointId=9");

        await api.RedeployAsync(PortainerFakes.Parse(PortainerFakes.WebEditorStack()), CancellationToken.None);

        Assert.Equal("/api/stacks/3?endpointId=9", handler.Write!.RequestUri!.PathAndQuery);
        Assert.False(PortainerFakes.Body(handler).GetProperty("RepullImageAndRedeploy").GetBoolean());
    }

    [Fact]
    public async Task UpdateEnv_GitStack_PutsGitRedeployWithMergedEnvAndKeepsCredentials() {
        var (api, handler) = PortainerFakes.Api();
        handler.On("GET", "/api/stacks/56", PortainerFakes.GitStack).On("PUT", GitRedeployPath);

        await api.UpdateEnvAsync(PortainerFakes.Parse(PortainerFakes.GitStack), Changes("B", "2"), CancellationToken.None);

        Assert.Equal(["GET /api/stacks/56", "PUT " + GitRedeployPath], handler.Calls);
        var body = PortainerFakes.Body(handler);
        Assert.True(body.GetProperty("RepositoryAuthentication").GetBoolean());
        Assert.Equal("", body.GetProperty("RepositoryPassword").GetString());
        Assert.Equal([("A", "1"), ("B", "2")], PortainerFakes.Env(body));
        Assert.False(body.TryGetProperty("AutoUpdate", out _));
    }

    [Fact]
    public async Task UpdateEnv_WebEditorStack_PutsStackWithRepull() {
        var stack = PortainerFakes.WebEditorStack("""[{"name":"A","value":"1"}]""");
        var (api, handler) = PortainerFakes.Api();
        handler.On("GET", "/api/stacks/3", stack)
            .On("GET", "/api/stacks/3/file", """{"StackFileContent":"services: {}"}""")
            .On("PUT", "/api/stacks/3?endpointId=9");

        await api.UpdateEnvAsync(PortainerFakes.Parse(stack), Changes("A", "2"), CancellationToken.None);

        var body = PortainerFakes.Body(handler);
        Assert.Equal([("A", "2")], PortainerFakes.Env(body));
        Assert.True(body.GetProperty("RepullImageAndRedeploy").GetBoolean());
    }

    [Fact]
    public void MergeEnv_UpdatesRemovesAndAppends() {
        var current = new List<StackEnvEntry> { new("A", "1"), new("B", "2"), new("C", "3") };
        var changes = new Dictionary<string, string?>(StringComparer.Ordinal) { ["B"] = "9", ["C"] = null, ["D"] = "4" };

        Assert.Equal([new StackEnvEntry("A", "1"), new StackEnvEntry("B", "9"), new StackEnvEntry("D", "4")], PortainerApi.MergeEnv(current, changes));
    }
}
