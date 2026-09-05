namespace EggIdentity.Agent;

public sealed class DeployHandler {
    private readonly Lock _gate = new();
    private bool _inProgress;

    public bool InProgress {
        get {
            lock (_gate) {
                return _inProgress;
            }
        }
    }

    public bool TryEnter() {
        lock (_gate) {
            if (_inProgress) return false;
            _inProgress = true;
            return true;
        }
    }

    public void Exit() {
        lock (_gate) {
            _inProgress = false;
        }
    }
}
