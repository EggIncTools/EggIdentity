namespace EggIdentity.Consent;

public sealed record ConsentBannerContext(
    bool Functional,
    bool Analytics,
    bool Configuring,
    string? PrivacyUrl,
    Func<Task> AcceptAll,
    Func<Task> NecessaryOnly,
    Action Configure,
    Func<Task> Save,
    Action<bool> SetFunctional,
    Action<bool> SetAnalytics);
