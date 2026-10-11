namespace EggIdentity.Visits;

public interface IVisitsSink {
    Task UpsertDayAsync(string site, VisitsDaySnapshot snapshot, CancellationToken ct = default);

    Task<VisitsDayState> ClaimDayAsync(string site, DateOnly day, byte[] salt, CancellationToken ct = default);
}

public sealed record VisitsDayState(byte[] Salt, IReadOnlyDictionary<string, long> LastSeen);
