using EggIdentity.Settings;

namespace EggIdentity.Host;

public sealed record AuthentikApp {
    public string Origin { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string CallbackUrl { get; init; } = "";
    public string? EndSessionUrl { get; init; }
}

public static class AuthentikApps {
    public const string Key = "authentik.apps";

    public static CollectionDescriptor Descriptor { get; } = new(
        Key, "Authentik app registrations (legacy)", "Identity: SSO",
        [
            new FieldDescriptor("origin", "Origin", SettingKind.Url) {
                Required = true,
                Description = "Scheme and host of the app. Also the returnUrl allowlist.",
            },
            new FieldDescriptor("client_id", "Client id", SettingKind.Text) { Required = true },
            new FieldDescriptor("client_secret", "Client secret", SettingKind.Secret, Sensitivity.Secret) { Required = true },
            new FieldDescriptor("callback_url", "Callback URL", SettingKind.Url) {
                Required = true,
                Description = "Authentik provider redirect URI. Must point at this host's /auth/callback.",
            },
            new FieldDescriptor("end_session_url", "End-session URL", SettingKind.Url) {
                Description = "Provider end-session endpoint. Falls back to OIDC discovery when empty.",
            },
        ],
        "origin", "origin") {
        Description = "Read only while no suite.apps row has a client id. Move rows with eggidentity-tools import-suite-apps.",
    };

    public static ICollectionProvider Provider { get; } = new StaticCollectionProvider([Descriptor]);
}
