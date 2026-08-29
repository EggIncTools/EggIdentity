using System.Collections.Immutable;

namespace EggIdentity.Styles;

public static class ComponentTokens {
    public const string Bg = "--color-bg";
    public const string Panel = "--color-panel";
    public const string Panel2 = "--color-panel2";
    public const string Fg = "--color-fg";
    public const string Muted = "--color-muted";
    public const string Accent = "--color-accent";
    public const string Accent2 = "--color-accent2";
    public const string Ok = "--color-ok";
    public const string Err = "--color-err";
    public const string Border = "--color-border";
    public const string Panel0 = "--color-panel0";

    public static readonly ImmutableArray<string> Required = [
        Bg, Panel, Panel2, Fg, Muted, Accent, Accent2, Ok, Err, Border,
    ];

    public static readonly ImmutableArray<string> Optional = [Panel0];
}
