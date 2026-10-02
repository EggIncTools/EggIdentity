namespace EggIdentity.Tools.Tests;

public class SharedStylesheetTests {
    private const string Regenerate = "run: dotnet run --project EggIdentity.Tools -- css emit";

    private static string Committed(string file) =>
        File.ReadAllText(Path.Combine(SharedStylesheet.RepoRoot(), "EggIdentity.Styles", "wwwroot", file)).Replace("\r\n", "\n");

    [Fact]
    public void Shared_CommittedMatchesRender() {
        Assert.True(SharedStylesheet.RenderShared() == Committed(SharedStylesheet.SharedFile), "shared.css is stale; " + Regenerate);
    }

    [Fact]
    public void Preflight_CommittedMatchesRender() {
        Assert.True(SharedStylesheet.RenderPreflight() == Committed(SharedStylesheet.PreflightFile), "preflight.css is stale; " + Regenerate);
    }

    [Fact]
    public void Preflight_IsNotEmpty() {
        Assert.Contains("box-sizing: border-box", SharedStylesheet.RenderPreflight());
    }

    [Fact]
    public void Render_IsDeterministic() {
        Assert.Equal(SharedStylesheet.RenderShared(), SharedStylesheet.RenderShared());
    }

    [Fact]
    public void Unwrap_RemovesLayerStatementsAndWrappers() {
        var compiled = "@layer base, components;\n@layer base {\n.a { color: red; }\n}\n@property --x { syntax: \"*\"; }\n@layer components {\n.b { color: blue; }\n}\n";

        var result = CssLayers.Unwrap(compiled);

        Assert.DoesNotContain("@layer", result);
        Assert.Contains(".a { color: red; }", result);
        Assert.Contains("@property --x", result);
        Assert.Contains(".b { color: blue; }", result);
    }

    [Fact]
    public void ExtractLayer_ReturnsOnlyThatLayerBody() {
        var compiled = "@layer theme {\n:root { --a: 1; }\n}\n@layer base {\n* { margin: 0; }\n}\n";

        var result = CssLayers.ExtractLayer(compiled, "base");

        Assert.Contains("* { margin: 0; }", result);
        Assert.DoesNotContain("--a", result);
        Assert.Equal("", CssLayers.ExtractLayer(compiled, "missing"));
    }
}
