using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Popovers {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".popover-wrap", "relative inline-flex" },
        { ".popover", $"absolute right-0 z-[1100] w-[280px] bg-panel2 border border-border rounded-md p-3 overflow-auto opacity-0 pointer-events-none top-[calc(100%+8px)] shadow-[0_6px_18px_rgba(0,0,0,.4)] [transform-origin:top_right] [transform:scale(.96)_translateY(-6px)] [transition:opacity_{Motion.Base}_{Motion.Standard},transform_{Motion.Base}_{Motion.Standard}]" },
        { ".popover.open", "opacity-100 pointer-events-auto [transform:scale(1)_translateY(0)]" },
        { ".popover.popover-sm", "w-[240px]" },
        { ".popover.popover-lg", "w-[320px]" },
        { ".drag-panel", "fixed z-[1100] flex flex-col overflow-auto bg-panel2 border border-border rounded-md min-w-[160px] min-h-[80px] max-w-[100vw] max-h-[100vh] [resize:both] shadow-[0_6px_18px_rgba(0,0,0,.4)]" },
        { ".drag-handle", "cursor-move select-none [touch-action:none]" },
        { ".combo-wrap", "relative block" },
        { ".combo-anchor", "[anchor-name:var(--combo-anchor)]" },
        { ".popover.popover-combo", "fixed m-0 max-h-[240px] overflow-auto bg-panel0 p-0 py-1 [position-anchor:var(--combo-anchor)] [top:anchor(bottom)] [left:anchor(left)] [right:auto] [bottom:auto] [width:anchor-size(width)] [min-width:max-content] [position-try-fallbacks:flip-block] opacity-100 pointer-events-auto [transform:none] [box-shadow:0_8px_24px_rgba(0,0,0,.55)]" },
        { ".popover-combo-opt", "block w-full text-left px-2.5 py-1.5 text-xs text-fg bg-transparent border-0 cursor-pointer truncate [font-family:inherit]" },
        { ".popover-combo-opt:hover", "bg-accent text-bg" },
        { ".popover-combo-opt.combo-highlighted", "bg-accent text-bg" },
    }.ToImmutableDictionary();
}
