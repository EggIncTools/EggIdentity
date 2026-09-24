using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed record DeployApp {
    public const string ProdEnvironment = "prod";
    public const string SubProdEnvironment = "subprod";
    public const string DefaultTag = "latest";

    public string Name { get; init; } = "";
    public string Repository { get; init; } = "";
    public string Tag { get; init; } = "";
    public string PreviousTag { get; init; } = "";
    public string Environment { get; init; } = ProdEnvironment;
    public string? Container { get; init; }
    public string? RepoUrl { get; init; }
    public string? DeploySecret { get; init; }
    public string? Stack { get; init; }
    public bool AutoDeploy { get; init; } = true;
    public bool Enabled { get; init; } = true;
    public string? PublicUrl { get; init; }
    public string? BrandSlug { get; init; }
    public bool Listed { get; init; }

    public string ContainerName => string.IsNullOrEmpty(Container) ? Name : Container;

    public bool TracksLatest => string.Equals(Environment, SubProdEnvironment, StringComparison.OrdinalIgnoreCase);

    public string ResolvedTag {
        get {
            if (!string.IsNullOrWhiteSpace(Tag)) return Tag.Trim();
            return string.IsNullOrWhiteSpace(LegacyTag) ? DefaultTag : LegacyTag;
        }
    }

    public string Image {
        init => LegacyImage = value?.Trim() ?? "";
        get => EffectiveRepository.Length == 0 ? LegacyImage : Compose(EffectiveRepository, ResolvedTag);
    }

    private string LegacyImage { get; init; } = "";

    private string LegacyRepository {
        get {
            Split(LegacyImage, out var repository, out _);
            return repository;
        }
    }

    private string LegacyTag {
        get {
            Split(LegacyImage, out _, out var tag);
            return tag;
        }
    }

    private string EffectiveRepository =>
        string.IsNullOrWhiteSpace(Repository) ? LegacyRepository : Repository.Trim();

    public bool CanRollBack => !string.IsNullOrWhiteSpace(PreviousTag)
        && !string.Equals(PreviousTag.Trim(), ResolvedTag, StringComparison.Ordinal);

    public DeployApp WithTag(string tag) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        var next = tag.Trim();
        return string.Equals(next, ResolvedTag, StringComparison.Ordinal)
            ? this
            : this with { Tag = next, PreviousTag = ResolvedTag };
    }

    public DeployApp RollBack() => CanRollBack ? WithTag(PreviousTag.Trim()) : this;

    private static void Split(string raw, out string repository, out string tag) {
        repository = "";
        tag = "";
        if (raw.Length == 0) return;

        var at = raw.IndexOf('@', StringComparison.Ordinal);
        if (at > 0 && at < raw.Length - 1) {
            repository = raw[..at];
            tag = raw[(at + 1)..];
            return;
        }
        if (at >= 0) return;

        var lastColon = raw.LastIndexOf(':');
        var lastSlash = raw.LastIndexOf('/');
        if (lastColon > lastSlash) {
            if (lastColon == 0 || lastColon == raw.Length - 1) return;
            repository = raw[..lastColon];
            tag = raw[(lastColon + 1)..];
            return;
        }

        repository = raw;
    }

    private static string Compose(string repository, string tag) {
        var repo = repository.Trim();
        if (repo.Length == 0) return "";
        return tag.StartsWith("sha256:", StringComparison.Ordinal) ? $"{repo}@{tag}" : $"{repo}:{tag}";
    }
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
            new FieldDescriptor("image", "Image (legacy)", SettingKind.Text) {
                Legacy = true,
                Description = "Full reference, superseded by repository and tag. Read only when repository is empty.",
            },
            new FieldDescriptor("repository", "Repository", SettingKind.Text) {
                Description = "Image repository without a tag, for example ghcr.io/egginctools/eggledger.",
            },
            new FieldDescriptor("tag", "Tag", SettingKind.Text) {
                Description = "Tag this environment runs. Defaults to latest.",
            },
            new FieldDescriptor("previous_tag", "Previous tag", SettingKind.Text) {
                Description = "Set on promotion so a rollback needs no registry lookup.",
            },
            new FieldDescriptor("environment", "Environment", SettingKind.Enum) {
                Default = DeployApp.ProdEnvironment,
                EnumValues = [DeployApp.ProdEnvironment, DeployApp.SubProdEnvironment],
                Description = "Sub-prod tracks latest. Production changes only on promotion.",
            },
            new FieldDescriptor("container", "Container", SettingKind.Text) {
                Description = "Container to recreate. Defaults to the app name.",
            },
            new FieldDescriptor("stack", "Stack", SettingKind.Text) {
                Description = "deploy.stacks row that redeploys this app. Apps in one stack share a row.",
            },
            new FieldDescriptor("repo_url", "Repository URL", SettingKind.Url) {
                Description = "Builds commit links from image revision labels.",
            },
            new FieldDescriptor("deploy_secret", "Deploy secret", SettingKind.Secret, Sensitivity.Secret) {
                Description = "Bearer accepted on POST /deploy/{app} for this app.",
            },
            new FieldDescriptor("auto_deploy", "Auto deploy", SettingKind.Bool) { Default = "true" },
            new FieldDescriptor("enabled", "Enabled", SettingKind.Bool) { Default = "true" },
            new FieldDescriptor("public_url", "Public URL", SettingKind.Url) {
                Description = "Empty means no public address of its own.",
            },
            new FieldDescriptor("brand_slug", "Brand slug", SettingKind.Text) {
                Description = "Joins to the brand marks in EggIdentity.Contract. The app name is a container key and need not match.",
            },
            new FieldDescriptor("listed", "Listed publicly", SettingKind.Bool) {
                Default = "false",
                Description = "Shows this app on suite landing pages. Off so adding a deploy row publishes nothing on its own.",
            },
        ],
        "name", "name") {
        Description = "One row per app the agent deploys.",
    };

    public static ICollectionProvider Provider { get; } = new StaticCollectionProvider([Descriptor]);
}
