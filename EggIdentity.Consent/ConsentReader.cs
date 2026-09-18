namespace EggIdentity.Consent;

public sealed class ConsentReader : IConsentReader {
    public ConsentState? Current { get; private set; }

    public event EventHandler? Changed;

    public bool Allows(ConsentCategory category) => Current is not null && category switch {
        ConsentCategory.Functional => Current.Functional,
        ConsentCategory.Analytics => Current.Analytics,
        _ => false,
    };

    public void Set(ConsentState? state) {
        Current = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
