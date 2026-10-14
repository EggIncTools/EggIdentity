using EggIdentity.Filters;

namespace EggIdentity.Reports.Tests;

internal sealed record Drop(string Mission, string Ship, string Item, int Rarity, double Airtime, double Value, DateTimeOffset At);

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider {
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class ReportFixture {
    public static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public static readonly ReportDimension Ship = new(new FilterFieldDef("ship", "Ship", FilterValueKind.Select, FilterOps.Comparison) {
        Options = [new("henerprise", "Henerprise"), new("atreggies", "Atreggies"), new("chicken_one", "Chicken One")],
    }) { Order = DimensionOrder.OptionOrder };

    public static readonly ReportDimension Item = new(new FilterFieldDef("item", "Item", FilterValueKind.Select, FilterOps.Equality) { Scope = "artifact" });

    public static readonly ReportDimension Rarity = new(new FilterFieldDef("rarity", "Rarity", FilterValueKind.Number, FilterOps.Comparison) { Scope = "artifact" });

    public static readonly ReportDimension At = new(new FilterFieldDef("at", "Launched", FilterValueKind.Date, FilterOps.Date));

    public static readonly ReportMeasure Airtime = new("airtime", "Airtime") { DistinctBy = "mission", Scopes = ["mission"] };

    public static readonly ReportMeasure Value = new("value", "Value") { Kind = MeasureKind.Distribution, Binning = 10 };

    public static readonly ReportMeasure MissionId = new("mission", "Missions") {
        Aggregations = [Aggregation.Distinct],
        Default = Aggregation.Distinct,
    };

    public static readonly ReportSource<Drop> Source = new("drops", "Drops", [Ship, Item, Rarity, At], [Airtime, Value, MissionId], Read) {
        SampleKey = "mission",
    };

    public static readonly Drop[] Rows = [
        new("m1", "henerprise", "gusset", 1, 100, 5, new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero)),
        new("m1", "henerprise", "feather", 2, 100, 15, new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero)),
        new("m2", "atreggies", "gusset", 1, 50, 25, new(2026, 10, 11, 23, 0, 0, TimeSpan.Zero)),
        new("m3", "henerprise", "gusset", 3, 80, 12, new(2026, 9, 21, 8, 0, 0, TimeSpan.Zero)),
        new("m4", "chicken_one", "feather", 1, 20, 7, new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero)),
    ];

    public static FilterSubjectValue Read(Drop d, string key) => key switch {
        "ship" => FilterSubjectValue.Of(d.Ship),
        "item" => FilterSubjectValue.Of(d.Item),
        "rarity" => FilterSubjectValue.Of((double)d.Rarity),
        "at" => FilterSubjectValue.Of(d.At),
        "airtime" => FilterSubjectValue.Of(d.Airtime),
        "value" => FilterSubjectValue.Of(d.Value),
        "mission" => FilterSubjectValue.Of(d.Mission),
        _ => FilterSubjectValue.Absent,
    };

    public static InMemoryReportEngine<Drop> Engine(IReadOnlyList<Drop>? rows = null) =>
        new(Source, _ => ValueTask.FromResult(rows ?? Rows), new FixedClock(Now));

    public static Task<ReportCube> Run(ReportQuery query, IReadOnlyList<Drop>? rows = null) =>
        Engine(rows).RunAsync(query, ReportContext.Utc, CancellationToken.None);

    public static CubeRow Row(ReportCube cube, params string[] keys) =>
        cube.Rows.Single(r => r.Keys.SequenceEqual(keys, StringComparer.Ordinal));
}
