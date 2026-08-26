using EggIdentity.Agent;

namespace EggIdentity.Agent.Tests;

public class DockerLogReaderTests {
    [Fact]
    public async Task TailAsync_UnknownContainer_ReturnsNonEmptyDiagnosticString() {
        var output = await DockerLogReader.TailAsync("definitely-not-a-real-container-name", 10, CancellationToken.None);

        Assert.NotNull(output);
        Assert.NotEmpty(output);
    }
}
