using EggIdentity.Contract;

namespace EggIdentity.Metrics;

public interface ITrafficSource {
    Task<TrafficSnapshot> GetSnapshotAsync(CancellationToken ct);
}
