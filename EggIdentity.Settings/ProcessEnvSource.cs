namespace EggIdentity.Settings;

public sealed class ProcessEnvSource(SettingsRegistry registry) : IEnvSource {
    public Task<IReadOnlyList<EnvKeyInfo>> GetAsync(CancellationToken ct) => Task.FromResult(Read(registry));

    public static IReadOnlyList<EnvKeyInfo> Read(SettingsRegistry registry) {
        ArgumentNullException.ThrowIfNull(registry);

        var declared = registry.All
            .Select(d => d.EnvKey)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet(StringComparer.Ordinal);

        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in Environment.GetEnvironmentVariables().Keys) {
            if (name is string text && text.Length > 0) names.Add(text);
        }

        return [.. names
            .Where(n => declared.Contains(n) || LooksLikeAppKey(n))
            .Select(n => new EnvKeyInfo(n, EnvOrigin.ServiceEnvironment) { Masked = true })];
    }

    private static bool LooksLikeAppKey(string name) =>
        name.Length > 2
        && name.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_')
        && !Ignored.Contains(name);

    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal) {
        "ALLUSERSPROFILE", "APPDATA", "COMPUTERNAME", "COMSPEC", "DOTNET_ROOT", "HOME", "HOMEDRIVE",
        "HOMEPATH", "HOSTNAME", "LANG", "LOCALAPPDATA", "LOGONSERVER", "NUMBER_OF_PROCESSORS", "OS",
        "PATH", "PATHEXT", "PROCESSOR_ARCHITECTURE", "PROCESSOR_IDENTIFIER", "PROCESSOR_LEVEL",
        "PROCESSOR_REVISION", "PROGRAMDATA", "PROGRAMFILES", "PSMODULEPATH", "PUBLIC", "PWD", "SHLVL",
        "SYSTEMDRIVE", "SYSTEMROOT", "TEMP", "TERM", "TMP", "USERDOMAIN", "USERNAME", "USERPROFILE", "WINDIR",
    };
}
