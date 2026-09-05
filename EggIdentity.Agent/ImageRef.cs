namespace EggIdentity.Agent;

public sealed record ImageRef(string Registry, string Repository, string Tag) {
    public const string DockerHub = "docker.io";

    public static ImageRef Parse(string reference) {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        var text = reference.Trim();

        string? digest = null;
        var at = text.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0) {
            digest = text[(at + 1)..];
            text = text[..at];
        }

        var tag = "latest";
        var lastSlash = text.LastIndexOf('/');
        var lastColon = text.LastIndexOf(':');
        if (lastColon > lastSlash) {
            tag = text[(lastColon + 1)..];
            text = text[..lastColon];
        }
        if (digest is not null) tag = digest;

        if (text.Length == 0 || tag.Length == 0)
            throw new FormatException($"bad image reference \"{reference}\"");

        var firstSlash = text.IndexOf('/', StringComparison.Ordinal);
        var first = firstSlash < 0 ? "" : text[..firstSlash];
        var looksLikeRegistry = firstSlash > 0 && (first.Contains('.', StringComparison.Ordinal) || first.Contains(':', StringComparison.Ordinal) || first == "localhost");

        if (looksLikeRegistry)
            return new ImageRef(first, text[(firstSlash + 1)..], tag);

        var repository = firstSlash < 0 ? "library/" + text : text;
        return new ImageRef(DockerHub, repository, tag);
    }

    public string RegistryHost => Registry == DockerHub ? "registry-1.docker.io" : Registry;

    public Uri TokenUri => Registry == DockerHub
        ? new Uri($"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{Repository}:pull")
        : new Uri($"https://{Registry}/token?scope=repository:{Repository}:pull&service={Registry}");

    public Uri ManifestUri => new($"https://{RegistryHost}/v2/{Repository}/manifests/{Tag}");

    public string Name {
        get {
            if (Registry != DockerHub) return $"{Registry}/{Repository}";
            return Repository.StartsWith("library/", StringComparison.Ordinal) ? Repository["library/".Length..] : Repository;
        }
    }

    public override string ToString() => Tag.StartsWith("sha256:", StringComparison.Ordinal) ? $"{Name}@{Tag}" : $"{Name}:{Tag}";
}
