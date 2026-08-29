using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Buttons {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".btn-primary", "inline-flex items-center justify-center gap-1 bg-accent text-bg border-0 rounded-md px-[1.4rem] py-2 font-semibold text-sm cursor-pointer no-underline" },
        { ".btn-primary:disabled", "cursor-not-allowed opacity-40" },
        { ".btn-primary:hover:not(:disabled)", "[filter:brightness(1.07)]" },

        { ".btn-secondary", "inline-flex items-center justify-center gap-1 bg-panel2 text-fg border border-border rounded-md px-4 py-[0.45rem] font-semibold cursor-pointer no-underline" },
        { ".btn-secondary:hover", "[filter:brightness(1.07)]" },

        { ".btn-accent", "inline-flex items-center justify-center gap-1 bg-accent2 text-bg border-0 rounded-md px-4 py-[0.45rem] font-semibold cursor-pointer" },
        { ".btn-accent:disabled", "cursor-not-allowed opacity-40" },
        { ".btn-accent:hover:not(:disabled)", "[filter:brightness(1.07)]" },

        { ".btn-mini", "inline-flex items-center gap-1 h-7 box-border bg-panel2 border border-border text-fg rounded px-2.5 text-xs cursor-pointer leading-none" },
        { ".btn-mini:hover", "border-accent2" },

        { ".btn-danger.btn-danger", "border-err text-err" },
        { ".btn-danger.btn-danger:hover", "bg-err text-bg border-err" },

        { ".icon-btn", "bg-panel2 border border-border rounded text-fg cursor-pointer leading-none w-8 h-[30px] inline-flex items-center justify-center p-0 no-underline" },
        { ".icon-btn:hover", "border-accent2" },
        { ".icon-btn.active", "border-accent text-accent" },
    }.ToImmutableDictionary();
}
