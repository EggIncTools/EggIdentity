using System.Reflection;
using EggIdentity.Contract;

namespace EggIdentity.Host;

public static class BuildInfo {
    public static VerifyInfo Build(Func<string, string?> envGetter, Assembly assembly) {
        var version = assembly
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
            .OfType<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "";
        return new VerifyInfo {
            Name = "EggIdentity",
            Sha256 = envGetter("GIT_SHA") ?? "",
            Version = version,
            Date = "",
        };
    }
}
