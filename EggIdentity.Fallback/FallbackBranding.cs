namespace EggIdentity.Fallback;

public sealed record FallbackBranding(string AppName, IReadOnlyDictionary<string, string> Tokens, string? RoleClaimType = null);
