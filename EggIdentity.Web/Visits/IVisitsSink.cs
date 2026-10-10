namespace EggIdentity.Visits;

public interface IVisitsSink {
    Task UpsertDayAsync(string site, VisitsDaySnapshot snapshot, CancellationToken ct = default);
}
