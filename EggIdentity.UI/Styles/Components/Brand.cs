using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Brand {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".brand-icon", "block [image-rendering:pixelated]" },
        { ".brand-art", "block max-w-full h-auto" },
    }.ToImmutableDictionary();
}
