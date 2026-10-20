using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class MiniGraphs {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".mini-spark", "block overflow-visible [color:var(--mini-color,var(--color-accent))]" },
        { ".mini-spark-line", "[fill:none] [stroke:currentColor] [stroke-width:1.5] [stroke-linejoin:round] [stroke-linecap:round] [vector-effect:non-scaling-stroke]" },
        { ".mini-spark-area", "[fill:currentColor] [opacity:0.15]" },
        { ".mini-stack", "flex w-full overflow-hidden [border-radius:var(--mini-stack-radius,9999px)] [height:var(--mini-stack-h,0.5rem)] [background-color:var(--mini-track,var(--color-panel2))]" },
        { ".mini-stack-seg", "h-full shrink-0 [background-color:var(--mini-color,var(--color-accent))]" },
        { ".mini-gauge", "block [color:var(--mini-color,var(--color-accent))]" },
        { ".mini-gauge-track", "[fill:none] [stroke:var(--mini-track,var(--color-panel2))] [stroke-linecap:round]" },
        { ".mini-gauge-fill", "[fill:none] [stroke:currentColor] [stroke-linecap:round]" },
    }.ToImmutableDictionary();
}
