namespace EggIdentity.Host;

public static class LandingPage {
    private const string IconGithub = """<svg width="18" height="18" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0016 8c0-4.42-3.58-8-8-8z"/></svg>""";
    private const string IconDiscord = """<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M20.32 4.37a19.8 19.8 0 00-4.89-1.52.07.07 0 00-.08.04c-.21.38-.45.87-.61 1.26a18.3 18.3 0 00-5.48 0 12.6 12.6 0 00-.63-1.26.08.08 0 00-.08-.04c-1.72.3-3.37.82-4.89 1.52a.07.07 0 00-.03.03C.53 9.05-.32 13.58.1 18.05a.08.08 0 00.03.06 19.9 19.9 0 006 3.03.08.08 0 00.08-.03c.46-.63.87-1.29 1.23-1.99a.07.07 0 00-.04-.1 13.1 13.1 0 01-1.87-.9.08.08 0 01-.01-.13c.13-.09.25-.19.37-.28a.07.07 0 01.08-.01c3.93 1.8 8.18 1.8 12.06 0a.07.07 0 01.08.01c.12.1.24.19.37.29a.08.08 0 010 .13c-.6.35-1.22.65-1.87.9a.08.08 0 00-.04.1c.37.7.78 1.36 1.23 1.98a.08.08 0 00.08.03 19.8 19.8 0 006.01-3.03.08.08 0 00.03-.06c.5-5.18-.84-9.67-3.55-13.65a.06.06 0 00-.03-.03zM8.02 15.33c-1.18 0-2.16-1.08-2.16-2.42 0-1.33.96-2.42 2.16-2.42 1.21 0 2.18 1.1 2.16 2.42 0 1.34-.96 2.42-2.16 2.42zm7.97 0c-1.18 0-2.16-1.08-2.16-2.42 0-1.33.96-2.42 2.16-2.42 1.21 0 2.18 1.1 2.16 2.42 0 1.34-.95 2.42-2.16 2.42z"/></svg>""";
    private const string IconLaunch = """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6"/><path d="M15 3h6v6"/><path d="M10 14L21 3"/></svg>""";
    private const string IconDoc = """<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><path d="M14 2v6h6"/></svg>""";

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
            .links a .icon { flex-shrink: 0; display: flex; color: #7aa2ff; }
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
                <a href="https://github.com/DavidArthurCole/eggidentity" target="_blank" rel="noopener"><span class="icon">{IconGithub}</span>GitHub repository</a>
                <a href="https://discord.davidarthurcole.me" target="_blank" rel="noopener"><span class="icon">{IconDiscord}</span>Discord server</a>
                <a href="/terms"><span class="icon">{IconDoc}</span>Terms of Service</a>
                <a href="/privacy"><span class="icon">{IconDoc}</span>Privacy &amp; Cookies</a>
            </div>
        </main>
        </body>
        </html>
        """;
}
