using System.Collections.Immutable;
using EggIdentity.Styles.Components;

namespace EggIdentity.Styles;

public static class ComponentClasses {
    public static readonly ImmutableDictionary<string, string> All = ImmutableDictionary.Create<string, string>(StableKeyComparer.Instance)
        .AddRange(Badges.Applies)
        .AddRange(Brand.Applies)
        .AddRange(Buttons.Applies)
        .AddRange(Panels.Applies)
        .AddRange(SegmentedToggles.Applies)
        .AddRange(Popovers.Applies)
        .AddRange(Modals.Applies)
        .AddRange(Motion.Applies)
        .AddRange(FloatingBubbles.Applies)
        .AddRange(FormControls.Applies)
        .AddRange(DataTables.Applies)
        .AddRange(Toasts.Applies)
        .AddRange(Consent.Applies)
        .AddRange(Tooltips.Applies)
        .AddRange(Prose.Applies)
        .AddRange(Workbench.Applies)
        .AddRange(Filters.Applies)
        .AddRange(Calendar.Applies)
        .AddRange(Icons.Applies)
        .AddRange(Rarity.Applies);
}
