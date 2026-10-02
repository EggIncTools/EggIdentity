using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Menus {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".menu-wrap", "relative inline-flex" },
        { ".menu", $"absolute right-0 z-[1100] min-w-[12rem] [max-height:var(--menu-max-h,none)] overflow-auto flex flex-col py-1 bg-panel2 border border-border rounded-md opacity-0 pointer-events-none invisible top-[calc(100%+6px)] shadow-[0_6px_18px_rgba(0,0,0,.4)] [transform-origin:top_right] [transform:scale(var(--menu-enter-scale,.96))_translateY(var(--menu-enter-shift,-6px))] [transition:opacity_{Motion.Base}_{Motion.Standard},transform_{Motion.Base}_{Motion.Standard},visibility_0s_linear_{Motion.Base}]" },
        { ".menu.menu-start", "right-auto left-0 [transform-origin:top_left]" },
        { ".menu.open", $"opacity-100 pointer-events-auto visible [transform:none] [transition:opacity_{Motion.Base}_{Motion.Standard},transform_{Motion.Base}_{Motion.Standard}]" },
        { ".menu-item", "flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm text-fg bg-transparent border-0 cursor-pointer no-underline whitespace-nowrap [font:inherit]" },
        { ".menu-item:hover", "bg-panel" },
        { ".menu-item:disabled", "opacity-40 cursor-not-allowed" },
        { ".menu-sep", "my-1 h-px bg-border" },
    }.ToImmutableDictionary();
}
