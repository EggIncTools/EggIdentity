using System.Collections.Frozen;

namespace EggIdentity.UI;

public enum WorkbenchStatusKind {
    Queued,
    Running,
    Done,
    Error,
    Info,
    Muted
}

public static class WorkbenchStatus {
    private static readonly FrozenDictionary<string, WorkbenchStatusKind> Known = new Dictionary<string, WorkbenchStatusKind> {
        ["queued"] = WorkbenchStatusKind.Queued,
        ["pending"] = WorkbenchStatusKind.Queued,
        ["running"] = WorkbenchStatusKind.Running,
        ["run"] = WorkbenchStatusKind.Running,
        ["succeeded"] = WorkbenchStatusKind.Done,
        ["done"] = WorkbenchStatusKind.Done,
        ["ok"] = WorkbenchStatusKind.Done,
        ["failed"] = WorkbenchStatusKind.Error,
        ["error"] = WorkbenchStatusKind.Error,
        ["err"] = WorkbenchStatusKind.Error,
        ["info"] = WorkbenchStatusKind.Info,
        ["offer"] = WorkbenchStatusKind.Info,
        ["offerable"] = WorkbenchStatusKind.Info
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string Class(WorkbenchStatusKind kind) => kind switch {
        WorkbenchStatusKind.Queued => "wb-st-queued",
        WorkbenchStatusKind.Running => "wb-st-run",
        WorkbenchStatusKind.Done => "wb-st-done",
        WorkbenchStatusKind.Error => "wb-st-err",
        WorkbenchStatusKind.Info => "wb-st-offer",
        _ => "wb-st-muted"
    };

    public static WorkbenchStatusKind Parse(string? value) =>
        value is { Length: > 0 } && Known.TryGetValue(value, out var kind) ? kind : WorkbenchStatusKind.Muted;
}
