using EggIdentity.Reports;

namespace EggIdentity.UI.Reports;

public static class ReportGridLayout {
    public const int GridCols = 8;
    public const int GridRows = 8;

    public static (int W, int H) ClampDims(int w, int h) =>
        (Math.Clamp(w, 1, GridCols), Math.Clamp(h, 1, GridRows));

    public static void MarkOccupied(HashSet<(int, int)> occupied, int x, int y, int w, int h) {
        ArgumentNullException.ThrowIfNull(occupied);
        for (var yy = y; yy < y + h; yy++)
            for (var xx = x; xx < x + w; xx++) occupied.Add((xx, yy));
    }

    public static bool CellsFit(HashSet<(int, int)> occupied, int x, int y, int w, int h) {
        ArgumentNullException.ThrowIfNull(occupied);
        for (var yy = y; yy < y + h; yy++) {
            for (var xx = x; xx < x + w; xx++)
                if (occupied.Contains((xx, yy))) return false;
        }
        return true;
    }

    public static (int X, int Y) FindPlacement(HashSet<(int, int)> occupied, int w, int h, int x, int y) {
        ArgumentNullException.ThrowIfNull(occupied);
        var cx = x;
        var cy = y;
        while (true) {
            if (cx + w - 1 > GridCols) {
                cx = 1;
                cy++;
            }
            if (CellsFit(occupied, cx, cy, w, h)) return (cx, cy);
            cx++;
        }
    }

    public static IReadOnlyList<ReportPlacement> Pack(IReadOnlyList<ReportPlacement> placements) {
        ArgumentNullException.ThrowIfNull(placements);
        var ordered = placements.OrderBy(p => p.Order).ToList();
        HashSet<(int, int)> occupied = [];
        var x = 1;
        var y = 1;
        var result = new List<ReportPlacement>(ordered.Count);
        foreach (var p in ordered) {
            var (w, h) = ClampDims(p.W, p.H);
            (x, y) = FindPlacement(occupied, w, h, x, y);
            result.Add(p with { X = x, Y = y, W = w, H = h });
            MarkOccupied(occupied, x, y, w, h);
            x += w;
        }
        return result;
    }

    public static IReadOnlyList<ReportPlacement> Resize(IReadOnlyList<ReportPlacement> placements, string reportId, int w, int h) {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(reportId);
        var updated = placements
            .Select(p => string.Equals(p.ReportId, reportId, StringComparison.Ordinal) ? p with { W = w, H = h } : p)
            .ToList();
        return Pack(updated);
    }

    public static IReadOnlyList<ReportPlacement> Swap(IReadOnlyList<ReportPlacement> placements, string fromId, string toId) {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(fromId);
        ArgumentNullException.ThrowIfNull(toId);
        if (string.Equals(fromId, toId, StringComparison.Ordinal)) return placements;
        var from = placements.FirstOrDefault(p => string.Equals(p.ReportId, fromId, StringComparison.Ordinal));
        var to = placements.FirstOrDefault(p => string.Equals(p.ReportId, toId, StringComparison.Ordinal));
        if (from is null || to is null) return placements;
        IReadOnlyList<ReportPlacement> updated = [.. placements.Select(p => {
            if (string.Equals(p.ReportId, fromId, StringComparison.Ordinal)) return p with { Order = to.Order };
            if (string.Equals(p.ReportId, toId, StringComparison.Ordinal)) return p with { Order = from.Order };
            return p;
        })];
        return Pack(updated);
    }

    public static IReadOnlyList<ReportPlacement> MoveUp(IReadOnlyList<ReportPlacement> placements, string reportId) =>
        Swap(placements, reportId, -1);

    public static IReadOnlyList<ReportPlacement> MoveDown(IReadOnlyList<ReportPlacement> placements, string reportId) =>
        Swap(placements, reportId, 1);

    private static IReadOnlyList<ReportPlacement> Swap(IReadOnlyList<ReportPlacement> placements, string reportId, int direction) {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(reportId);
        var ordered = placements.OrderBy(p => p.Order).ToList();
        var index = ordered.FindIndex(p => string.Equals(p.ReportId, reportId, StringComparison.Ordinal));
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count) return Pack(placements);
        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        return Pack(Renumber(ordered));
    }

    private static List<ReportPlacement> Renumber(List<ReportPlacement> ordered) {
        var result = new List<ReportPlacement>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++) result.Add(ordered[i] with { Order = i });
        return result;
    }
}
