namespace EggIdentity.Visits;

public sealed record VisitsDaySnapshot(
    DateOnly Day,
    long Visits,
    long Visitors,
    long Pageviews,
    long DurationSeconds,
    bool Capped,
    IReadOnlyDictionary<string, long> Paths) {
    public bool IsEmpty => Visits == 0 && Visitors == 0 && Pageviews == 0 && DurationSeconds == 0 && !Capped;
}
