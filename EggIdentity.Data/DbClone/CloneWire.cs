using EggIdentity.Contract;

namespace EggIdentity.DbClone;

public static class CloneWire {
    public static CloneStatusResponse ToWire(string app, CloneStatus status, CloneStamp? stamp) {
        ArgumentNullException.ThrowIfNull(status);
        return new CloneStatusResponse {
            App = app,
            State = status.State.ToString().ToLowerInvariant(),
            StartedAt = status.StartedAt,
            FinishedAt = status.FinishedAt,
            Error = status.Error,
            Events = [.. status.Events.Select(ToWire)],
            Tables = [.. status.Tables.Select(ToWire)],
            LastStampAt = stamp?.ClonedAt,
            LastStampSource = stamp?.SourceDatabase,
        };
    }

    public static CloneEventWire ToWire(CloneEvent e) {
        ArgumentNullException.ThrowIfNull(e);
        return new CloneEventWire { At = e.At, Phase = e.Phase, Message = e.Message, Table = e.Table };
    }

    public static CloneTableCountWire ToWire(CloneTableCount c) {
        ArgumentNullException.ThrowIfNull(c);
        return new CloneTableCountWire {
            Table = c.Table,
            Policy = c.Policy.ToString(),
            SourceRows = c.SourceRows,
            TargetRows = c.TargetRows,
        };
    }
}
