using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Panels {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".panel", "bg-panel border border-border rounded-lg p-3 flex flex-col overflow-hidden" },
        { ".panel-head", "flex items-center justify-between gap-2 mb-2" },
        { ".panel-title", "text-[13px] uppercase text-muted mt-1 mb-2.5" },
    }.ToImmutableDictionary();
}
