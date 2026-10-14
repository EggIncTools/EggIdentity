using static EggIdentity.Reports.Tests.ReportFixture;

namespace EggIdentity.Reports.Tests;

public class ReportProjectionTests {
    [Fact]
    public async Task Matrix_PlacesCellsAndLabelsFromOptions() {
        var cube = await Run(new ReportQuery("drops") { GroupBy = [new GroupKey("ship"), new GroupKey("item")] });
        var matrix = ReportProjection.Matrix(cube, MeasureRequest.RowCountKey, "ship", "item", Source);
        Assert.Equal(["Henerprise", "Atreggies", "Chicken One"], matrix.RowLabels);
        var row = matrix.RowKeys.ToList().IndexOf("atreggies");
        var col = matrix.ColKeys.ToList().IndexOf("feather");
        Assert.Null(matrix.Cells[row][col]);
        Assert.Equal(0, matrix.Samples[row][col]);
        Assert.Equal(2d, matrix.Cells[0][matrix.ColKeys.ToList().IndexOf("gusset")]);
    }

    [Fact]
    public async Task Series_CollapsesTrailingAxes() {
        var cube = await Run(new ReportQuery("drops") { GroupBy = [new GroupKey("ship"), new GroupKey("item")] });
        var series = ReportProjection.Series(cube, MeasureRequest.RowCountKey);
        Assert.Equal(3d, series.Values[series.Keys.ToList().IndexOf("henerprise")]);
    }

    [Fact]
    public async Task Series_CollapsingAvgAcrossExtraDimensionThrows() {
        var cube = await Run(new ReportQuery("drops") {
            GroupBy = [new GroupKey("ship"), new GroupKey("item")],
            Measures = [new MeasureRequest("value", Aggregation.Avg)],
        });
        var ex = Assert.Throws<InvalidOperationException>(() => ReportProjection.Series(cube, "value"));
        Assert.Contains("value", ex.Message);
        Assert.Contains("Avg", ex.Message);
    }

    [Fact]
    public async Task Series_CollapsingSumAcrossExtraDimensionSums() {
        var cube = await Run(new ReportQuery("drops") {
            GroupBy = [new GroupKey("ship"), new GroupKey("item")],
            Measures = [new MeasureRequest("value", Aggregation.Sum)],
        });
        var series = ReportProjection.Series(cube, "value");
        Assert.Equal(32d, series.Values[series.Keys.ToList().IndexOf("henerprise")]);
    }

    [Fact]
    public async Task Series_ExactArityAvgProjectionWorks() {
        var cube = await Run(new ReportQuery("drops") {
            GroupBy = [new GroupKey("ship")],
            Measures = [new MeasureRequest("value", Aggregation.Avg)],
        });
        var series = ReportProjection.Series(cube, "value");
        Assert.Equal(32d / 3d, series.Values[series.Keys.ToList().IndexOf("henerprise")]);
    }

    [Fact]
    public void Slices_RotateHueEvenlyFromTheBase() {
        var slices = ChartPalette.Slices("#ff0000", 3);
        Assert.Equal(["#ff0000", "#00ff00", "#0000ff"], slices);
        Assert.Empty(ChartPalette.Slices("red", 3));
    }

    [Fact]
    public void Slots_AreStableAcrossKeySets() {
        var a = ChartPalette.Slots(["gusset", "feather"], 8);
        var b = ChartPalette.Slots(["tachyon", "feather", "gusset"], 8);
        Assert.Equal(a["gusset"], b["gusset"]);
        Assert.Equal(a["feather"], b["feather"]);
        Assert.InRange(a["gusset"], 1, 8);
    }
}
