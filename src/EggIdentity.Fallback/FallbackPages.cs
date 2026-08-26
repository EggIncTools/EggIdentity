using System.Text;
using EggIdentity.Styles;

namespace EggIdentity.Fallback;

public static class FallbackPages {
    public static string RenderError(FallbackBranding branding, Exception exception, bool showTrace, string correlationId) {
        var trace = showTrace ? $"""
            <pre class="trace">{Escape(exception.ToString())}</pre>
            """ : "";
        return Page(branding, "Something broke", $"""
            <h1>{Escape(branding.AppName)} hit an error</h1>
            {trace}
            <p class="correlation">Reference id: {Escape(correlationId)}</p>
            """);
    }

    public static string RenderMaintenance(FallbackBranding branding) =>
        Page(branding, "Maintenance", $"""
            <h1>{Escape(branding.AppName)} is down for maintenance</h1>
            <p>Back shortly.</p>
            """);

    public static string RenderNotFound(FallbackBranding branding) =>
        Page(branding, "Not found", $"""
            <h1>{Escape(branding.AppName)} - page not found</h1>
            """);

    public static string RenderDown(FallbackBranding branding, bool showAdminLink, string logsUrl) {
        var link = showAdminLink
            ? $"""<p><a href="{Escape(logsUrl)}">View crash details</a></p>"""
            : "";
        return Page(branding, "Service unavailable", $"""
            <h1>{Escape(branding.AppName)} is unavailable</h1>
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
        <title>{Escape(branding.AppName)} - {Escape(title)}</title>
        {StyleBlock(branding)}
        </head>
        <body>
        <main>
        {body}
        </main>
        </body>
        </html>
        """;

    private static string StyleBlock(FallbackBranding branding) {
        var vars = new StringBuilder();
        foreach (var name in ComponentTokens.Required) {
            if (branding.Tokens.TryGetValue(name, out var value) && !value.Contains('<') && !value.Contains('>'))
                vars.Append(name).Append(':').Append(value).Append(';');
        }
        foreach (var name in ComponentTokens.Optional) {
            if (branding.Tokens.TryGetValue(name, out var value) && !value.Contains('<') && !value.Contains('>'))
                vars.Append(name).Append(':').Append(value).Append(';');
        }
        return $$"""
            <style>
                :root { {{vars}} }
                body { margin: 0; background: var(--color-bg, #000); color: var(--color-fg, #fff); font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
                main { max-width: 640px; margin: 0 auto; padding: 64px 24px; }
                h1 { font-size: 1.6rem; }
                .trace { white-space: pre-wrap; background: var(--color-panel, #111); border: 1px solid var(--color-border, #333); border-radius: 8px; padding: 16px; overflow-x: auto; }
                .correlation { color: var(--color-muted, #888); }
                a { color: var(--color-accent, #5af); }
            </style>
            """;
    }

    private static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);
}
