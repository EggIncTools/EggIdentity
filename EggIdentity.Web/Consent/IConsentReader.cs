namespace EggIdentity.Consent;

public interface IConsentReader {
    ConsentState? Current { get; }
    event EventHandler? Changed;
    bool Allows(ConsentCategory category);
}
