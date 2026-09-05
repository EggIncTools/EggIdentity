namespace EggIdentity.Agent.Tests;

public class ImageRefTests {
    [Fact]
    public void Parse_Ghcr_SplitsRegistryRepositoryTag() {
        var image = ImageRef.Parse("ghcr.io/egginctools/eggledger:1.2.3");

        Assert.Equal("ghcr.io", image.Registry);
        Assert.Equal("egginctools/eggledger", image.Repository);
        Assert.Equal("1.2.3", image.Tag);
        Assert.Equal("ghcr.io", image.RegistryHost);
        Assert.Equal("ghcr.io/egginctools/eggledger", image.Name);
        Assert.Equal("ghcr.io/egginctools/eggledger:1.2.3", image.ToString());
    }

    [Fact]
    public void Parse_NoTag_DefaultsLatest() =>
        Assert.Equal("latest", ImageRef.Parse("ghcr.io/x/y").Tag);

    [Fact]
    public void Parse_RegistryWithPort_KeepsPortOutOfTag() {
        var image = ImageRef.Parse("localhost:5000/team/app");

        Assert.Equal("localhost:5000", image.Registry);
        Assert.Equal("team/app", image.Repository);
        Assert.Equal("latest", image.Tag);
    }

    [Fact]
    public void Parse_BareName_IsDockerHubLibrary() {
        var image = ImageRef.Parse("postgres:16");

        Assert.Equal(ImageRef.DockerHub, image.Registry);
        Assert.Equal("library/postgres", image.Repository);
        Assert.Equal("16", image.Tag);
        Assert.Equal("registry-1.docker.io", image.RegistryHost);
        Assert.Equal("postgres", image.Name);
        Assert.Equal("postgres:16", image.ToString());
    }

    [Fact]
    public void Parse_OwnerName_IsDockerHub() {
        var image = ImageRef.Parse("portainer/portainer-ce:latest");

        Assert.Equal(ImageRef.DockerHub, image.Registry);
        Assert.Equal("portainer/portainer-ce", image.Repository);
        Assert.Equal("portainer/portainer-ce", image.Name);
    }

    [Fact]
    public void Parse_DigestReference_UsesDigestAsManifestReference() {
        var image = ImageRef.Parse("ghcr.io/x/y:1.0@sha256:abc123");

        Assert.Equal("x/y", image.Repository);
        Assert.Equal("sha256:abc123", image.Tag);
        Assert.Equal("ghcr.io/x/y@sha256:abc123", image.ToString());
    }

    [Fact]
    public void Uris_Ghcr_PointAtTokenAndManifestEndpoints() {
        var image = ImageRef.Parse("ghcr.io/egginctools/eggledger:latest");

        Assert.Equal("https://ghcr.io/token?scope=repository:egginctools/eggledger:pull&service=ghcr.io", image.TokenUri.ToString());
        Assert.Equal("https://ghcr.io/v2/egginctools/eggledger/manifests/latest", image.ManifestUri.ToString());
    }

    [Fact]
    public void Uris_DockerHub_UseAuthAndRegistry1Hosts() {
        var image = ImageRef.Parse("postgres:16");

        Assert.Equal("https://auth.docker.io/token?service=registry.docker.io&scope=repository:library/postgres:pull", image.TokenUri.ToString());
        Assert.Equal("https://registry-1.docker.io/v2/library/postgres/manifests/16", image.ManifestUri.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ghcr.io/x/y:")]
    public void Parse_Invalid_Throws(string reference) =>
        Assert.ThrowsAny<Exception>(() => ImageRef.Parse(reference));
}
