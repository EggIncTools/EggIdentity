namespace EggIdentity.Models;

public sealed class CookieConsent {
    public Guid UserId { get; set; }
    public bool Functional { get; set; }
    public bool Analytics { get; set; }
    public int PolicyVersion { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
