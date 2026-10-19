using System.Reflection;
using System.Reflection.Emit;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace EggIdentity.UI.Tests;

public class AppVersionTests {
    private sealed record VersionBody(string Version, string Display);

    [Fact]
    public async Task MapAppVersion_ServesVersionAndDisplayUncached() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        const string version = "257.0.0+abe9392f00d";
        app.MapAppVersion(version);
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var res = await client.GetAsync(AppVersion.Path);

        res.EnsureSuccessStatusCode();
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
        var body = await res.Content.ReadFromJsonAsync<VersionBody>();
        Assert.Equal(version, body?.Version);
        Assert.Equal(BuildVersion.Describe(version, null), body?.Display);
    }

    [Fact]
    public void Read_ReturnsInformationalVersion() {
        var name = new AssemblyName("VersionedProbe");
        var ctor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run,
            [new CustomAttributeBuilder(ctor, ["4.5.6"])]);

        Assert.Equal("4.5.6", AppVersion.Read(assembly));
    }

    [Fact]
    public void Read_EmptyWithoutAssembly() {
        Assert.Equal("", AppVersion.Read(null));
    }
}
