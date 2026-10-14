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
    public const string Warn = "--color-warn";
    public const string Panel0 = "--color-panel0";
    public const string HeatLow = "--color-heat-low";
    public const string HeatHigh = "--color-heat-high";
    public const string Chart1 = "--color-chart-1";
    public const string Chart2 = "--color-chart-2";
    public const string Chart3 = "--color-chart-3";
    public const string Chart4 = "--color-chart-4";
    public const string Chart5 = "--color-chart-5";
    public const string Chart6 = "--color-chart-6";
    public const string Chart7 = "--color-chart-7";
    public const string Chart8 = "--color-chart-8";

    public static readonly ImmutableArray<string> Required = [
        Bg, Panel, Panel2, Fg, Muted, Accent, Accent2, Ok, Err, Border, Warn,
    ];

    public static readonly ImmutableArray<string> Optional = [
        Panel0, HeatLow, HeatHigh, Chart1, Chart2, Chart3, Chart4, Chart5, Chart6, Chart7, Chart8,
    ];
}
