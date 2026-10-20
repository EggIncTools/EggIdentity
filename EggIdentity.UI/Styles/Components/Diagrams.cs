using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Diagrams {
    private const string Fast = $"{Motion.Fast}_{Motion.Standard}";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".diagram", "relative w-full [overflow:auto] rounded-md border border-border bg-panel" },
        { ".diagram-tools", "sticky top-0 z-10 flex items-center gap-1 [padding:var(--diagram-tools-padding,0.35rem)] border-b border-border [background-color:var(--diagram-tools-bg,var(--color-panel))]" },
        { ".diagram-tool", $"inline-flex items-center justify-center cursor-pointer px-2 [height:var(--diagram-tool-size,1.8rem)] [min-width:var(--diagram-tool-size,1.8rem)] rounded-md text-xs [color:var(--color-muted)] [border:1px_solid_var(--color-border)] [background-color:transparent] [transition:background-color_{Fast},color_{Fast}]" },
        { ".diagram-tool:hover", "[color:var(--color-accent2)] [background-color:var(--diagram-tool-hover-bg,var(--color-panel2))]" },
        { ".diagram-tool:disabled", "[opacity:var(--diagram-tool-disabled-opacity,0.45)] cursor-not-allowed" },
        { ".diagram-stage", "relative [overflow:visible]" },
        { ".diagram-canvas", "relative [transform-origin:top_left]" },
        { ".diagram-edges", "absolute top-0 left-0 [fill:none] [pointer-events:none]" },
        { ".diagram-edge-label", "absolute px-1.5 rounded-full border border-border text-xs [pointer-events:none] [background-color:var(--diagram-edge-label-bg,var(--color-panel))] [color:var(--diagram-edge-label-fg,var(--color-muted))]" },
        { ".diagram-node", $"absolute flex items-center justify-center text-left cursor-pointer rounded-md border [border-color:var(--diagram-node-border,var(--color-border))] [background-color:var(--diagram-node-bg,var(--color-panel))] [transition:border-color_{Fast},box-shadow_{Fast}]" },
        { ".diagram-node:hover", "[border-color:var(--diagram-node-hover-border,var(--color-accent2))]" },
        { ".diagram-node:focus-visible", "[outline:none] [box-shadow:var(--diagram-focus-ring,0_0_0_2px_var(--color-accent))]" },
        { ".diagram-node-selected", "[border-color:var(--diagram-node-selected-border,var(--color-accent))] [box-shadow:var(--diagram-node-selected-ring,0_0_0_1px_var(--color-accent))]" },
        { ".diagram-node-toggle", $"absolute inline-flex items-center justify-center cursor-pointer rounded-full [width:var(--diagram-toggle-size,1.1rem)] [height:var(--diagram-toggle-size,1.1rem)] [font-size:var(--diagram-toggle-font-size,0.65rem)] [color:var(--diagram-toggle-fg,var(--color-muted))] [border:1px_solid_var(--color-border)] [background-color:var(--diagram-toggle-bg,var(--color-panel))] [transition:background-color_{Fast},color_{Fast}]" },
        { ".diagram-node-toggle:hover", "[color:var(--diagram-toggle-hover-fg,var(--color-accent2))] [background-color:var(--diagram-toggle-hover-bg,var(--color-panel2))]" },
    }.ToImmutableDictionary();
}
