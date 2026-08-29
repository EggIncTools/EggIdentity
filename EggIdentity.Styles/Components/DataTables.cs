using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class DataTables {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".data-table", "w-full text-xs text-muted tabular-nums border border-border rounded-md overflow-hidden [border-collapse:collapse]" },
        { ".data-table thead th", "sticky top-0 z-10 bg-panel0 text-left py-1 px-2 whitespace-nowrap" },
        { ".data-table td", "py-1 px-2 whitespace-nowrap" },
        { ".data-table tbody tr", "border-t border-border" },
        { ".data-table tbody tr:hover", "bg-panel2" },
        { ".stat-tile", "bg-panel2 border border-border rounded-md p-2 text-center" },
        { ".stat-tile-label", "text-xs text-muted" },
        { ".stat-tile-value", "text-lg font-bold text-fg tabular-nums" },
    }.ToImmutableDictionary();
}
