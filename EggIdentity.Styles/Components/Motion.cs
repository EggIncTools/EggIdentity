using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Motion {
    internal const string Fast = "var(--motion-fast,120ms)";
    internal const string Base = "var(--motion-base,150ms)";
    internal const string Morph = "var(--motion-morph,220ms)";
    internal const string Standard = "var(--ease-standard,cubic-bezier(.2,0,0,1))";
    internal const string Pulse = "var(--ease-pulse,cubic-bezier(.4,0,.6,1))";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".pulse", $"[animation:pulse_var(--motion-pulse,2s)_{Pulse}_infinite]" },
        { ".pane-in", $"[animation:pane-in_{Morph}_{Standard}_backwards]" },
        { ".pane-in-reverse", $"[animation:pane-in-reverse_{Morph}_{Standard}_backwards]" },
        { ".progress", "relative w-full h-2 rounded-full overflow-hidden [background-color:var(--progress-track,var(--color-panel2))]" },
        { ".progress-fill", $"h-full rounded-full [width:var(--progress,0%)] [background-color:var(--progress-fill,var(--color-accent))] [transition:width_{Morph}_{Standard}]" },
    }.ToImmutableDictionary();
}
