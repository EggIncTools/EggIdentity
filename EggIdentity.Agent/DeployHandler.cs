using EggIdentity.Contract;

namespace EggIdentity.Agent;

public sealed class DeployHandler {
    private readonly Lock _gate = new();
    private bool _inProgress;

    public bool TryEnter() {
        lock (_gate) {
            if (_inProgress) {
                Console.WriteLine("deploy: skipped, another deploy is already in progress");
                return false;
            }
            _inProgress = true;
            return true;
        }
    }

    public DeployResponse RunAndExit(Func<DeployResponse> run) {
        try { return run(); } finally {
            lock (_gate) { _inProgress = false; }
        }
    }

    public (DeployResponse Result, bool Ran) TryRun(Func<DeployResponse> run) {
        if (!TryEnter()) return (new DeployResponse(), false);
        return (RunAndExit(run), true);
    }
}
