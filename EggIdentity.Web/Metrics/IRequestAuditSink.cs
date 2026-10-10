namespace EggIdentity.Metrics;

public interface IRequestAuditSink {
    Task RecordAsync(AuditEntry entry, CancellationToken ct);
}
