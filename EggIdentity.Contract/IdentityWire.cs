namespace EggIdentity.Contract;

public static class IdentityWire {
    public const string SessionHeader = "X-EggIdentity-Session";

    public const string Discord = "discord";
    public const string Google = "google";
    public const string Microsoft = "microsoft";
    public const string GitHub = "github";

    public static readonly string[] KnownProviders = [Discord, Google, Microsoft, GitHub];

    public static string AvatarPath(Guid userId) => $"/avatars/{userId}";
}
