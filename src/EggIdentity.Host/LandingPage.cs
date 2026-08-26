namespace EggIdentity.Host;

public static class LandingPage {
    public static readonly string Html = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>EggIdentity</title>
        {LegalPages.Style}
        </head>
        <body>
        <main>
            <h1>EggIdentity</h1>
            <p class="updated">Shared login and session backbone for davidarthurcole.me's Egg, Inc. tools.</p>

            <div class="disclaimer">
                EggIdentity is an independent, fan-made project and is not affiliated with, endorsed by,
                or sponsored by Auxbrain Inc. Egg, Inc. and related marks are the property of their
                respective owners.
            </div>

            <section>
                <h2>Apps</h2>
                <ul>
                    <li><a href="https://eggledger.davidarthurcole.me" target="_blank" rel="noopener">EggLedger</a></li>
                    <li><a href="https://eggincognito.davidarthurcole.me" target="_blank" rel="noopener">EggIncognito</a></li>
                </ul>
            </section>

            <section>
                <h2>Resources</h2>
                <ul>
                    <li><a href="https://github.com/DavidArthurCole/eggidentity" target="_blank" rel="noopener">GitHub repository</a></li>
                    <li><a href="https://discord.davidarthurcole.me" target="_blank" rel="noopener">Discord server</a></li>
                    <li><a href="/terms">Terms of Service</a></li>
                    <li><a href="/privacy">Privacy &amp; Cookies</a></li>
                </ul>
            </section>
        </main>
        </body>
        </html>
        """;
}
