namespace EggIdentity.Contract.Tests;

public class BuildVersionTests {
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Parse_DerivedForm_ReadsHeightAndSha() {
        var v = BuildVersion.Parse($"142.0.0+{Sha}");

        Assert.Equal(142, v?.Height);
        Assert.Equal(Sha, v?.Sha);
        Assert.Equal("#142 0123456", v?.Display);
    }

    [Fact]
    public void Parse_NoMetadata_HasNoSha() => Assert.Equal("#142", BuildVersion.Parse("142.0.0")?.Display);

    [Theory]
    [InlineData("3.9.4")]
    [InlineData("3.9.4+abc")]
    [InlineData("v142.0.0")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_AnythingElse_IsNull(string? input) => Assert.Null(BuildVersion.Parse(input));

    [Fact]
    public void Describe_LegacyVersion_FallsBackToRevision() => Assert.Equal("0123456", BuildVersion.Describe("3.9.4", Sha));

    [Fact]
    public void Describe_DerivedVersionWithoutSha_UsesRevision() => Assert.Equal("#142 0123456", BuildVersion.Describe("142.0.0", Sha));

    [Fact]
    public void Describe_Nothing_IsEmpty() => Assert.Equal("", BuildVersion.Describe(null, null));

    [Fact]
    public void FromHeight_IsMajorOnly() => Assert.Equal("142.0.0", BuildVersion.FromHeight(142));
}
