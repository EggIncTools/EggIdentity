using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class StatusNotes {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".status-note", "flex items-start gap-2 px-3 py-2 rounded-md border border-border border-l-[3px] bg-panel2 text-sm text-fg [--status-note-tone:var(--color-accent2)] [border-left-color:var(--status-note-tone)]" },
        { ".status-note.status-note-ok", "[--status-note-tone:var(--color-ok)]" },
        { ".status-note.status-note-warn", "[--status-note-tone:var(--color-warn)]" },
        { ".status-note.status-note-error", "[--status-note-tone:var(--color-err)]" },
        { ".status-note.status-note-busy", "[--status-note-tone:var(--color-muted)]" },
        { ".status-note-icon", "mt-[0.125rem] [color:var(--status-note-tone)]" },
        { ".status-note-text", "flex-1 min-w-0 break-words" },
        { ".status-note-actions", "flex items-center gap-1.5 shrink-0" },
    }.ToImmutableDictionary();
}
