namespace EggIdentity.Styles.Tests;

public class CssBuildTextTests {
    [Fact]
    public void Scan_ExtractsTokensAboveMinLength() {
        var dir = Directory.CreateTempSubdirectory();
        try {
            var path = Path.Combine(dir.FullName, "sample.razor");
            File.WriteAllText(path, "<div class=\"flex items-center px-3\">x</div>");

            var tokens = CssBuildText.Scan([path]);

            Assert.Contains("flex", tokens);
            Assert.Contains("items-center", tokens);
            Assert.Contains("px-3", tokens);
        } finally {
            dir.Delete(true);
        }
    }

    [Fact]
    public void FindSemicolonInsideApplyBracket_ReturnsNullWhenClean() {
        var result = CssBuildText.FindSemicolonInsideApplyBracket(".foo { @apply flex px-3; }");

        Assert.Null(result);
    }

    [Fact]
    public void FindSemicolonInsideApplyBracket_FindsSemicolonInsideBracketValue() {
        var css = ".foo {\n  @apply [background:red;blue];\n}";

        var result = CssBuildText.FindSemicolonInsideApplyBracket(css);

        Assert.NotNull(result);
        Assert.Equal(2, result.Value.Line);
    }

    [Fact]
    public void StripApplyDirectives_RemovesApplyRuleEntirely() {
        var css = ".foo { @apply flex px-3; color: red; }";

        var stripped = CssBuildText.StripApplyDirectives(css);

        Assert.DoesNotContain("@apply", stripped);
        Assert.Contains("color: red;", stripped);
    }

    [Fact]
    public void UnwrapLayersAndSpliceRaw_SplicesAfterDefaultComponentsLayer() {
        var compiled = "@layer base { .a{color:red} } @layer components { .b{color:blue} } @layer utilities { .c{color:green} }";

        var result = CssBuildText.UnwrapLayersAndSpliceRaw(compiled, ".raw{color:pink}");

        var componentsIdx = result.IndexOf(".b{color:blue}", StringComparison.Ordinal);
        var rawIdx = result.IndexOf(".raw{color:pink}", StringComparison.Ordinal);
        var utilitiesIdx = result.IndexOf(".c{color:green}", StringComparison.Ordinal);
        Assert.True(componentsIdx < rawIdx && rawIdx < utilitiesIdx);
    }

    [Fact]
    public void UnwrapLayersAndSpliceRaw_HonorsCustomSpliceLayer() {
        var compiled = "@layer base { .a{color:red} } @layer utilities { .c{color:green} }";

        var result = CssBuildText.UnwrapLayersAndSpliceRaw(compiled, ".raw{color:pink}", "base");

        var baseIdx = result.IndexOf(".a{color:red}", StringComparison.Ordinal);
        var rawIdx = result.IndexOf(".raw{color:pink}", StringComparison.Ordinal);
        var utilitiesIdx = result.IndexOf(".c{color:green}", StringComparison.Ordinal);
        Assert.True(baseIdx < rawIdx && rawIdx < utilitiesIdx);
    }

    [Fact]
    public void UnwrapLayersAndSpliceRaw_AppendsAtEndWhenSpliceLayerMissing() {
        var compiled = "@layer base { .a{color:red} } @layer utilities { .c{color:green} }";

        var result = CssBuildText.UnwrapLayersAndSpliceRaw(compiled, ".raw{color:pink}");

        var utilitiesIdx = result.IndexOf(".c{color:green}", StringComparison.Ordinal);
        var rawIdx = result.IndexOf(".raw{color:pink}", StringComparison.Ordinal);
        Assert.True(utilitiesIdx < rawIdx);
    }

    [Fact]
    public void UnwrapLayersAndSpliceRaw_StripsBareLayerStatementRegardlessOfPosition() {
        var compiled = "@layer base, components, utilities; @layer components { .b{color:blue} }";

        var result = CssBuildText.UnwrapLayersAndSpliceRaw(compiled, "");

        Assert.DoesNotContain("@layer base, components, utilities;", result);
    }

    [Fact]
    public void UnwrapLayersAndSpliceRaw_PreservesTextBetweenLayerBlocksInPlace() {
        var compiled = "@layer base { .a{color:red} } @property --x { syntax: \"*\"; } @layer properties { .p{color:teal} }";

        var result = CssBuildText.UnwrapLayersAndSpliceRaw(compiled, "");

        var propertyIdx = result.IndexOf("@property --x", StringComparison.Ordinal);
        var propertiesLayerIdx = result.IndexOf(".p{color:teal}", StringComparison.Ordinal);
        Assert.True(propertyIdx >= 0 && propertyIdx < propertiesLayerIdx);
    }
}
