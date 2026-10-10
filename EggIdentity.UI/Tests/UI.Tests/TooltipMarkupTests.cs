using System.Text.RegularExpressions;

namespace EggIdentity.UI.Tests;

public partial class TooltipMarkupTests {
    [GeneratedRegex(@"<(/?)([^\s>/]+)")]
    private static partial Regex TagNames();

    private static string Source() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "UI", "Tooltip.razor"));

    [Fact]
    public void EveryElementNameIsALiteralHtmlTag() {
        var markup = Source().Split("@code", 2)[0];
        var names = TagNames().Matches(markup).Select(m => m.Groups[2].Value).Distinct().ToList();
        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.Matches("^[a-z][a-z0-9]*$", n));
    }

    [Fact]
    public void TriggerRendersAsSpanByDefaultAndDivOnRequest() {
        var markup = Source().Split("@code", 2)[0];
        Assert.Contains("<span class=\"@TriggerCss\"", markup);
        Assert.Contains("<div class=\"@TriggerCss\"", markup);
        Assert.Contains("Tag { get; set; } = \"span\"", Source());
    }
}
