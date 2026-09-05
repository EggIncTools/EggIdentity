using EggIdentity.Contract;
using EggIdentity.UI;

namespace EggIdentity.Deploy.AdminUi;

public sealed class DeployToastBridge(IDeployEvents events, ToastService toasts, TimeProvider? time = null) : IDisposable {
    public static readonly TimeSpan StaleWindow = TimeSpan.FromMinutes(2);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private bool _started;

    public void Start() {
        if (_started) return;
        _started = true;
        events.Received += OnEvent;
    }

    public void Dispose() {
        if (!_started) return;
        _started = false;
        events.Received -= OnEvent;
    }

    private void OnEvent(DeployEvent evt) {
        if (_time.GetUtcNow() - evt.At > StaleWindow) return;
        toasts.Push(Kind(evt.Phase), Text(evt));
    }

    internal static StatusNoteKind Kind(DeployPhase phase) => phase switch {
        DeployPhase.Pulling or DeployPhase.Pulled or DeployPhase.Recreating => StatusNoteKind.Busy,
        DeployPhase.Deployed => StatusNoteKind.Ok,
        DeployPhase.Failed => StatusNoteKind.Error,
        _ => StatusNoteKind.Info,
    };

    internal static string Text(DeployEvent evt) {
        if (evt.Phase != DeployPhase.ReleaseAvailable) return $"{evt.App}: {evt.Message}";
        var what = !string.IsNullOrWhiteSpace(evt.Version) ? evt.Version.Trim() : DeployPanelFormat.ShortDigest(evt.Digest);
        return what.Length == 0 ? $"{evt.App}: release available" : $"{evt.App}: {what} available";
    }
}
