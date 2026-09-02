using System.Security.Cryptography;
using System.Text;
using EggIdentity.Auth;
using EggIdentity.Db;
using EggIdentity.Fallback;
using EggIdentity.Host.Components;
using EggIdentity.Settings.Store;

namespace EggIdentity.Host;

public static class Program {
    public static async Task Main(string[] args) {
        var config = HostConfig.FromEnvironment();

        var builder = WebApplication.CreateBuilder(args);
        var runtime = HostServices.Register(builder, config);

        var app = builder.Build();
        ConfigurePipeline(app, config);
        await InitializeAsync(app, config, runtime);
        MapRoutes(app, config);

        await app.RunAsync();
    }

    private static void ConfigurePipeline(WebApplication app, HostConfig config) {
        app.UseStaticFiles();

        if (config.AdminEnabled) {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
        }

        app.UseEggIdentityFallback();
        IdentityApiRoutes.UseBearerGate(app, config);
    }

    private static async Task InitializeAsync(WebApplication app, HostConfig config, HostRuntime runtime) {
        await using (var conn = await runtime.DataSource.OpenConnectionAsync())
            await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));

        await runtime.SettingsStore.MigrateAsync();

        var stopping = app.Lifetime.ApplicationStopping;
        _ = new SettingsChangeListener(runtime.DataSource, runtime.SettingsCache).RunAsync(stopping);
        _ = new ExpiredRowSweeper(runtime.DataSource, TimeSpan.FromMinutes(config.SweepIntervalMinutes)).RunAsync(stopping);
    }

    private static void MapRoutes(WebApplication app, HostConfig config) {
        var sponsorSync = config.SponsorEnabled ? app.Services.GetRequiredService<SponsorSyncService>() : null;

        app.MapGet("/", () => Results.Content(LandingPage.Html, "text/html"));
        app.MapGet("/privacy", () => Results.Content(LegalPages.Privacy, "text/html"));
        app.MapGet("/terms", () => Results.Content(LegalPages.Terms, "text/html"));

        LoginRoutes.Map(app, config, sponsorSync);

        if (config.ProfileEnabled) ProfileLinkRoutes.Map(app, config);
        if (config.SponsorEnabled) SponsorRoutes.Map(app, config, sponsorSync!);

        IdentityApiRoutes.Map(app, config);

        if (config.AdminEnabled) app.MapRazorComponents<AppHost>().AddInteractiveServerRenderMode();
    }

    public const string IdHintCookie = "eggidentity_idhint";

    public static readonly string[] KnownProviders = ["discord", "google", "microsoft", "github"];

    public static bool IsValidLocalKey(string? configured, string? presented) {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(presented)) return false;
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        return CryptographicOperations.FixedTimeEquals(configuredHash, presentedHash);
    }

    public static bool ShouldThrottleSponsorRefresh(DateTimeOffset? lastSyncedAt, DateTimeOffset now) =>
        lastSyncedAt is { } last && now - last < TimeSpan.FromSeconds(30);

    public static async Task TrySyncSourceIdentitiesAsync(
        IdentityResolver resolver, Guid userId, AuthentikTokenResult token, CancellationToken ct) {
        try {
            await resolver.SyncSourceIdentitiesAsync(userId, token.PerSourceIds, ct);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            Console.Error.WriteLine($"source identity sync failed for {userId}: {exc.Message}");
        }
    }

    public static (Guid UserId, string? Provider) ParseLinkMode(string mode) {
        var body = mode["link:".Length..];
        var separator = body.IndexOf(':');
        return separator < 0
            ? (Guid.Parse(body), null)
            : (Guid.Parse(body[..separator]), body[(separator + 1)..]);
    }

    public static string ComputeLinkFlag(string? requestedProvider, LinkOutcome authentikOutcome, IReadOnlyList<(string Provider, LinkOutcome Outcome)> sourceOutcomes) {
        if (requestedProvider is not null) {
            var requested = sourceOutcomes.FirstOrDefault(o => o.Provider == requestedProvider);
            if (requested.Provider is not null) {
                if (requested.Outcome.Conflict) return $"linkConflict={requestedProvider}";
                if (requested.Outcome.NotAvailable) return $"linkUnavailable={requestedProvider}";
                if (requested.Outcome.AlreadyLinked) return $"linkRejected={requestedProvider}";
                if (requested.Outcome.Linked) return "linked=ok";
            }
            return "linkError=1";
        }

        var conflict = sourceOutcomes.FirstOrDefault(o => o.Outcome.Conflict);
        if (conflict.Provider is not null) return $"linkConflict={conflict.Provider}";
        if (authentikOutcome.Conflict) return "linkConflict=authentik";
        var rejected = sourceOutcomes.FirstOrDefault(o => o.Outcome.AlreadyLinked);
        if (rejected.Provider is not null) return $"linkRejected={rejected.Provider}";
        if (authentikOutcome.AlreadyLinked) return "linkRejected=authentik";
        if (authentikOutcome.Linked || sourceOutcomes.Any(o => o.Outcome.Linked)) return "linked=ok";
        return "linkError=1";
    }

    public static string BuildEndSessionUrl(string endSessionEndpoint, string? idTokenHint, string? returnUrl) {
        var url = endSessionEndpoint;
        if (!string.IsNullOrEmpty(idTokenHint))
            url = Append(url, $"id_token_hint={Uri.EscapeDataString(idTokenHint)}");
        if (!string.IsNullOrEmpty(returnUrl))
            url = Append(url, $"post_logout_redirect_uri={Uri.EscapeDataString(returnUrl)}");
        return url;

        static string Append(string target, string param) {
            var sep = target.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{target}{sep}{param}";
        }
    }

    public static string ValidateMode(string? raw) =>
        raw is "inline" or "redirect" ? raw : "popup";

    public static AppAuthConfig? ResolveApp(string returnUrl, Dictionary<string, AppAuthConfig> appConfigs) {
        if (string.IsNullOrEmpty(returnUrl) || !Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsed))
            return null;
        var origin = $"{parsed.Scheme}://{parsed.Authority}";
        return appConfigs.TryGetValue(origin, out var app) ? app : null;
    }

    public static string BuildFlowUrl(string authority, string provider, string authorizeUrl) {
        var flowSlug = $"{provider}-only-auth";
        return $"{authority}/if/flow/{flowSlug}/?next={Uri.EscapeDataString(authorizeUrl)}";
    }

    public static string BuildRelinkContinueUrl(string callbackUrl) =>
        callbackUrl.EndsWith("/callback", StringComparison.Ordinal)
            ? callbackUrl[..^"/callback".Length] + "/relink/continue"
            : callbackUrl.TrimEnd('/') + "/relink/continue";

    public static string BuildRelinkLogoutUrl(string endSessionUrl, string idTokenHint, string continueUrl, string flowUrl) {
        var url = Append(endSessionUrl, $"id_token_hint={Uri.EscapeDataString(idTokenHint)}");
        url = Append(url, $"post_logout_redirect_uri={Uri.EscapeDataString(continueUrl)}");
        url = Append(url, $"state={Uri.EscapeDataString(flowUrl)}");
        return url;

        static string Append(string target, string param) {
            var sep = target.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{target}{sep}{param}";
        }
    }

    public static bool IsAllowedRelinkTarget(string? target, string authority, string[] knownProviders) {
        if (string.IsNullOrEmpty(target)) return false;
        if (!Uri.TryCreate(target, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) return false;
        var trimmed = authority.TrimEnd('/');
        if (!knownProviders.Any(provider => target.StartsWith($"{trimmed}/if/flow/{provider}-only-auth/", StringComparison.Ordinal)))
            return false;

        var next = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(parsed.Query).TryGetValue("next", out var values)
            ? values.ToString()
            : null;
        return !string.IsNullOrEmpty(next) && next.StartsWith($"{trimmed}/application/o/authorize/", StringComparison.Ordinal);
    }

    public static string BuildRedirectCallbackUrl(string returnUrl, string? code, string? error) {
        var param = code is not null ? $"code={Uri.EscapeDataString(code)}" : $"error={Uri.EscapeDataString(error!)}";
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{returnUrl}{separator}{param}";
    }

    public static string AppendQuery(string returnUrl, string param) {
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{returnUrl}{separator}{param}";
    }
}
