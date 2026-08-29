using EggIdentity.Agent;

namespace EggIdentity.Agent.Tests;

public class AgentRegistryTests {
    private static string WriteTempDir(params (string fileName, string content)[] files) {
        var dir = Path.Combine(Path.GetTempPath(), "eggidentity-agents-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        foreach (var (fileName, content) in files)
            File.WriteAllText(Path.Combine(dir, fileName), content);
        return dir;
    }

    [Fact]
    public void LoadFromDir_TwoValidFiles_KeyedByName() {
        var dir = WriteTempDir(
            ("a.yaml", "name: appa\nsteps:\n  - git-pull\n"),
            ("b.yaml", "name: appb\nsteps:\n  - git-pull\n"));

        var registry = AgentRegistry.LoadFromDir(dir);

        Assert.Equal(2, registry.Apps.Count);
        Assert.True(registry.Apps.ContainsKey("appa"));
        Assert.True(registry.Apps.ContainsKey("appb"));
    }

    [Fact]
    public void LoadFromDir_DuplicateName_Throws() {
        var dir = WriteTempDir(
            ("a.yaml", "name: dup\nsteps:\n  - git-pull\n"),
            ("b.yaml", "name: dup\nsteps:\n  - git-pull\n"));

        Assert.Throws<FormatException>(() => AgentRegistry.LoadFromDir(dir));
    }

    [Fact]
    public void LoadFromDir_MissingName_Throws() {
        var dir = WriteTempDir(("a.yaml", "steps:\n  - git-pull\n"));

        Assert.Throws<FormatException>(() => AgentRegistry.LoadFromDir(dir));
    }

    [Fact]
    public void LoadFromDir_MissingDirectory_Throws() {
        var dir = Path.Combine(Path.GetTempPath(), "eggidentity-agents-missing-" + Guid.NewGuid());

        Assert.Throws<DirectoryNotFoundException>(() => AgentRegistry.LoadFromDir(dir));
    }

    [Fact]
    public void LoadFromDir_EmptyDirectory_Throws() {
        var dir = WriteTempDir();

        Assert.Throws<FormatException>(() => AgentRegistry.LoadFromDir(dir));
    }

    [Fact]
    public void LoadFromDir_NonYamlFile_Ignored() {
        var dir = WriteTempDir(("a.yaml", "name: appa\nsteps:\n  - git-pull\n"));
        File.WriteAllText(Path.Combine(dir, ".gitkeep"), "");

        var registry = AgentRegistry.LoadFromDir(dir);

        Assert.Single(registry.Apps);
    }
}
