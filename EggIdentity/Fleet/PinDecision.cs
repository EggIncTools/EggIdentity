using EggIdentity.Contract;

namespace EggIdentity.Fleet;

public sealed record PinInputs(
    string? Desired, bool FollowLatest, string? Latest, IReadOnlyList<string> Committed, string? PendingVersion, bool HasToken);

public static class PinDecision {
    public static string? Target(PinInputs p) => p.FollowLatest ? p.Latest ?? p.Desired : p.Desired;

    public static PinAction Decide(PinInputs p) {
        if (!p.HasToken) return PinAction.NoToken;
        if (Target(p) is not { } target) return PinAction.NoTarget;
        if (p.Committed.Count == 0) return PinAction.NoPackages;
        if (p.PendingVersion == target) return PinAction.Waiting;
        return p.Committed.All(v => v == target) ? PinAction.InSync : PinAction.Commit;
    }
}
