namespace EggIdentity.Agent.Tests;

public class ComposeEnvTests {
    private const string Compose = """
        services:
          eggledger:
            image: ghcr.io/x/eggledger:latest
            container_name: eggledger-web
            environment:
              - IDENTITY_DB_CONNECTION=${DB_CONN}
              - EGGIDENTITY_SESSION_SECRET=${SESSION_SECRET:-dev}
              - GIT_SHA
            env_file:
              - ledger.env
          eggincognito:
            image: ghcr.io/x/eggincognito:latest
            environment:
              CAPTURE_IFACE: eth0
              PRICE: "$$NOT_A_VAR"
              PORT: $APP_PORT
          agent:
            image: ghcr.io/x/agent:latest
        """;

    [Fact]
    public void Parse_ListForm_ReadsKeysWithAndWithoutValues() {
        var info = ComposeEnv.Parse(Compose, "eggledger");

        Assert.True(info.Found);
        Assert.Equal(["IDENTITY_DB_CONNECTION", "EGGIDENTITY_SESSION_SECRET", "GIT_SHA"], info.EnvironmentKeys);
        Assert.True(info.HasEnvFile);
    }

    [Fact]
    public void Parse_MapForm_ReadsKeysAndNoEnvFile() {
        var info = ComposeEnv.Parse(Compose, "eggincognito");

        Assert.True(info.Found);
        Assert.Equal(["CAPTURE_IFACE", "PRICE", "PORT"], info.EnvironmentKeys);
        Assert.False(info.HasEnvFile);
    }

    [Fact]
    public void Parse_MatchesByContainerName() {
        var info = ComposeEnv.Parse(Compose, "eggledger-web");

        Assert.True(info.Found);
        Assert.Contains("GIT_SHA", info.EnvironmentKeys);
    }

    [Fact]
    public void Parse_ServiceWithoutEnvironment_IsFoundWithNoKeys() {
        var info = ComposeEnv.Parse(Compose, "agent");

        Assert.True(info.Found);
        Assert.Empty(info.EnvironmentKeys);
    }

    [Fact]
    public void Parse_UnknownService_IsNotFoundButStillReportsReferences() {
        var info = ComposeEnv.Parse(Compose, "nope");

        Assert.False(info.Found);
        Assert.Contains("DB_CONN", info.ReferencedVariables);
    }

    [Fact]
    public void FindReferencedVariables_CoversBracedDefaultedAndBareForms_AndSkipsEscapedDollar() {
        var refs = ComposeEnv.FindReferencedVariables(Compose);

        Assert.Equal(["APP_PORT", "DB_CONN", "SESSION_SECRET"], refs.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Parse_EmptyDocument_IsNotFound() {
        var info = ComposeEnv.Parse("", "eggledger");

        Assert.False(info.Found);
        Assert.Empty(info.ReferencedVariables);
    }
}
