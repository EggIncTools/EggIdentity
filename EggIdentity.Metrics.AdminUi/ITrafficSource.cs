using EggIdentity.Contract;

namespace EggIdentity.Metrics.AdminUi;

public interface ITrafficSource {
    Task<TrafficSnapshot> GetSnapshotAsync(CancellationToken ct);
}
