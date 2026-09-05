using EggIdentity.Settings;

namespace EggIdentity.Agent.Tests;

public class EnvProvenanceTests {
    private static readonly HashSet<string> Referenced = new(["DB_CONN"], StringComparer.Ordinal);

    private static ComposeServiceInfo Service(bool envFile, params string[] keys) =>
        new(true, keys, envFile, Referenced);

    private static EnvKeyInfo Find(IReadOnlyList<EnvKeyInfo> entries, string name) => entries.Single(e => e.Name == name);

    [Fact]
    public void Build_ClassifiesEveryOrigin() {
        var entries = EnvProvenance.Build(
            Service(envFile: true, "IDENTITY_DB_CONNECTION", "GIT_SHA"),
            ["IDENTITY_DB_CONNECTION=pg", "GIT_SHA=abc", "FROM_FILE=1", "PATH=/usr/bin", "APP_UID=1654"],
            ["PATH=/usr/bin", "APP_UID=1654"],
            [new StackEnvEntry("DB_CONN", "pg"), new StackEnvEntry("UNUSED", "x")]);

        Assert.Equal(EnvOrigin.ServiceEnvironment, Find(entries, "IDENTITY_DB_CONNECTION").Origin);
        Assert.Equal(EnvOrigin.ServiceEnvironment, Find(entries, "GIT_SHA").Origin);
        Assert.Equal(EnvOrigin.EnvFile, Find(entries, "FROM_FILE").Origin);
        Assert.Equal(EnvOrigin.Image, Find(entries, "PATH").Origin);
        Assert.Equal(EnvOrigin.Image, Find(entries, "APP_UID").Origin);
        Assert.Equal(EnvOrigin.StackVariable, Find(entries, "DB_CONN").Origin);
        Assert.True(Find(entries, "DB_CONN").Referenced);
        Assert.False(Find(entries, "UNUSED").Referenced);
    }

    [Fact]
    public void Build_NoEnvFile_UnexplainedContainerKeysAreRuntime() {
        var entries = EnvProvenance.Build(Service(envFile: false, "A"), ["A=1", "B=2"], [], []);

        Assert.Equal(EnvOrigin.ServiceEnvironment, Find(entries, "A").Origin);
        Assert.Equal(EnvOrigin.Runtime, Find(entries, "B").Origin);
    }

    [Fact]
    public void Build_WithoutCompose_ContainerKeysAreServiceEnvironmentAndStackVariablesReferenced() {
        var entries = EnvProvenance.Build(null, ["A=1", "PATH=/bin"], ["PATH=/bin"], [new StackEnvEntry("X", "1")]);

        Assert.Equal(EnvOrigin.ServiceEnvironment, Find(entries, "A").Origin);
        Assert.Equal(EnvOrigin.Image, Find(entries, "PATH").Origin);
        Assert.True(Find(entries, "X").Referenced);
    }

    [Fact]
    public void Build_ServiceNotFoundInCompose_FallsBackToContainerOnlyClassification() {
        var entries = EnvProvenance.Build(ComposeServiceInfo.Missing(Referenced), ["A=1"], [], [new StackEnvEntry("UNUSED", "x")]);

        Assert.Equal(EnvOrigin.ServiceEnvironment, Find(entries, "A").Origin);
        Assert.False(Find(entries, "UNUSED").Referenced);
    }

    [Fact]
    public void Build_MasksSecretLookingValues() {
        var entries = EnvProvenance.Build(null, ["API_TOKEN=abc", "PORT=80"], [], [new StackEnvEntry("DB_PASSWORD", "pw")]);

        var token = Find(entries, "API_TOKEN");
        Assert.True(token.Masked);
        Assert.Equal("********", token.Value);
        Assert.False(Find(entries, "PORT").Masked);
        Assert.Equal("80", Find(entries, "PORT").Value);
        Assert.Equal("********", Find(entries, "DB_PASSWORD").Value);
    }

    [Fact]
    public void Build_ComposeKeyAbsentFromContainer_StillListedWithoutValue() {
        var entries = EnvProvenance.Build(Service(envFile: false, "OPTIONAL"), [], [], []);

        var optional = Find(entries, "OPTIONAL");
        Assert.Equal(EnvOrigin.ServiceEnvironment, optional.Origin);
        Assert.Null(optional.Value);
    }

    [Fact]
    public void Build_IsSortedByName() {
        var entries = EnvProvenance.Build(null, ["Z=1", "A=1", "M=1"], [], []);

        Assert.Equal(["A", "M", "Z"], entries.Select(e => e.Name));
    }
}
