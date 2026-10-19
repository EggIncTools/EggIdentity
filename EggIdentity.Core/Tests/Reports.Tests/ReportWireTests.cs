using EggIdentity.Filters;

using static EggIdentity.Reports.Tests.ReportFixture;

namespace EggIdentity.Reports.Tests;

public class ReportWireTests {
    private const string DropsSource = "drops";

    private static readonly SnapshotSchema Grain = new(DropsSource, [new GroupKey("ship"), new GroupKey("item"), new GroupKey("rarity"), new GroupKey("at", TimeBucket.Day)], ["airtime", "value", "mission"]);

    [Fact]
    public async Task Cube_RoundTripsColumnar() {
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship"), new GroupKey("item")],
            Measures = [new MeasureRequest("value", Aggregation.Sum), new MeasureRequest("airtime", Aggregation.Avg)],
        };
        var cube = await Run(query);
        Assert.True(cube.Rows.Count > 0);

        var json = ReportWire.WriteCube(cube);
        var roundTripped = ReportWire.ReadCube(json);

        Assert.Equal(cube.Dimensions, roundTripped.Dimensions);
        Assert.Equal(cube.Measures, roundTripped.Measures);
        Assert.Equal(cube.Normalized, roundTripped.Normalized);
        Assert.Equal(cube.Rows.Count, roundTripped.Rows.Count);
        for (var i = 0; i < cube.Rows.Count; i++) {
            Assert.Equal(cube.Rows[i].Keys, roundTripped.Rows[i].Keys);
            Assert.Equal(cube.Rows[i].Values, roundTripped.Rows[i].Values);
            Assert.Equal(cube.Rows[i].Samples, roundTripped.Rows[i].Samples);
        }
    }

    [Fact]
    public void Snapshot_RoundTripsBinary() {
        var columns = new List<SnapshotColumn> {
            NumberColumn("airtime", [100.0, null, 50.0]),
            DateColumn("at", [1000L, null, 2000L]),
            BoolColumn("flag", [true, false, null]),
            TextColumn("ship", ["henerprise", null, "atreggies"]),
            ManyColumn("tags", [["a", "b"], [], null]),
        };
        var snapshot = new RowSnapshot(DropsSource, "v1", 3, columns);

        var bytes = ReportWire.WriteSnapshot(snapshot);
        var roundTripped = ReportWire.ReadSnapshot(bytes);

        Assert.Equal(snapshot.Source, roundTripped.Source);
        Assert.Equal(snapshot.Version, roundTripped.Version);
        Assert.Equal(snapshot.Rows, roundTripped.Rows);
        foreach (var column in snapshot.Columns) {
            var other = roundTripped.Column(column.Key) ?? throw new Xunit.Sdk.XunitException($"missing column {column.Key}");
            for (var row = 0; row < snapshot.Rows; row++)
                Assert.Equal(column.At(row), other.At(row));
        }
    }

    [Fact]
    public void Snapshot_LargeRoundTripIsCompact() {
        const int rows = 100_000;
        const int columns = 10;
        var generated = new List<SnapshotColumn>(columns);
        for (var c = 0; c < columns; c++) {
            var values = new double?[rows];
            for (var r = 0; r < rows; r++) values[r] = r % 37 == 0 ? null : r + c;
            generated.Add(NumberColumn($"col{c}", values));
        }
        var snapshot = new RowSnapshot(DropsSource, "v-large", rows, generated);

        var bytes = ReportWire.WriteSnapshot(snapshot);
        Assert.True(bytes.Length < rows * columns * 9);

        var roundTripped = ReportWire.ReadSnapshot(bytes);
        Assert.Equal(snapshot.Rows, roundTripped.Rows);
        for (var c = 0; c < columns; c++) {
            var column = roundTripped.Column($"col{c}") ?? throw new Xunit.Sdk.XunitException("missing column");
            for (var r = 0; r < rows; r += 997)
                Assert.Equal(generated[c].At(r), column.At(r));
        }
    }

    [Fact]
    public async Task Registry_RunsSnapshotTierOverSnapshotSource() {
        var (descriptor, _) = SourceDescriptor.Capture(Source);
        var snapshot = RowSnapshot.Capture(Source, Rows, "v1");
        var registry = new ReportEngineRegistry().AddSnapshotSource(descriptor, snapshot, Grain, new FixedClock(Now));

        var query = new ReportQuery(DropsSource) { GroupBy = [new GroupKey("ship")] };
        var liveCube = await registry.RunAsync(query, ReportContext.Utc, CancellationToken.None);
        var snapshotCube = await registry.RunAsync(query, new ReportContext(TimeZoneInfo.Utc, ReportTier.Snapshot), CancellationToken.None);

        Assert.Equal(liveCube.Rows.Count, snapshotCube.Rows.Count);
        foreach (var liveRow in liveCube.Rows) {
            var snapshotRow = snapshotCube.Rows.Single(r => r.Keys.SequenceEqual(liveRow.Keys, StringComparer.Ordinal));
            Assert.Equal(liveRow.Values, snapshotRow.Values);
        }
    }

    [Fact]
    public async Task Registry_EnforcesCapsOverSnapshotSource() {
        var (descriptor, _) = SourceDescriptor.Capture(Source);
        var snapshot = RowSnapshot.Capture(Source, Rows, "v1");
        var registry = new ReportEngineRegistry().AddSnapshotSource(descriptor, snapshot, Grain, new FixedClock(Now));
        registry.SetCaps(ReportTier.Live, new ReportTierCaps(int.MaxValue, 1, int.MaxValue));

        var query = new ReportQuery(DropsSource) { GroupBy = [new GroupKey("ship")] };
        var exception = await Assert.ThrowsAsync<ReportRejectedException>(() => registry.RunAsync(query, ReportContext.Utc, CancellationToken.None));
        Assert.Contains(exception.Rejections, r => r.Kind == ReportRejectionKind.CellCap);
    }

    [Fact]
    public void Registry_RejectsSnapshotForAnotherSource() {
        var (descriptor, _) = SourceDescriptor.Capture(Source);
        var snapshot = new RowSnapshot("other-source", "v1", 0, []);
        var registry = new ReportEngineRegistry();
        Assert.Throws<ArgumentException>(() => registry.AddSnapshotSource(descriptor, snapshot, null, new FixedClock(Now)));
    }

    private static SnapshotColumn NumberColumn(string key, IReadOnlyList<double?> values) {
        var column = new SnapshotColumn(key, FilterValueKind.Number, values.Count) { Numbers = new double[values.Count] };
        for (var i = 0; i < values.Count; i++) {
            if (values[i] is { } v) {
                SetPresent(column.Present, i);
                column.Numbers![i] = v;
            }
        }
        return column;
    }

    private static SnapshotColumn DateColumn(string key, IReadOnlyList<long?> values) {
        var column = new SnapshotColumn(key, FilterValueKind.Date, values.Count) { Dates = new long[values.Count] };
        for (var i = 0; i < values.Count; i++) {
            if (values[i] is { } v) {
                SetPresent(column.Present, i);
                column.Dates![i] = v;
            }
        }
        return column;
    }

    private static SnapshotColumn BoolColumn(string key, IReadOnlyList<bool?> values) {
        var column = new SnapshotColumn(key, FilterValueKind.Bool, values.Count) { Bools = new bool[values.Count] };
        for (var i = 0; i < values.Count; i++) {
            if (values[i] is { } v) {
                SetPresent(column.Present, i);
                column.Bools![i] = v;
            }
        }
        return column;
    }

    private static SnapshotColumn TextColumn(string key, IReadOnlyList<string?> values) {
        var dictionary = values.Where(v => v is not null).Select(v => v!).Distinct(StringComparer.Ordinal).ToArray();
        var column = new SnapshotColumn(key, FilterValueKind.Text, values.Count) { Dictionary = dictionary, Text = new int[values.Count] };
        for (var i = 0; i < values.Count; i++) {
            if (values[i] is { } v) {
                SetPresent(column.Present, i);
                column.Text![i] = Array.IndexOf(dictionary, v);
            }
        }
        return column;
    }

    private static SnapshotColumn ManyColumn(string key, IReadOnlyList<IReadOnlyList<string>?> values) {
        var dictionary = values.Where(v => v is not null).SelectMany(v => v!).Distinct(StringComparer.Ordinal).ToArray();
        var offsets = new int[values.Count + 1];
        var items = new List<int>();
        for (var i = 0; i < values.Count; i++) {
            offsets[i] = items.Count;
            if (values[i] is { } v) items.AddRange(v.Select(item => Array.IndexOf(dictionary, item)));
        }
        offsets[values.Count] = items.Count;
        var column = new SnapshotColumn(key, FilterValueKind.Set, values.Count) { Dictionary = dictionary, ManyOffsets = offsets, ManyItems = [.. items] };
        for (var i = 0; i < values.Count; i++)
            if (values[i] is not null) SetPresent(column.Present, i);
        return column;
    }

    private static void SetPresent(ulong[] bitmap, int row) => bitmap[row >> 6] |= 1UL << (row & 63);
}
