namespace EggIdentity.UI;

public enum FaultScope { Panel, Page }

public enum FaultKind { Crash, CrashRepeat, SignInFailed, Unavailable, NotFound }

public enum SuiteDependency { Identity, Database, EggIncApi, SyncServer, BrowserStorage, Capacity, Custom }

public interface IFaultDetailGate {
    ValueTask<bool> CanSeeDetailAsync();
}

public sealed record FaultCopy(string Icon, string Title, string Hint) {
    public static FaultCopy For(FaultKind kind) => kind switch {
        FaultKind.CrashRepeat => new("refresh-cw", "This keeps failing", "Reloading the page starts a fresh session."),
        FaultKind.SignInFailed => new("log-in", "Sign-in did not complete", "The identity service did not answer. Try again in a moment."),
        FaultKind.Unavailable => new("wifi-off", "Temporarily unavailable", ""),
        FaultKind.NotFound => new("search", "Page not found", "The link may be old or mistyped."),
        _ => new("triangle-alert", "This part stopped working", "Retrying usually fixes it. If not, quote the reference below."),
    };

    public static string Reason(SuiteDependency dependency) => dependency switch {
        SuiteDependency.Identity => "the sign-in service is not responding",
        SuiteDependency.Database => "the database is not reachable",
        SuiteDependency.EggIncApi => "the Egg Inc servers are not responding",
        SuiteDependency.SyncServer => "the sync server is not reachable",
        SuiteDependency.BrowserStorage => "browser storage is turned off",
        SuiteDependency.Capacity => "every slot is busy right now",
        _ => "a service it needs is not responding",
    };
}
