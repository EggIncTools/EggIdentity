using EggIdentity.Contract;

namespace EggIdentity.Host;

public static class AvatarUrl {
    public static string? Canonical(Guid userId, string? stored) =>
        string.IsNullOrEmpty(stored) ? null : IdentityWire.AvatarPath(userId);
}
