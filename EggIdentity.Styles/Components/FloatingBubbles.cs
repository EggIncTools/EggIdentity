using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class FloatingBubbles {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".fab-bubble", "fixed right-4 bottom-[var(--fab-offset,1rem)] w-11 h-11 inline-flex items-center justify-center rounded-full bg-panel border border-border no-underline cursor-pointer [transition:color_.12s,border-color_.12s,transform_.12s]" },
        { ".fab-bubble:hover", "border-accent2 [transform:translateY(-2px)]" },
    }.ToImmutableDictionary();
}
