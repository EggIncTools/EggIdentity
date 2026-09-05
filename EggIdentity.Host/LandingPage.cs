using EggIdentity.Icons;

namespace EggIdentity.Host;

public static class LandingPage {
    private static readonly string IconGithub = IconPack.Get("brand-github") ?? "";
    private static readonly string IconDiscord = IconPack.Get("brand-discord") ?? "";
    private static readonly string IconLaunch = IconPack.Get("external-link") ?? "";
    private static readonly string IconDoc = IconPack.Get("file-text") ?? "";

    private const string LinksStyle = """
        <style>
            .links { display: flex; flex-direction: column; gap: 8px; margin-top: 8px; }
            .links a {
                display: flex;
                align-items: center;
                gap: 12px;
                padding: 12px 16px;
                background: #161a24;
                border: 1px solid #262c3a;
                border-radius: 8px;
                color: #d7dbe3;
                text-decoration: none;
                transition: border-color 0.15s, background 0.15s;
            }
            .links a:hover { border-color: #3a4257; background: #1a1f2c; color: #f3f5f9; }
            .links a .icon { flex-shrink: 0; display: flex; width: 18px; height: 18px; color: #7aa2ff; }
        </style>
        """;

    public static readonly string Html = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>EggIdentity</title>
        {LegalPages.Style}
        {LinksStyle}
        </head>
        <body>
        <main>
            <h1>EggIdentity</h1>

            <div class="disclaimer">
                EggIdentity, like the apps it serves, is an independent, fan-made project and is not
                affiliated with, endorsed by, or sponsored by Auxbrain Inc. Egg, Inc. and related marks
                are the property of their respective owners.
            </div>

            <div class="links">
                <a href="https://eggledger.davidarthurcole.me" target="_blank" rel="noopener"><span class="icon">{IconLaunch}</span>EggLedger</a>
                <a href="https://eggincognito.davidarthurcole.me" target="_blank" rel="noopener"><span class="icon">{IconLaunch}</span>EggIncognito</a>
                <a href="https://github.com/EggIncTools/eggidentity" target="_blank" rel="noopener"><span class="icon">{IconGithub}</span>GitHub repository</a>
                <a href="https://discord.davidarthurcole.me" target="_blank" rel="noopener"><span class="icon">{IconDiscord}</span>Discord server</a>
                <a href="/terms"><span class="icon">{IconDoc}</span>Terms of Service</a>
                <a href="/privacy"><span class="icon">{IconDoc}</span>Privacy &amp; Cookies</a>
            </div>
        </main>
        </body>
        </html>
        """;
}
