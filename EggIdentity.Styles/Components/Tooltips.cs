using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Tooltips {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".tooltip-floating", "whitespace-nowrap text-center text-fg fixed pointer-events-none p-[10px] rounded border [transform-origin:50%_100%] [z-index:var(--tooltip-z,9999)] [border-color:var(--tooltip-border,var(--color-border))] [background-color:var(--tooltip-bg,rgba(33,37,41,.95))] [transform:translateX(calc(-50%_+_var(--tt-shift-x,0px)))_translateY(var(--tt-ty,calc(-100%_-_10px)))_scale(var(--tt-scale,1))] [transition:opacity_.16s_ease,transform_.16s_ease]" },
        { ".tooltip-anchored", "absolute left-1/2 bottom-full [transform:translateX(calc(-50%_+_var(--tt-shift-x,0px)))_translateY(var(--tt-ty,-10px))_scale(var(--tt-scale,1))]" },
        { ".tooltip-fixed", "[left:var(--tt-left,50%)] [top:var(--tt-top,50%)]" },
        { ".tooltip-host", "relative inline-block" },
        { ".tooltip-toggle", "opacity-0 [--tt-scale:.85]" },
        { ".tooltip-host:hover .tooltip-toggle", "opacity-100 [--tt-scale:1]" },
        { ".tooltip-toggle.show", "opacity-100 [--tt-scale:1]" },
        { ".tooltip-floating.tooltip-below", "[--tt-ty:10px] [transform-origin:50%_0%]" },
        { ".tooltip-anchored.tooltip-below", "bottom-auto top-full" },
        { ".tooltip-floating::before", "content-[''] absolute top-full left-[calc(50%_+_var(--arrow-offset,_0px))] [transform:translateX(-50%)] border-[7px] border-transparent [border-top-color:var(--tooltip-border,var(--color-border))]" },
        { ".tooltip-floating::after", "content-[''] absolute top-full left-[calc(50%_+_var(--arrow-offset,_0px))] [transform:translateX(-50%)_translateY(-1px)] border-[6px] border-transparent [border-top-color:var(--tooltip-bg,rgba(33,37,41,.95))]" },
        { ".tooltip-floating.tooltip-below::before", "top-auto bottom-full [border-top-color:transparent] [border-bottom-color:var(--tooltip-border,var(--color-border))]" },
        { ".tooltip-floating.tooltip-below::after", "top-auto bottom-full [transform:translateX(-50%)_translateY(1px)] [border-top-color:transparent] [border-bottom-color:var(--tooltip-bg,rgba(33,37,41,.95))]" },
        { ".tooltip-floating.tooltip-err", "text-err [--tooltip-border:var(--color-err)] [--tooltip-bg:color-mix(in_srgb,var(--color-err)_18%,var(--color-bg))] [box-shadow:0_0_0_1px_var(--color-err),0_4px_14px_color-mix(in_srgb,var(--color-err)_35%,transparent)]" },
        { ".tooltip-floating.tooltip-err *", "text-err" },
    }.ToImmutableDictionary();
}
