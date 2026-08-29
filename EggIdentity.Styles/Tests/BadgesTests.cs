using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class BadgesTests {
    private static Theme BuildTheme() =>
        Theme.CreateWithDefaults([])
            .Add("--color-bg", "#1b1b1f")
            .Add("--color-panel", "#25252b")
            .Add("--color-panel2", "#2e2e36")
            .Add("--color-fg", "#e7e7ea")
            .Add("--color-muted", "#9a9aa5")
            .Add("--color-accent", "#ef7559")
            .Add("--color-accent2", "#5aa9e6")
            .Add("--color-ok", "#5ec27e")
            .Add("--color-err", "#e0685f")
            .Add("--color-border", "#3a3a44");

    private static CssFramework BuildFramework() => new(new CssFrameworkSettings {
        Theme = BuildTheme(),
        IncludePreflight = false,
        Applies = Components.Badges.Applies,
    });

    [Fact]
    public void Badge_CompilesBaseSelector() {
        var css = BuildFramework().Process("badge");
        var flat = css.Replace(" ", "").Replace("\n", "");
        Assert.Contains(".badge{", flat);
        Assert.Contains(".badge:hover{", flat);
    }

    [Fact]
    public void BadgeModifiers_ResolveToRoleColors() {
        var css = BuildFramework().Process("badge badge-ok badge-err badge-accent badge-accent2 badge-muted");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".badge.badge-ok{", flat);
        Assert.Contains(".badge.badge-err{", flat);
        Assert.Contains(".badge.badge-accent{", flat);
        Assert.Contains(".badge.badge-accent2{", flat);
        Assert.Contains(".badge.badge-muted{", flat);
        Assert.Contains("#5ec27e", css);
        Assert.Contains("#e0685f", css);
        Assert.Contains("#ef7559", css);
        Assert.Contains("#5aa9e6", css);
        Assert.Contains("#9a9aa5", css);
    }
}
