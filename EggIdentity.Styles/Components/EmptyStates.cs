using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class EmptyStates {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".empty", "flex flex-col items-center justify-center gap-2 p-6 text-center text-muted" },
        { ".empty.empty-fill", "flex-1 min-h-0" },
        { ".empty.empty-tall", "min-h-[16rem]" },
        { ".empty-icon", "[--icon-size:2rem] opacity-60" },
        { ".empty-title", "text-sm font-semibold text-fg" },
        { ".empty-hint", "text-xs max-w-[28rem]" },
        { ".empty-actions", "flex flex-wrap justify-center gap-2 mt-1" },
    }.ToImmutableDictionary();
}
