using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Canvas {
    private const string Handle = "absolute z-[2] [width:var(--canvas-handle,8px)] [height:var(--canvas-handle,8px)] rounded-sm border [border-color:var(--color-accent)] [background-color:var(--color-panel)] [touch-action:none]";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".canvas-view", "relative w-full overflow-hidden [container-type:inline-size]" },
        { ".canvas-view-item", "absolute box-border overflow-hidden min-w-0 min-h-0" },
        { ".canvas-editor", "grid [grid-template-columns:auto_1fr_minmax(12rem,16rem)] [grid-template-rows:auto_auto] gap-3 min-h-0" },
        { ".canvas-palette", "flex flex-col gap-1 [grid-row:1/3]" },
        { ".canvas-palette-item", $"inline-flex items-center gap-1.5 h-7 px-2 rounded-md border cursor-grab text-xs [color:var(--color-muted)] [border-color:var(--color-border)] [touch-action:none] [transition:background-color_{Motion.Fast},color_{Motion.Fast}]" },
        { ".canvas-palette-item:hover", "[color:var(--color-fg)] [background-color:var(--color-panel2)]" },
        { ".canvas-stage", "relative overflow-auto rounded-md border [border-color:var(--color-border)] [background-color:var(--color-panel)]" },
        { ".canvas-stage:focus-visible", "[outline:none] [box-shadow:0_0_0_2px_var(--color-accent)]" },
        { ".canvas-grid", "relative [background-image:linear-gradient(to_right,var(--canvas-line,color-mix(in_srgb,var(--color-border)_60%,transparent))_1px,transparent_1px),linear-gradient(to_bottom,var(--canvas-line,color-mix(in_srgb,var(--color-border)_60%,transparent))_1px,transparent_1px)] [background-size:var(--canvas-unit)_var(--canvas-unit)]" },
        { ".canvas-item", $"absolute box-border overflow-hidden min-w-0 min-h-0 cursor-move [touch-action:none] [outline:1px_solid_transparent] [transition:outline-color_{Motion.Fast}]" },
        { ".canvas-item:hover", "[outline-color:color-mix(in_srgb,var(--color-accent)_50%,transparent)]" },
        { ".canvas-item.selected", "[outline-color:var(--color-accent)] z-[1]" },
        { ".canvas-item.dragging", "[opacity:0.85]" },
        { ".canvas-handle", Handle },
        { ".canvas-handle-n", "top-[-4px] left-1/2 [translate:-50%_0] cursor-ns-resize" },
        { ".canvas-handle-s", "bottom-[-4px] left-1/2 [translate:-50%_0] cursor-ns-resize" },
        { ".canvas-handle-e", "right-[-4px] top-1/2 [translate:0_-50%] cursor-ew-resize" },
        { ".canvas-handle-w", "left-[-4px] top-1/2 [translate:0_-50%] cursor-ew-resize" },
        { ".canvas-handle-ne", "top-[-4px] right-[-4px] cursor-nesw-resize" },
        { ".canvas-handle-nw", "top-[-4px] left-[-4px] cursor-nwse-resize" },
        { ".canvas-handle-se", "bottom-[-4px] right-[-4px] cursor-nwse-resize" },
        { ".canvas-handle-sw", "bottom-[-4px] left-[-4px] cursor-nesw-resize" },
        { ".canvas-inspector", "flex flex-col gap-2 min-w-0 rounded-md border [border-color:var(--color-border)] p-2 [grid-row:1/3]" },
        { ".canvas-preview", "flex flex-wrap gap-3 items-start [grid-column:2]" },
        { ".canvas-empty", "absolute inset-0 flex items-center justify-center pointer-events-none" },
    }.ToImmutableDictionary();
}
