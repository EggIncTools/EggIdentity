using EggIdentity.Auth;
using EggIdentity.Client;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.UI.Suite;

public interface ISuiteEidStore {
    bool IsAvailable { get; }
    Task<ProfileGame?> LoadAsync(CancellationToken ct);
    Task<bool> SetActiveAsync(string? eid, string? label, CancellationToken ct);
    Task<EidUpsertResult?> UpsertAsync(IReadOnlyList<EidUpsert> entries, CancellationToken ct);
    Task<bool> UpdateSnapshotAsync(string eid, EidSnapshot snapshot, CancellationToken ct);
    Task<bool> RenameAsync(string eid, string? label, CancellationToken ct);
    Task<bool> RemoveAsync(string eid, CancellationToken ct);
    Task<bool> ReorderAsync(IReadOnlyList<string> eids, CancellationToken ct);
}

public sealed class IdentityEidStore(
    ICurrentUser user,
    IdentityApiClient? api = null,
    IHttpContextAccessor? accessor = null,
    SessionCookieOptions? session = null) : ISuiteEidStore {
    private const string DefaultSessionCookie = "eggidentity_session";

    private string? SessionToken { get; } = ReadToken(accessor, session);

    public bool IsAvailable => Token is not null && api is not null;

    private string? Token => user.Current.IsAuthenticated ? SessionToken : null;

    public async Task<ProfileGame?> LoadAsync(CancellationToken ct) =>
        Token is { } token && api is not null ? await api.GetGameAsync(token, ct) : null;

    public async Task<bool> SetActiveAsync(string? eid, string? label, CancellationToken ct) =>
        Token is { } token && api is not null && await api.SetGameAsync(token, new ProfileGame { ActiveEid = eid, ActiveEidLabel = label }, ct);

    public async Task<EidUpsertResult?> UpsertAsync(IReadOnlyList<EidUpsert> entries, CancellationToken ct) =>
        Token is { } token && api is not null ? await api.UpsertEidsAsync(token, entries, ct) : null;

    public async Task<bool> UpdateSnapshotAsync(string eid, EidSnapshot snapshot, CancellationToken ct) =>
        Token is { } token && api is not null && await api.UpdateEidSnapshotAsync(token, eid, snapshot, ct);

    public async Task<bool> RenameAsync(string eid, string? label, CancellationToken ct) =>
        Token is { } token && api is not null && await api.RenameEidAsync(token, eid, label, ct);

    public async Task<bool> RemoveAsync(string eid, CancellationToken ct) =>
        Token is { } token && api is not null && await api.RemoveEidAsync(token, eid, ct);

    public async Task<bool> ReorderAsync(IReadOnlyList<string> eids, CancellationToken ct) =>
        Token is { } token && api is not null && await api.ReorderEidsAsync(token, eids, ct);

    private static string? ReadToken(IHttpContextAccessor? accessor, SessionCookieOptions? session) {
        var name = session?.CookieName ?? DefaultSessionCookie;
        return accessor?.HttpContext?.Request.Cookies.TryGetValue(name, out var token) == true ? token : null;
    }
}
