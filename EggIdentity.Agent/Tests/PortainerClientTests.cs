using System.Net;
using System.Text.Json;

namespace EggIdentity.Agent.Tests;

public class PortainerClientTests {
    private const string GitRedeployPath = "/api/stacks/56/git/redeploy?endpointId=9";

    private static (PortainerClient Client, FakePortainerHandler Handler) Build(int stackId, string stackJson) {
        var handler = new FakePortainerHandler().On("GET", $"/api/stacks/{stackId}", stackJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://portainer.test/") };
        return (new PortainerClient(http, stackId, 9), handler);
    }

    private static PortainerStack Parse(string json) {
        using var doc = JsonDocument.Parse(json);
        return PortainerClient.ReadStack(doc.RootElement);
    }

    private static Dictionary<string, string?> Changes(string name, string? value) => new(StringComparer.Ordinal) { [name] = value };

    [Fact]
    public void ReadStack_ParsesGitStackWithWebhook() {
        var stack = Parse(PortainerFakes.GitStack);

        Assert.Equal(56, stack.Id);
        Assert.Equal("ei-servers", stack.Name);
        Assert.Equal(2, stack.Type);
        Assert.Equal(9, stack.EndpointId);
        Assert.Equal([new StackEnvEntry("A", "1"), new StackEnvEntry("B", "")], stack.Env);
        Assert.Equal(new PortainerAutoUpdate(PortainerFakes.Webhook, true, false), stack.AutoUpdate);
        Assert.Equal(
            new PortainerGitConfig("https://gitea:3000/EIStacks/ei-servers.git", "refs/heads/main", "docker-compose.yml", false, "portainer"),
            stack.GitConfig);
        Assert.True(stack.GitBacked);
        Assert.True(stack.Prune);
    }

    [Fact]
    public void ReadStack_WebEditorStack_HasNoGitAndNoAutoUpdate() {
        var stack = Parse(PortainerFakes.WebEditorStack());

        Assert.Equal(3, stack.Id);
        Assert.Equal("db", stack.Name);
        Assert.False(stack.GitBacked);
        Assert.Null(stack.AutoUpdate);
        Assert.False(stack.Prune);
        Assert.Empty(stack.Env);
    }

    [Fact]
    public void ReadStack_GitAuthWithEmptyUsername_IsAnonymous() {
        var stack = Parse(PortainerFakes.GitStack.Replace("\"Username\":\"portainer\"", "\"Username\":\"\"", StringComparison.Ordinal));

        Assert.True(stack.GitBacked);
        Assert.Null(stack.GitConfig!.Username);
    }

    [Fact]
    public void MergeEnv_UpdatesRemovesAndAppends() {
        var current = new List<StackEnvEntry> { new("A", "1"), new("B", "2"), new("C", "3") };
        var changes = new Dictionary<string, string?>(StringComparer.Ordinal) { ["B"] = "9", ["C"] = null, ["D"] = "4" };

        var merged = PortainerClient.MergeEnv(current, changes);

        Assert.Equal([new StackEnvEntry("A", "1"), new StackEnvEntry("B", "9"), new StackEnvEntry("D", "4")], merged);
    }

    [Fact]
    public async Task UpdateEnv_GitStack_PutsGitRedeployWithMergedEnvAndKeepsCredentials() {
        var (client, handler) = Build(56, PortainerFakes.GitStack);
        handler.On("PUT", GitRedeployPath);

        await client.UpdateEnvAsync(Changes("B", "2"), CancellationToken.None);

        Assert.Equal(["GET /api/stacks/56", "PUT " + GitRedeployPath], handler.Calls);
        Assert.Equal(GitRedeployPath, handler.Write!.RequestUri!.PathAndQuery);
        var body = PortainerFakes.Body(handler);
        Assert.Equal("refs/heads/main", body.GetProperty("RepositoryReferenceName").GetString());
        Assert.True(body.GetProperty("RepositoryAuthentication").GetBoolean());
        Assert.Equal("portainer", body.GetProperty("RepositoryUsername").GetString());
        Assert.Equal("", body.GetProperty("RepositoryPassword").GetString());
        Assert.True(body.GetProperty("Prune").GetBoolean());
        Assert.False(body.GetProperty("RepullImageAndRedeploy").GetBoolean());
        Assert.Equal([("A", "1"), ("B", "2")], PortainerFakes.Env(body));
        Assert.False(body.TryGetProperty("AutoUpdate", out _));
    }

    [Fact]
    public async Task UpdateEnv_WebEditorStack_PutsStackWithFileContent() {
        var (client, handler) = Build(3, PortainerFakes.WebEditorStack("""[{"name":"A","value":"1"}]"""));
        handler.On("GET", "/api/stacks/3/file", """{"StackFileContent":"services: {}"}""").On("PUT", "/api/stacks/3?endpointId=9");

        await client.UpdateEnvAsync(Changes("A", "2"), CancellationToken.None);

        Assert.Equal(["GET /api/stacks/3", "GET /api/stacks/3/file", "PUT /api/stacks/3?endpointId=9"], handler.Calls);
        var body = PortainerFakes.Body(handler);
        Assert.Equal("services: {}", body.GetProperty("StackFileContent").GetString());
        Assert.Equal([("A", "2")], PortainerFakes.Env(body));
        Assert.True(body.GetProperty("RepullImageAndRedeploy").GetBoolean());
        Assert.False(body.GetProperty("Prune").GetBoolean());
    }

    [Fact]
    public async Task Redeploy_GitStack_DoesNotForceRecreateFlagOnPayload() {
        var (client, handler) = Build(56, PortainerFakes.GitStack);
        handler.On("PUT", GitRedeployPath);

        await client.RedeployAsync(forceRecreate: false, CancellationToken.None);

        Assert.Equal(GitRedeployPath, handler.Write!.RequestUri!.PathAndQuery);
        var body = PortainerFakes.Body(handler);
        Assert.False(body.GetProperty("RepullImageAndRedeploy").GetBoolean());
        Assert.Equal([("A", "1"), ("B", "")], PortainerFakes.Env(body));
    }

    [Fact]
    public async Task Redeploy_Conflict_ThrowsStackBusy() {
        var (client, handler) = Build(56, PortainerFakes.GitStack);
        handler.On("PUT", GitRedeployPath, status: HttpStatusCode.Conflict);

        await Assert.ThrowsAsync<StackBusyException>(() => client.RedeployAsync(forceRecreate: false, CancellationToken.None));
    }

    [Fact]
    public async Task Redeploy_ServerError_ThrowsWithStatusAndBody() {
        var (client, handler) = Build(56, PortainerFakes.GitStack);
        handler.On("PUT", GitRedeployPath, "boom", HttpStatusCode.InternalServerError);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.RedeployAsync(forceRecreate: false, CancellationToken.None));

        Assert.Contains("500", error.Message, StringComparison.Ordinal);
        Assert.Contains("boom", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redeploy_SwarmStack_Refuses() {
        var (client, handler) = Build(56, PortainerFakes.GitStack.Replace("\"Type\":2", "\"Type\":1", StringComparison.Ordinal));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.RedeployAsync(forceRecreate: false, CancellationToken.None));

        Assert.Contains("compose", error.Message, StringComparison.Ordinal);
        Assert.Null(handler.Write);
    }
}
