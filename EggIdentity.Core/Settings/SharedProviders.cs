namespace EggIdentity.Settings;

public static class SharedProviders {
    private static readonly HashSet<ISettingsProvider> Registered = [SettingsFrameworkSettings.Provider];

    internal static IReadOnlySet<ISettingsProvider> All {
        get {
            lock (Registered) return new HashSet<ISettingsProvider>(Registered);
        }
    }

    public static ISettingsProvider Register(ISettingsProvider provider) {
        ArgumentNullException.ThrowIfNull(provider);
        lock (Registered) Registered.Add(provider);
        return provider;
    }
}
