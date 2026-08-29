namespace EggIdentity.Models;

public sealed class GitHubSponsorStatus {
    public Guid UserId { get; set; }
    public bool IsSponsor { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
