using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class FormControls {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".drop-zone", $"border border-dashed border-border rounded-md [transition:border-color_{Motion.Fast}_{Motion.Standard},background-color_{Motion.Fast}_{Motion.Standard}]" },
        { ".drop-zone.drop-zone-active", "border-accent [background-color:color-mix(in_oklab,var(--color-accent)_12%,transparent)]" },
        { ".form-input", "bg-panel2 border border-border rounded text-fg px-1.5 py-1 text-xs" },
        { ".form-input:focus", "[outline:none] border-accent2" },
        { ".form-select", "bg-panel2 border border-border rounded text-fg px-1.5 py-1 text-xs [appearance:none]" },
        { ".form-select:focus", "[outline:none] border-accent2" },
        { ".form-check", "flex items-center gap-2 text-[13px] text-fg cursor-pointer" },
    }.ToImmutableDictionary();
}
