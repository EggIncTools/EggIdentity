namespace EggIdentity.Consent;

public sealed class ConsentOptions {
    public string? CookieDomain { get; set; }
    public int PolicyVersion { get; set; } = 1;
    public TimeSpan CookieLifetime { get; set; } = TimeSpan.FromDays(365);
}
