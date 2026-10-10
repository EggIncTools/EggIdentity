namespace EggIdentity.Contract;

public sealed record VisitsDay(DateOnly Day, long Visits, long Visitors, long Pageviews, long DurationSeconds, bool Capped);

public sealed record VisitsPath(string Path, long Pageviews);

public sealed record VisitsSummary(
    string Site,
    int Days,
    long Visitors,
    long Visits,
    long Pageviews,
    long DurationSeconds,
    bool Capped,
    IReadOnlyList<VisitsDay> ByDay,
    IReadOnlyList<VisitsPath> TopPaths);
