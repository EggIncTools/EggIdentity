using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Badges {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".badge", "inline-flex items-center gap-1 text-xs font-semibold px-2 py-0.5 rounded-[999px] border border-border leading-none cursor-default" },
        { ".badge:hover", "[filter:brightness(1.15)]" },
        { ".badge.badge-ok", "text-ok border-ok" },
        { ".badge.badge-err", "text-err border-err" },
        { ".badge.badge-accent", "text-accent border-accent" },
        { ".badge.badge-accent2", "text-accent2 border-accent2" },
        { ".badge.badge-muted", "text-muted border-border" },
    }.ToImmutableDictionary();
}
