using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class SegmentedToggles {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".segmented", "inline-flex border border-border rounded-lg overflow-hidden" },
        { ".segmented-opt", "bg-panel2 text-muted border-0 border-l border-border -ml-px first:ml-0 first:border-l-0 px-[0.9rem] py-[0.4rem] cursor-pointer text-[13px]" },
        { ".segmented-opt.active", "bg-accent text-bg font-semibold" },
        { ".segmented-opt:disabled", "opacity-45 cursor-not-allowed" },
    }.ToImmutableDictionary();
}
