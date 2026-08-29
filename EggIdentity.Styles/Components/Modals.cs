using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Modals {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".modal-backdrop", "fixed inset-0 z-[50] flex items-start justify-center overflow-y-auto p-4 [background:rgba(0,0,0,.6)] [backdrop-filter:blur(4px)]" },
        { ".modal-card", "w-full max-w-3xl bg-panel border border-border rounded-lg my-auto overflow-hidden shadow-[0_20px_25px_-5px_rgba(0,0,0,.4)]" },
        { ".modal-head", "flex items-center justify-between gap-3 px-5 py-3 border-b border-border" },
        { ".modal-title", "text-base font-semibold m-0 text-fg" },
        { ".modal-body", "px-5 py-4 overflow-x-auto" },
    }.ToImmutableDictionary();
}
