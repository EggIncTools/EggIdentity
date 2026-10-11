using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Consent {
    private const string Text = "[font-size:var(--suite-consent-text-size,13px)] [color:var(--suite-consent-fg,var(--color-fg))]";
    private const string Button = "[flex:var(--suite-consent-button-flex,1_1_auto)] min-w-0 overflow-hidden text-ellipsis whitespace-nowrap [padding-inline:var(--suite-consent-button-padding,calc(var(--spacing)*2))]";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".consent-banner", "fixed bottom-4 left-1/2 -translate-x-1/2 z-[1001] w-[calc(100vw_-_2rem)] [max-width:var(--suite-consent-max-width,640px)] [background:var(--suite-consent-bg,var(--color-panel2))] [border:var(--suite-consent-border,1px_solid_var(--color-border))] [border-radius:var(--suite-consent-radius,var(--radius-md))] [padding:var(--suite-consent-padding,calc(var(--spacing)*3)_calc(var(--spacing)*4))] flex flex-col [gap:var(--suite-consent-gap,calc(var(--spacing)*2))] [box-shadow:var(--suite-consent-shadow,0_6px_20px_rgba(0,0,0,.45))]" },
        { ".consent-text", $"{Text} m-0" },
        { ".consent-actions", "flex [flex-wrap:var(--suite-consent-wrap,nowrap)] [gap:var(--suite-consent-gap,calc(var(--spacing)*2))] [justify-content:var(--suite-consent-align,flex-end)]" },
        { ".consent-actions > .btn-primary", Button },
        { ".consent-actions > .btn-secondary", Button },
        { ".consent-options", "flex flex-wrap gap-4" },
        { ".consent-option", $"flex items-center gap-1.5 {Text} cursor-pointer" },
    }.ToImmutableDictionary();
}
