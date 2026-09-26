using EggIdentity.DbClone;

namespace EggIdentity.Tools;

internal static class SubProdClonePlan {
    public const string TargetDatabase = "eggidentity_subprod";

    public static ClonePlan Plan { get; } = new("eggidentity", TargetDatabase, [
        new TablePolicy("users", ClonePolicy.Full),
        new TablePolicy("identities", ClonePolicy.Full),
        new TablePolicy("user_merges", ClonePolicy.Full),
        new TablePolicy("github_sponsor_status", ClonePolicy.Full),
        new TablePolicy("cookie_consent", ClonePolicy.Full),
        new TablePolicy("revoked_sessions", ClonePolicy.SchemaOnly),
        new TablePolicy("login_codes", ClonePolicy.SchemaOnly),
        new TablePolicy("oauth_states", ClonePolicy.SchemaOnly),
        new TablePolicy("site_visits_daily", ClonePolicy.SchemaOnly),
        new TablePolicy("site_visit_paths_daily", ClonePolicy.SchemaOnly),
        new TablePolicy("bot_channel_config", ClonePolicy.SchemaOnly),
        new TablePolicy("bot_channel_state", ClonePolicy.SchemaOnly),
        new TablePolicy("deploy_state", ClonePolicy.SchemaOnly),
        new TablePolicy("app_settings", ClonePolicy.Skip),
        new TablePolicy("app_setting_collections", ClonePolicy.Skip),
    ]) {
        IgnoredTables = ["eggidentity_migrations", "eggidentity_settings_migrations", "eggidentity_bot_migrations"],
    };
}
