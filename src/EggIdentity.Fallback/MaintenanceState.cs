namespace EggIdentity.Fallback;

public sealed class MaintenanceState {
    private volatile bool _isOn;

    public bool IsOn => _isOn;

    public void Set(bool on) => _isOn = on;
}
