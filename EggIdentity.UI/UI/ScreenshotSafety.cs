namespace EggIdentity.UI;

public sealed class ScreenshotSafety {
    public event Action? Changed;

    public bool Enabled {
        get;
        set {
            if (field == value) return;
            field = value;
            Changed?.Invoke();
        }
    } = true;
}
