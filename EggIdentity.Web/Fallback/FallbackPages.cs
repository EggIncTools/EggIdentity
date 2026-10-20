using System.Net;
using System.Text;
using EggIdentity.Styles;

namespace EggIdentity.Fallback;

public static class FallbackPages {
    public static string RenderError(FallbackBranding branding, Exception exception, bool showTrace, string faultId) {
        var trace = showTrace ? $"""
            <pre class="trace">{WebUtility.HtmlEncode(exception.ToString())}</pre>
            """ : "";
        return Page(branding, "Something broke", $"""
            <h1>This part stopped working</h1>
            <p class="hint">Reloading usually fixes it. If not, quote the reference below.</p>
            <p class="actions"><a class="button" href="">Reload</a> <a href="/">Home</a></p>
            <p class="correlation">Reference <code>{WebUtility.HtmlEncode(faultId)}</code></p>
            {trace}
            """);
    }

    public static string RenderMaintenance(FallbackBranding branding) =>
        Page(branding, "Maintenance", $"""
            <h1>{WebUtility.HtmlEncode(branding.AppName)} is down for maintenance</h1>
            <p class="hint">Back shortly.</p>
            """);

    public static string RenderNotFound(FallbackBranding branding) =>
        Page(branding, "Not found", """
            <h1>Page not found</h1>
            <p class="hint">The link may be old or mistyped.</p>
            <p class="actions"><a href="/">Home</a></p>
            """);

    public static string RenderMaintenanceAdmin(FallbackBranding branding, bool isOn) {
        var action = isOn ? "off" : "on";
        var label = isOn ? "Turn maintenance off" : "Turn maintenance on";
        return Page(branding, "Maintenance admin", $"""
            <h1>{WebUtility.HtmlEncode(branding.AppName)} maintenance</h1>
            <p>Maintenance mode is currently {(isOn ? "on" : "off")}.</p>
            <form method="post" action="/admin/maintenance/{action}">
            <button type="submit">{WebUtility.HtmlEncode(label)}</button>
            </form>
            """);
    }

    public static string RenderDown(FallbackBranding branding, bool showAdminLink, string logsUrl) {
        var link = showAdminLink
            ? $"""<p><a href="{WebUtility.HtmlEncode(logsUrl)}">View crash details</a></p>"""
            : "";
        return Page(branding, "Service unavailable", $"""
            <h1>{WebUtility.HtmlEncode(branding.AppName)} is unavailable</h1>
            <p>We're aware and looking into it.</p>
            {link}
            """);
    }

    private static string Page(FallbackBranding branding, string title, string body) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{WebUtility.HtmlEncode(branding.AppName)} - {WebUtility.HtmlEncode(title)}</title>
        {StyleBlock(branding)}
        </head>
        <body>
        <main>
        {body}
        </main>
        </body>
        </html>
        """;

    private static readonly System.Text.RegularExpressions.Regex SafeTokenValue =
        new(@"^[#A-Za-z0-9(),.\s%-]{1,64}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string StyleBlock(FallbackBranding branding) {
        var vars = new StringBuilder();
        foreach (var name in ComponentTokens.Required) {
            if (branding.Tokens.TryGetValue(name, out var value) && SafeTokenValue.IsMatch(value))
                vars.Append(name).Append(':').Append(value).Append(';');
        }
        foreach (var name in ComponentTokens.Optional) {
            if (branding.Tokens.TryGetValue(name, out var value) && SafeTokenValue.IsMatch(value))
                vars.Append(name).Append(':').Append(value).Append(';');
        }
        return $$"""
            <style>
                :root { {{vars}} }
                body { margin: 0; background: var(--color-bg); color: var(--color-fg); font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
                main { max-width: 640px; margin: 0 auto; padding: 64px 24px; text-align: center; }
                h1 { font-size: 1.4rem; }
                .hint { color: var(--color-muted); }
                .button { display: inline-block; padding: 6px 14px; border-radius: 6px; background: var(--color-accent); color: var(--color-bg); text-decoration: none; }
                .trace { text-align: left; }
                code { font-family: ui-monospace, monospace; background: var(--color-panel); padding: 2px 6px; border-radius: 4px; }
                .trace { white-space: pre-wrap; background: var(--color-panel); border: 1px solid var(--color-border); border-radius: 8px; padding: 16px; overflow-x: auto; }
                .correlation { color: var(--color-muted); }
                a { color: var(--color-accent); }
            </style>
            """;
    }
}
