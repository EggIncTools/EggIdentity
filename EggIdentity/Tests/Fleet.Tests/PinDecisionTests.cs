using EggIdentity.Contract;

namespace EggIdentity.Fleet.Tests;

public class PinDecisionTests {
    private static PinInputs In(string? desired = "142.0.0", bool follow = false, string? latest = null,
        string[]? committed = null, string? pending = null, bool token = true) =>
        new(desired, follow, latest, committed ?? ["140.0.0"], pending, token);

    [Fact] public void NoToken_Wins() => Assert.Equal(PinAction.NoToken, PinDecision.Decide(In(token: false)));
    [Fact] public void NoDesiredNoFollow_IsNoTarget() => Assert.Equal(PinAction.NoTarget, PinDecision.Decide(In(desired: null)));
    [Fact] public void FollowWithoutLatest_FallsBackToDesired() => Assert.Equal("142.0.0", PinDecision.Target(In(follow: true)));
    [Fact] public void Follow_UsesLatest() => Assert.Equal("150.0.0", PinDecision.Target(In(follow: true, latest: "150.0.0")));
    [Fact] public void EmptyProps_IsNoPackages() => Assert.Equal(PinAction.NoPackages, PinDecision.Decide(In(committed: [])));
    [Fact] public void Differs_Commits() => Assert.Equal(PinAction.Commit, PinDecision.Decide(In()));
    [Fact] public void Matches_IsInSync() => Assert.Equal(PinAction.InSync, PinDecision.Decide(In(committed: ["142.0.0"])));
    [Fact] public void MixedVersions_Commits() => Assert.Equal(PinAction.Commit, PinDecision.Decide(In(committed: ["142.0.0", "140.0.0"])));
    [Fact] public void PendingForTarget_Waits() => Assert.Equal(PinAction.Waiting, PinDecision.Decide(In(committed: ["142.0.0"], pending: "142.0.0")));
    [Fact] public void PendingForStaleTarget_CommitsAgain() => Assert.Equal(PinAction.Commit, PinDecision.Decide(In(pending: "141.0.0")));
}
