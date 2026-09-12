using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed record DeployApp {
    public string Name { get; init; } = "";
    public string Image { get; init; } = "";
    public string? Container { get; init; }
    public string? RepoUrl { get; init; }
    public string? DeploySecret { get; init; }
    public bool AutoDeploy { get; init; } = true;
    public bool Enabled { get; init; } = true;
    public string? PublicUrl { get; init; }
    public string? BrandSlug { get; init; }
    public bool Listed { get; init; }

    public string ContainerName => string.IsNullOrEmpty(Container) ? Name : Container;
}

public static class DeployApps {
    public const string Key = "deploy.apps";

    public static CollectionDescriptor Descriptor { get; } = new(
        Key, "Deployed apps", "Deploy",
        [
            new FieldDescriptor("name", "App name", SettingKind.Text) {
                Required = true,
                Description = "Route key and default container name.",
            },
            new FieldDescriptor("image", "Image", SettingKind.Text) {
                Required = true,
                Description = "Full image reference, for example ghcr.io/egginctools/eggledger:latest.",
            },
            new FieldDescriptor("container", "Container", SettingKind.Text) {
                Description = "Container to recreate. Defaults to the app name.",
            },
            new FieldDescriptor("repo_url", "Repository URL", SettingKind.Url) {
                Description = "Used to build commit links from image revision labels.",
            },
            new FieldDescriptor("deploy_secret", "Deploy secret", SettingKind.Secret, Sensitivity.Secret) {
                Description = "Bearer accepted on POST /deploy/{app} for this app.",
            },
            new FieldDescriptor("auto_deploy", "Auto deploy", SettingKind.Bool) { Default = "true" },
            new FieldDescriptor("enabled", "Enabled", SettingKind.Bool) { Default = "true" },
            new FieldDescriptor("public_url", "Public URL", SettingKind.Url) {
                Description = "Where the tool is served. Empty means it has no public address of its own.",
            },
            new FieldDescriptor("brand_slug", "Brand slug", SettingKind.Text) {
                Description = "Joins this row to the brand marks in EggIdentity.Contract. The app name is a container key and does not always match.",
            },
            new FieldDescriptor("listed", "Listed publicly", SettingKind.Bool) {
                Default = "false",
                Description = "Whether suite landing pages show this app. Off by default so adding a deploy row never publishes anything on its own.",
            },
        ],
        "name", "name") {
        Description = "Apps in the suite: what eggidentity-agent watches and recreates, plus where each one is served.",
    };

    public static ICollectionProvider Provider { get; } = new StaticCollectionProvider([Descriptor]);
}
