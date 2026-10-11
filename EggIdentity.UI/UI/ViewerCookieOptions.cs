using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.UI;

public sealed record ViewerCookieRule(TimeSpan Lifetime, string? Domain = null);

public sealed class ViewerCookieOptions {
    public const int MaxValueLength = 3800;
    public const string ThemeCookie = "theme";
    public const string MotionCookie = "motion";
    public const string EidCookie = "eid";

    public static IReadOnlySet<string> SuiteCookies { get; } = new HashSet<string>([ThemeCookie, MotionCookie, EidCookie], StringComparer.Ordinal);

    public string Path { get; init; } = "/_viewer-cookie";
    public string? Domain { get; set; }
    public Dictionary<string, ViewerCookieRule> Cookies { get; } = [with(StringComparer.Ordinal)];

    public string? DomainFor(string name) {
        if (Cookies.TryGetValue(name, out var rule) && rule.Domain is not null) return rule.Domain;
        return SuiteCookies.Contains(name) ? Domain : null;
    }

    public ViewerCookieOptions Allow(string name, TimeSpan lifetime, string? domain = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Cookies[name] = new ViewerCookieRule(lifetime, domain);
        return this;
    }

    public static ViewerCookieOptions In(IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        if (services.FirstOrDefault(d => d.ServiceType == typeof(ViewerCookieOptions))?.ImplementationInstance is ViewerCookieOptions existing)
            return existing;
        var options = new ViewerCookieOptions();
        services.AddSingleton(options);
        services.AddHttpContextAccessor();
        services.AddScoped<ViewerCookies>();
        return options;
    }
}
