using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Prose {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".prose-legal", "text-sm text-muted leading-relaxed max-w-[80ch] flex flex-col gap-3" },
        { ".prose-legal h2", "text-base font-semibold text-fg mt-0 mb-2" },
        { ".prose-legal p", "text-sm text-muted leading-relaxed m-0" },
        { ".prose-legal ul", "text-sm text-muted leading-relaxed list-disc pl-5 flex flex-col gap-1.5" },
        { ".prose-legal a", "text-accent no-underline" },
        { ".prose-legal a:hover", "underline" },
    }.ToImmutableDictionary();
}
