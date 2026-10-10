using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Icons {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".icon", "inline-block shrink-0 align-[-0.125em] [width:var(--icon-size,1rem)] [height:var(--icon-size,1rem)] [&>svg]:block [&>svg]:w-full [&>svg]:h-full" },
        { ".icon-xs", "[--icon-size:0.75rem]" },
        { ".icon-sm", "[--icon-size:0.875rem]" },
        { ".icon-md", "[--icon-size:1.25rem]" },
        { ".icon-lg", "[--icon-size:1.5rem]" },

        { ".icon-btn", "inline-flex items-center justify-center p-1 rounded-md bg-transparent text-muted hover:text-fg cursor-pointer" },
        { ".icon-btn.active", "text-accent" },
        { ".icon-btn .icon", "[--icon-size:1.125rem]" },
        { ".icon-btn-sm .icon.icon", "[--icon-size:0.9375rem]" },
    }.ToImmutableDictionary();
}
