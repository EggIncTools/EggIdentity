namespace EggIdentity.Deploy;

public interface ISessionTokenSource {
    ValueTask<string?> GetAsync(CancellationToken ct);
}
