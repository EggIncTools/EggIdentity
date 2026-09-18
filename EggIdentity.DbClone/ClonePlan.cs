namespace EggIdentity.DbClone;

public enum ClonePolicy {
    Full,
    Scrub,
    SchemaOnly,
    Skip,
}

public sealed record TablePolicy(string Table, ClonePolicy Policy) {
    public string? ScrubSql { get; init; }
    public string? VerifySql { get; init; }
    public string? Where { get; init; }
    public IReadOnlyList<string> UserIdColumns { get; init; } = [];

    public bool Copies => Policy is ClonePolicy.Full or ClonePolicy.Scrub;

    public bool Truncates => Policy != ClonePolicy.Skip;
}

public sealed record ClonePlan(string App, string TargetDatabase, IReadOnlyList<TablePolicy> Tables) {
    public IReadOnlyList<string> IgnoredTables { get; init; } = [];

    public TablePolicy? Find(string table) =>
        Tables.FirstOrDefault(t => string.Equals(t.Table, table, StringComparison.Ordinal));
}
