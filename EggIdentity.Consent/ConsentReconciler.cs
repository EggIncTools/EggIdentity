namespace EggIdentity.Consent;

public sealed record ConsentReconciliation(ConsentState? Winner, bool WriteCookie, bool WriteServer);

public static class ConsentReconciler {
    public static ConsentReconciliation Reconcile(ConsentState? cookie, ConsentState? server) {
        if (cookie is null && server is null) return new ConsentReconciliation(null, false, false);
        if (server is null) return new ConsentReconciliation(cookie, false, true);
        if (cookie is null) return new ConsentReconciliation(server, true, false);
        if (server.DecidedAt > cookie.DecidedAt) return new ConsentReconciliation(server, true, false);
        return cookie.DecidedAt > server.DecidedAt
            ? new ConsentReconciliation(cookie, false, true)
            : new ConsentReconciliation(cookie, false, false);
    }
}
