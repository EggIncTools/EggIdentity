using System.Text.Json;
using EggIdentity.Filters;

namespace EggIdentity.Reports.Tests;

public class RemoteReportEngineTests {
    private static readonly ReportQuery Query = new("drops") { GroupBy = [new GroupKey("ship")] };

    [Fact]
    public async Task RemoteEngine_ReturnsCubeFromWire() {
        var expected = new ReportCube(["ship"], [MeasureRequest.RowCount], false, [new CubeRow(["henerprise"], [3], 3)]);
        var engine = new RemoteReportEngine((_, _, _, _) => Task.FromResult(ReportWire.WriteCube(expected)));

        var actual = await engine.RunAsync(Query, ReportContext.Utc, CancellationToken.None);

        Assert.Equal(expected.Dimensions, actual.Dimensions);
        Assert.Equal(expected.Measures, actual.Measures);
        Assert.Equal(expected.Rows.Single().Keys, actual.Rows.Single().Keys);
        Assert.Equal(expected.Rows.Single().Values, actual.Rows.Single().Values);
    }

    [Fact]
    public async Task RemoteEngine_RethrowsRejections() {
        var rejections = new[] { new ReportRejection(ReportRejectionKind.CellCap, "drops", "too many cells") };
        var payload = JsonSerializer.Serialize(rejections, ReportJsonContext.Default.ReportRejectionArray);
        var engine = new RemoteReportEngine((_, _, _, _) => throw new InvalidOperationException(payload));

        var ex = await Assert.ThrowsAsync<ReportRejectedException>(() => engine.RunAsync(Query, ReportContext.Utc, CancellationToken.None));

        Assert.Equal(rejections.Length, ex.Rejections.Count);
        Assert.Equal(rejections[0].Kind, ex.Rejections[0].Kind);
        Assert.Equal(rejections[0].Key, ex.Rejections[0].Key);
        Assert.Equal(rejections[0].Message, ex.Rejections[0].Message);
    }

    [Fact]
    public void Host_LoadWithoutDescriptorThrows() {
        var core = new ReportWorkerHostCore(new FixedClock(ReportFixture.Now));
        var snapshot = new RowSnapshot("unregistered", "v1", 0, []);

        Assert.Throws<KeyNotFoundException>(() => core.Load(ReportWire.WriteSnapshot(snapshot), null));
    }

    [Fact]
    public void Host_LoadReplacesVersion() {
        var core = new ReportWorkerHostCore(new FixedClock(ReportFixture.Now));
        var descriptor = new SourceDescriptor("drops", "Drops", [], [], [], null, null, FilterSet.Empty, []);
        core.Describe(ReportWire.WriteDescriptor(descriptor));

        core.Load(ReportWire.WriteSnapshot(new RowSnapshot("drops", "v1", 0, [])), null);
        Assert.Equal("v1", core.Version("drops"));

        core.Load(ReportWire.WriteSnapshot(new RowSnapshot("drops", "v2", 0, [])), null);
        Assert.Equal("v2", core.Version("drops"));
    }
}
