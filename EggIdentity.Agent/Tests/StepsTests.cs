using EggIdentity.Agent;

namespace EggIdentity.Agent.Tests;

public class StepsTests {
    [Fact]
    public void DockerBuild_HeadRevisionSet_IncludesRevisionLabel() {
        string[] capturedArgs = [];
        (string, bool) Run(string name, string[] args) {
            capturedArgs = args;
            return ("", true);
        }
        var c = new RunContext { Repo = "/repo", RepoUrl = "", Run = Run, HeadRevisionFull = "abc123full" };
        var step = new DockerBuild { Tag = "img:latest" };

        Assert.Null(step.Exec(c));
        Assert.Contains("--label", capturedArgs);
        Assert.Contains("org.opencontainers.image.revision=abc123full", capturedArgs);
    }

    [Fact]
    public void DockerBuild_HeadRevisionUnset_OmitsRevisionLabel() {
        string[] capturedArgs = [];
        (string, bool) Run(string name, string[] args) {
            capturedArgs = args;
            return ("", true);
        }
        var c = new RunContext { Repo = "/repo", RepoUrl = "", Run = Run };
        var step = new DockerBuild { Tag = "img:latest" };

        Assert.Null(step.Exec(c));
        Assert.DoesNotContain("--label", capturedArgs);
    }

    [Fact]
    public void DockerBuild_DockerfileSet_AddsDashFArg() {
        string[] capturedArgs = [];
        (string, bool) Run(string name, string[] args) {
            capturedArgs = args;
            return ("", true);
        }
        var c = new RunContext { Repo = "/repo", RepoUrl = "", Run = Run };
        var step = new DockerBuild { Tag = "img:latest", Dockerfile = "src/App/Dockerfile" };

        Assert.Null(step.Exec(c));
        Assert.Contains("-f", capturedArgs);
        Assert.Contains(Path.Combine("/repo", "src/App/Dockerfile"), capturedArgs);
    }

    [Fact]
    public void DockerBuild_DockerfileUnset_OmitsDashF() {
        string[] capturedArgs = [];
        (string, bool) Run(string name, string[] args) {
            capturedArgs = args;
            return ("", true);
        }
        var c = new RunContext { Repo = "/repo", RepoUrl = "", Run = Run };
        var step = new DockerBuild { Tag = "img:latest" };

        Assert.Null(step.Exec(c));
        Assert.DoesNotContain("-f", capturedArgs);
    }

    [Fact]
    public void GitPull_AlreadyUpToDate_SetsHeadRevisionFull() {
        static (string, bool) Run(string name, string[] args) {
            if (args[2] == "pull") return ("Already up to date.\n", true);
            if (args.Contains("--short")) return ("abc1234\n", true);
            if (args[2] == "rev-parse") return ("abc1234567890fullsha\n", true);
            return ("", false);
        }
        var c = new RunContext { Repo = "/repo", RepoUrl = "", Run = Run };
        var step = new GitPull();

        Assert.Null(step.Exec(c));
        Assert.True(c.ShortCircuit);
        Assert.Equal("abc1234567890fullsha", c.HeadRevisionFull);
    }

    [Fact]
    public void GitPull_PullHappened_SetsHeadRevisionFull() {
        static (string, bool) Run(string name, string[] args) {
            if (args[2] == "pull") return ("Updating abc123..def456\n", true);
            if (args.Contains("--short")) return ("def4567\n", true);
            if (args[2] == "rev-parse") return ("def4567890fullsha\n", true);
            return ("", false);
        }
        var c = new RunContext { Repo = "/repo", RepoUrl = "", Run = Run };
        var step = new GitPull();

        Assert.Null(step.Exec(c));
        Assert.False(c.ShortCircuit);
        Assert.Equal("def4567890fullsha", c.HeadRevisionFull);
    }

    [Fact]
    public void DockerPull_StaleRevisionAncestorOfHead_ShortCircuitsAndLogsReason() {
        const string rev = "abcdef1234567890abcdef1234567890abcdef12";
        const string headSha = "1111111111111111111111111111111111111111";

        static (string, bool) Run(string name, string[] args) {
            if (name == "docker") {
                if (args[0] == "pull") return ("Status: Downloaded newer image for img:latest\n", true);
                if (args[0] == "image" && args[1] == "inspect") {
                    if (args[3].StartsWith("{{.Id}}", StringComparison.Ordinal)) return ("sha256:someid <no value>", true);
                    if (args[3].StartsWith("{{index", StringComparison.Ordinal)) return (rev, true);
                }
                return ("", false);
            }
            if (name == "git") {
                if (args[2] == "fetch") return ("", true);
                if (args[2] == "merge-base") return ("", true);
                if (args[2] == "rev-parse") return (headSha, true);
            }
            return ("", false);
        }

        var c = new RunContext { Repo = "/repo", RepoUrl = "", Run = Run };
        var step = new DockerPull { Ref = "img:latest" };

        Assert.Null(step.Exec(c));
        Assert.True(c.ShortCircuit);
        Assert.Contains("stale", c.Out.ToString());
    }

    [Fact]
    public void DockerPull_RevisionNotAncestor_ProceedsNormally() {
        const string rev = "abcdef1234567890abcdef1234567890abcdef12";

        static (string, bool) Run(string name, string[] args) {
            if (name == "docker") {
                if (args[0] == "pull") return ("Status: Downloaded newer image for img:latest\n", true);
                if (args[0] == "image" && args[1] == "inspect") {
                    if (args[3].StartsWith("{{.Id}}", StringComparison.Ordinal)) return ("sha256:someid <no value>", true);
                    if (args[3].StartsWith("{{index", StringComparison.Ordinal)) return (rev, true);
                }
                return ("", false);
            }
            if (name == "git") {
                if (args[2] == "fetch") return ("", true);
                if (args[2] == "merge-base") return ("", false);
            }
            return ("", false);
        }

        var c = new RunContext { Repo = "/repo", RepoUrl = "", Run = Run };
        var step = new DockerPull { Ref = "img:latest" };

        Assert.Null(step.Exec(c));
        Assert.False(c.ShortCircuit);
    }

    [Fact]
    public void DockerPull_EmptyRepo_SkipsGuard_ProceedsNormally() {
        var gitCalled = false;

        (string, bool) Run(string name, string[] args) {
            if (name == "git") { gitCalled = true; return ("", false); }
            if (args[0] == "pull") return ("Status: Downloaded newer image for img:latest\n", true);
            if (args[0] == "image" && args[1] == "inspect") {
                if (args[3].StartsWith("{{.Id}}", StringComparison.Ordinal)) return ("sha256:someid <no value>", true);
                if (args[3].StartsWith("{{index", StringComparison.Ordinal)) return ("somerev", true);
            }
            return ("", false);
        }

        var c = new RunContext { Repo = "", RepoUrl = "", Run = Run };
        var step = new DockerPull { Ref = "img:latest" };

        Assert.Null(step.Exec(c));
        Assert.False(c.ShortCircuit);
        Assert.False(gitCalled);
    }

    [Fact]
    public void DockerPull_TwoContainers_OneStale_ShortCircuitEndsFalse() {
        var containerImage = new Dictionary<string, string> {
            ["a"] = "sha256:new",
            ["b"] = "sha256:old-b",
        };

        (string, bool) Run(string name, string[] args) {
            if (name != "docker") return ("", false);
            if (args[0] == "inspect" && args[2] == "{{.Image}}")
                return (containerImage[args[3]], true);
            if (args[0] == "pull")
                return ("Image is up to date for img:latest\n", true);
            if (args[0] == "image" && args[1] == "inspect" && args[3] == "{{.Id}}")
                return ("sha256:new", true);
            if (args[0] == "image" && args[1] == "inspect")
                return ("sha256:new <no value>", true);
            return ("", false);
        }

        var c = new RunContext { Repo = "", RepoUrl = "", Run = Run };
        var a = new DockerPull { Ref = "img:latest", Container = "a" };
        var b = new DockerPull { Ref = "img:latest", Container = "b" };

        Assert.Null(a.Exec(c));
        Assert.True(c.ShortCircuit);

        Assert.Null(b.Exec(c));
        Assert.False(c.ShortCircuit);
    }

    [Fact]
    public void DockerPull_TwoContainers_BothCurrent_ShortCircuitStaysTrue() {
        var containerImage = new Dictionary<string, string> {
            ["a"] = "sha256:new",
            ["b"] = "sha256:new",
        };

        (string, bool) Run(string name, string[] args) {
            if (name != "docker") return ("", false);
            if (args[0] == "inspect" && args[2] == "{{.Image}}")
                return (containerImage[args[3]], true);
            if (args[0] == "pull")
                return ("Image is up to date for img:latest\n", true);
            if (args[0] == "image" && args[1] == "inspect" && args[3] == "{{.Id}}")
                return ("sha256:new", true);
            if (args[0] == "image" && args[1] == "inspect")
                return ("sha256:new <no value>", true);
            return ("", false);
        }

        var c = new RunContext { Repo = "", RepoUrl = "", Run = Run };
        var a = new DockerPull { Ref = "img:latest", Container = "a" };
        var b = new DockerPull { Ref = "img:latest", Container = "b" };

        Assert.Null(a.Exec(c));
        Assert.Null(b.Exec(c));
        Assert.True(c.ShortCircuit);
    }
}
