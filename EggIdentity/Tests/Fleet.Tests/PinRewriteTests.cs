namespace EggIdentity.Fleet.Tests;

public class PinRewriteTests {
    private const string Literal = """
        <Project>
          <ItemGroup>
            <PackageVersion Include="EggIdentity.Core" Version="3.9.4" />
            <PackageVersion Include="EggIdentity.UI" Version="3.9.4" />
            <PackageVersion Include="EggIncognito.Protos" Version="2.17.0" />
            <PackageVersion Include="MinVer" Version="7.0.0" />
          </ItemGroup>
        </Project>
        """;

    private const string Property = """
        <Project>
          <PropertyGroup>
            <EggIdentityVersion>3.5.1</EggIdentityVersion>
          </PropertyGroup>
          <ItemGroup>
            <PackageVersion Include="EggIdentity.Auth" Version="$(EggIdentityVersion)" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void Read_Literal_ReturnsPrefixVersionsOnly() => Assert.Equal(["3.9.4"], PinRewrite.Read(Literal, "EggIdentity."));

    [Fact]
    public void Read_Property_ResolvesIt() => Assert.Equal(["3.5.1"], PinRewrite.Read(Property, "EggIdentity."));

    [Fact]
    public void Apply_Literal_ChangesOnlyThePrefix() {
        var text = PinRewrite.Apply(Literal, "EggIdentity.", "142.0.0");

        Assert.Equal(["142.0.0"], PinRewrite.Read(text, "EggIdentity."));
        Assert.Equal(["2.17.0"], PinRewrite.Read(text, "EggIncognito."));
        Assert.Contains("""Include="MinVer" Version="7.0.0" """, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_Property_InlinesLiteralAndDropsTheDeadProperty() {
        var text = PinRewrite.Apply(Property, "EggIdentity.", "142.0.0");

        Assert.Equal(["142.0.0"], PinRewrite.Read(text, "EggIdentity."));
        Assert.DoesNotContain("EggIdentityVersion", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_IsIdempotent() {
        var once = PinRewrite.Apply(Literal, "EggIdentity.", "142.0.0");

        Assert.Equal(once, PinRewrite.Apply(once, "EggIdentity.", "142.0.0"));
    }

    [Fact]
    public void Read_NoMatches_IsEmpty() => Assert.Empty(PinRewrite.Read(Literal, "EggAbacus."));
}
