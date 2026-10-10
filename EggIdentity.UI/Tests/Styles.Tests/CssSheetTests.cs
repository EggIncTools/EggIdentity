using EggIdentity.Styles.Css;

namespace EggIdentity.Styles.Tests;

public class CssSheetTests {
    private const string Sample = """
        @layer a, b;
        :root { --x: 1; --color-fg: #fff; }
        .a, .b { color: var(--color-fg, #abc); transition: width 1s, color .1s; }
        @media (prefers-reduced-motion: reduce) {
          .a, .b { transition: none; }
        }
        @keyframes k { from { opacity: 0; } }
        @property --tw-x { syntax: "*"; inherits: false; initial-value: 0 0 #0000; }
        .c { background: rgba(0,0,0,.4); border-color: color-mix(in oklab, var(--color-fg) 50%, transparent); }
        """;

    private static readonly CssSheet Sheet = CssSheet.Parse(Sample);

    [Fact]
    public void ParsesStatementsRulesAndContainers() {
        Assert.Equal(["@layer a, b"], Sheet.Statements);
        Assert.Contains(".a, .b", Sheet.Selectors);
        Assert.Contains("@property --tw-x", Sheet.Selectors);
        Assert.DoesNotContain("from", Sheet.Selectors);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", Sheet.Containers);
        Assert.Contains("@keyframes k", Sheet.Containers);
    }

    [Fact]
    public void IndexerReturnsLastDeclarationAndCollapsesWhitespace() {
        Assert.Equal("var(--color-fg, #abc)", Sheet.Rule(".a, .b")?["color"]);
        Assert.Equal("none", Sheet.Within("@media").Single()["transition"]);
        Assert.Equal("color-mix(in oklab, var(--color-fg) 50%, transparent)", Sheet.Rule(".c")?["border-color"]);
    }

    [Fact]
    public void TracksDefinedAndReadCustomProperties() {
        Assert.Equal(["--color-fg", "--x"], Sheet.DefinedProperties.Order());
        Assert.Equal(["--color-fg"], Sheet.ReadProperties.Order());
    }

    [Fact]
    public void LiteralsExcludeVarFallbacks() {
        Assert.Equal(["#0000", "#fff", "rgba(0,0,0,.4)"], Sheet.Literals.Order());
    }

    [Fact]
    public void StripsComments() {
        var sheet = CssSheet.Parse(".a { color: red; } " + "/" + "* .ghost { color: blue; } *" + "/");
        Assert.Equal([".a"], sheet.Selectors);
    }
}
