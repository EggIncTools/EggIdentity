namespace EggIdentity.UI.Tests;

public class WorkbenchStatusTests {
    [Theory]
    [InlineData("queued", WorkbenchStatusKind.Queued)]
    [InlineData("pending", WorkbenchStatusKind.Queued)]
    [InlineData("running", WorkbenchStatusKind.Running)]
    [InlineData("run", WorkbenchStatusKind.Running)]
    [InlineData("succeeded", WorkbenchStatusKind.Done)]
    [InlineData("failed", WorkbenchStatusKind.Error)]
    [InlineData("offerable", WorkbenchStatusKind.Info)]
    [InlineData("QuEuEd", WorkbenchStatusKind.Queued)]
    public void Parse_RecognizesKnownValuesCaseInsensitively(string value, WorkbenchStatusKind expected) {
        Assert.Equal(expected, WorkbenchStatus.Parse(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unrecognized")]
    public void Parse_FallsBackToMuted(string? value) {
        Assert.Equal(WorkbenchStatusKind.Muted, WorkbenchStatus.Parse(value));
    }

    [Theory]
    [InlineData(WorkbenchStatusKind.Queued, "wb-st-queued")]
    [InlineData(WorkbenchStatusKind.Running, "wb-st-run")]
    [InlineData(WorkbenchStatusKind.Done, "wb-st-done")]
    [InlineData(WorkbenchStatusKind.Error, "wb-st-err")]
    [InlineData(WorkbenchStatusKind.Info, "wb-st-offer")]
    [InlineData(WorkbenchStatusKind.Muted, "wb-st-muted")]
    public void Class_MapsEachKindToItsCssClass(WorkbenchStatusKind kind, string expected) {
        Assert.Equal(expected, WorkbenchStatus.Class(kind));
    }
}
