using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Icons {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".icon", "inline-block w-4 h-4 shrink-0 align-[-0.125em] [&>svg]:block [&>svg]:w-full [&>svg]:h-full" },
        { ".icon-xs", "w-3 h-3" },
        { ".icon-sm", "w-3.5 h-3.5" },
        { ".icon-md", "w-5 h-5" },
        { ".icon-lg", "w-6 h-6" },

        { ".icon-btn", "inline-flex items-center justify-center p-1 rounded-md bg-transparent text-muted hover:text-fg cursor-pointer" },
        { ".icon-btn.active", "text-accent" },
        { ".icon-btn .icon", "w-[1.125rem] h-[1.125rem]" },
        { ".icon-btn-sm .icon", "w-[0.9375rem] h-[0.9375rem]" },
    }.ToImmutableDictionary();
}
