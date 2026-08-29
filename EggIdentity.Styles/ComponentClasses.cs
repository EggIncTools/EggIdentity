using System.Collections.Immutable;
using EggIdentity.Styles.Components;

namespace EggIdentity.Styles;

public static class ComponentClasses {
    public static readonly ImmutableDictionary<string, string> All = ImmutableDictionary<string, string>.Empty
        .AddRange(Badges.Applies)
        .AddRange(Buttons.Applies)
        .AddRange(Panels.Applies)
        .AddRange(SegmentedToggles.Applies)
        .AddRange(Popovers.Applies)
        .AddRange(Modals.Applies)
        .AddRange(FloatingBubbles.Applies)
        .AddRange(FormControls.Applies)
        .AddRange(DataTables.Applies)
        .AddRange(Toasts.Applies)
        .AddRange(Tooltips.Applies)
        .AddRange(Prose.Applies);
}
