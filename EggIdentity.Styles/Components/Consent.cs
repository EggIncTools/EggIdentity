using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Consent {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".consent-banner", "fixed bottom-4 left-1/2 -translate-x-1/2 z-[1001] w-[calc(100vw_-_2rem)] max-w-[640px] bg-panel2 border border-border rounded-md px-4 py-3 flex flex-col gap-2 [box-shadow:0_6px_20px_rgba(0,0,0,.45)]" },
        { ".consent-text", "text-[13px] text-fg m-0" },
        { ".consent-actions", "flex flex-wrap gap-2 justify-end" },
        { ".consent-options", "flex flex-wrap gap-4" },
        { ".consent-option", "flex items-center gap-1.5 text-[13px] text-fg cursor-pointer" },
    }.ToImmutableDictionary();
}
