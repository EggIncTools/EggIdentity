namespace EggIdentity.Models;

public sealed class UserMerge {
    public Guid MergedUserId { get; set; }
    public Guid KeptUserId { get; set; }
    public DateTimeOffset MergedAt { get; set; }
}
