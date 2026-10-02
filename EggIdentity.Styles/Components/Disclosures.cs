using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Disclosures {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".disclosure", "flex flex-col" },
        { ".disclosure-head", "flex items-center gap-2 min-w-0" },
        { ".disclosure-toggle", "flex flex-1 items-center gap-1.5 min-w-0 p-0 bg-transparent border-0 text-fg text-left cursor-pointer [font:inherit]" },
        { ".disclosure-label", "truncate" },
        { ".disclosure-tools", "ml-auto flex items-center gap-1" },
        { ".disclosure-body", "flex flex-col" },
    }.ToImmutableDictionary();
}
