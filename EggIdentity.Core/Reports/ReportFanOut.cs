namespace EggIdentity.Reports;

public static class ReportFanOut {
    public static IReadOnlyList<TRow> Rows<TEvent, TItem, TRow>(IEnumerable<TEvent> events, Func<TEvent, string> cell, Func<TEvent, IEnumerable<TItem>> items, Func<TItem, string> itemKey, Func<TEvent, string, TItem?, TRow> project) {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(itemKey);
        ArgumentNullException.ThrowIfNull(project);

        List<(TEvent Event, string Cell, List<TItem> Items)> eventData = [];
        foreach (var e in events) {
            List<TItem> eventItems = [.. items(e)];
            eventData.Add((e, cell(e), eventItems));
        }

        Dictionary<string, HashSet<string>> keysByCell = [];
        foreach (var data in eventData) {
            if (!keysByCell.TryGetValue(data.Cell, out var keys)) {
                keys = [];
                keysByCell[data.Cell] = keys;
            }

            foreach (var item in data.Items) {
                keys.Add(itemKey(item));
            }
        }

        Dictionary<string, List<string>> candidatesByCell = [];
        foreach (var (key, keys) in keysByCell) {
            List<string> sorted = [.. keys];
            sorted.Sort(StringComparer.Ordinal);
            candidatesByCell[key] = sorted;
        }

        List<TRow> rows = [];
        foreach (var data in eventData) {
            var candidates = candidatesByCell[data.Cell];
            foreach (var candidate in candidates) {
                List<TItem> matches = [];
                foreach (var item in data.Items) {
                    if (string.Equals(itemKey(item), candidate, StringComparison.Ordinal)) {
                        matches.Add(item);
                    }
                }

                if (matches.Count == 0) {
                    rows.Add(project(data.Event, candidate, default));
                } else {
                    foreach (var match in matches) {
                        rows.Add(project(data.Event, candidate, match));
                    }
                }
            }
        }

        return rows;
    }
}
