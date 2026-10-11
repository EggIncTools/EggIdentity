using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Tooltips {
    private const string Shown = "opacity-100 [--tt-scale:1]";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".tooltip-floating", $"whitespace-nowrap text-center text-fg text-[0.75rem] [line-height:calc(1_/_0.75)] font-normal [font-style:normal] [text-transform:none] [letter-spacing:normal] fixed pointer-events-none p-[10px] rounded border [transform-origin:50%_100%] [z-index:var(--tooltip-z,9999)] [border-color:var(--tooltip-border,var(--color-border))] [background-color:var(--tooltip-bg,rgba(33,37,41,.95))] [transform:translateX(calc(-50%_+_var(--tt-shift-x,0px)))_translateY(var(--tt-ty,calc(-100%_-_10px)))_scale(var(--tt-scale,1))] [transition:opacity_{Motion.Base}_{Motion.Standard},transform_{Motion.Base}_{Motion.Standard}]" },
        { ".tooltip-trigger", "[anchor-name:var(--tt-anchor)]" },
        { "span.tooltip-trigger", "inline-block [vertical-align:baseline] [max-width:100%]" },
        { ".tooltip-point", "absolute w-0 h-0 pointer-events-none [anchor-name:var(--tt-anchor)]" },
        { ".tooltip-point > .tooltip-floating", Shown },
        { ".tooltip-floating.tooltip-fixed", "[position-anchor:var(--tt-anchor)] [position-area:top_span-all] [justify-self:anchor-center] [margin:0_0_10px] [position-try-fallbacks:flip-block,--tt-top-right,--tt-top-left,--tt-bottom-right,--tt-bottom-left] [transform:scale(var(--tt-scale,1))] [container-type:anchored]" },
        { ".tooltip-trigger:hover > .tooltip-toggle", Shown },
        { ".tooltip-trigger:focus-within > .tooltip-toggle", Shown },
        { ".tooltip-trigger.tt-dismissed > .tooltip-toggle.tooltip-floating", "opacity-0" },
        { ".tooltip-anchored","absolute left-1/2 bottom-full [transform:translateX(calc(-50%_+_var(--tt-shift-x,0px)))_translateY(var(--tt-ty,-10px))_scale(var(--tt-scale,1))]" },
        { ".tooltip-floating.tooltip-anchored", "absolute [transform:translateX(calc(-50%_+_var(--tt-shift-x,0px)))_translateY(var(--tt-ty,-10px))_scale(var(--tt-scale,1))]" },
        { ".tooltip-host", "relative inline-block" },
        { ".tooltip-toggle", "opacity-0 [--tt-scale:.85]" },
        { ".tooltip-host:hover .tooltip-toggle", Shown },
        { ".tooltip-floating.tooltip-below", "[--tt-ty:10px] [transform-origin:50%_0%]" },
        { ".tooltip-anchored.tooltip-below", "bottom-auto top-full" },
        { ".tooltip-floating::before", "content-[''] absolute top-full left-[calc(50%_+_var(--arrow-offset,_0px))] [transform:translateX(-50%)] border-[7px] border-transparent [border-top-color:var(--tooltip-border,var(--color-border))]" },
        { ".tooltip-floating::after", "content-[''] absolute top-full left-[calc(50%_+_var(--arrow-offset,_0px))] [transform:translateX(-50%)_translateY(-1px)] border-[6px] border-transparent [border-top-color:var(--tooltip-bg,rgba(33,37,41,.95))]" },
        { ".tooltip-floating.tooltip-below::before", "top-auto bottom-full [border-top-color:transparent] [border-bottom-color:var(--tooltip-border,var(--color-border))]" },
        { ".tooltip-floating.tooltip-below::after", "top-auto bottom-full [transform:translateX(-50%)_translateY(1px)] [border-top-color:transparent] [border-bottom-color:var(--tooltip-bg,rgba(33,37,41,.95))]" },
        { ".tooltip-floating.tooltip-err", "text-err [--tooltip-border:var(--color-err)] [--tooltip-bg:color-mix(in_srgb,var(--color-err)_18%,var(--color-bg))] [box-shadow:0_0_0_1px_var(--color-err),0_4px_14px_color-mix(in_srgb,var(--color-err)_35%,transparent)]" },
        { ".tooltip-floating.tooltip-err *", "text-err" },
        { ".tooltip-line", "block text-[0.75rem] [line-height:calc(1_/_0.75)] text-muted" },
        { ".tooltip-line-head", "font-medium text-fg" },
    }.ToImmutableDictionary();
}
