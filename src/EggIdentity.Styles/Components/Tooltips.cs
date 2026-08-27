using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Tooltips {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".tooltip-floating", "whitespace-nowrap text-center text-fg fixed z-[9999] border border-border p-[10px] rounded [background-color:rgba(33,37,41,.95)] pointer-events-none [transform:translateX(-50%)_translateY(calc(-100%_-_10px))]" },
        { ".tooltip-anchored", "absolute left-1/2 bottom-full [transform:translateX(-50%)_translateY(-10px)]" },
        { ".tooltip-floating::before", "content-[''] absolute top-full left-[calc(50%_+_var(--arrow-offset,_0px))] [transform:translateX(-50%)] border-[7px] border-transparent [border-top-color:var(--color-border)]" },
        { ".tooltip-floating::after", "content-[''] absolute top-full left-[calc(50%_+_var(--arrow-offset,_0px))] [transform:translateX(-50%)_translateY(-1px)] border-[6px] border-transparent [border-top-color:rgba(33,37,41,.95)]" },
    }.ToImmutableDictionary();
}
