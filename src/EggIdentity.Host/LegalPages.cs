namespace EggIdentity.Host;

public static class LegalPages {
    private const string Style = """
        <style>
            :root { color-scheme: dark; }
            * { box-sizing: border-box; }
            body {
                margin: 0;
                background: #0b0d12;
                color: #d7dbe3;
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                line-height: 1.6;
            }
            main {
                max-width: 760px;
                margin: 0 auto;
                padding: 48px 24px 96px;
            }
            h1 {
                font-size: 1.9rem;
                margin: 0 0 4px;
                color: #f3f5f9;
            }
            .updated {
                color: #8992a4;
                font-size: 0.9rem;
                margin: 0 0 24px;
            }
            .disclaimer {
                background: #161a24;
                border: 1px solid #262c3a;
                border-radius: 8px;
                padding: 14px 18px;
                font-size: 0.9rem;
                color: #aab1c0;
                margin-bottom: 32px;
            }
            section { margin-bottom: 28px; }
            h2 {
                font-size: 1.1rem;
                color: #f3f5f9;
                margin: 0 0 10px;
            }
            p, li { color: #c3c9d4; }
            ul { padding-left: 22px; margin: 8px 0; }
            li { margin-bottom: 6px; }
            a { color: #7aa2ff; }
            a:hover { color: #9dbaff; }
            code {
                background: #161a24;
                border-radius: 4px;
                padding: 1px 6px;
                font-size: 0.9em;
            }
        </style>
        """;

    public static readonly string Privacy = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>EggIdentity - Privacy &amp; Cookies</title>
        {Style}
        </head>
        <body>
        <main>
            <h1>Privacy &amp; Cookies</h1>
            <p class="updated">Last updated 25 August 2026</p>

            <div class="disclaimer">
                EggIdentity, like the apps it serves, is an independent, fan-made project and is not
                affiliated with, endorsed by, or sponsored by Auxbrain Inc. Egg, Inc. and related marks
                are the property of their respective owners.
            </div>

            <section>
                <h2>The short version</h2>
                <p>
                    EggIdentity is the shared login and session backbone behind davidarthurcole.me's
                    Egg, Inc. tools, currently <a href="https://eggledger.davidarthurcole.me" target="_blank" rel="noopener">EggLedger</a>
                    and <a href="https://eggincognito.davidarthurcole.me" target="_blank" rel="noopener">EggIncognito</a>.
                    It exists so you can sign in once and stay signed in across those sites. It stores
                    only the identity data needed to recognise you and apply your role or perks. There is
                    no advertising, no third-party tracking, no analytics, and payment data is never
                    handled here.
                </p>
            </section>

            <section>
                <h2>What EggIdentity is</h2>
                <p>
                    EggIdentity runs at eggidentity.davidarthurcole.me and is operated by an individual,
                    David Cole, not a company. It sits between you, an OAuth provider you choose (Discord,
                    Google, Microsoft, or GitHub) via a self-hosted Authentik instance at
                    auth.davidarthurcole.me, and the app you are signing into. EggIdentity never sees or
                    stores your password; that is handled entirely by the provider and Authentik.
                </p>
            </section>

            <section>
                <h2>What is stored</h2>
                <p>When you log in, EggIdentity creates or updates a single account record holding:</p>
                <ul>
                    <li>An internal account id.</li>
                    <li>Your Discord user id, username, and avatar, if you have linked Discord.</li>
                    <li>The role assigned to your account (for example viewer, contributor, or admin) on whichever service you are using.</li>
                    <li>When the account was created and when you last logged in.</li>
                </ul>
                <p>
                    If you link more than one provider to the same account, each linked provider's id and
                    the username/avatar it reports are stored too, so EggIdentity can recognise you on any
                    of them. Linking anything beyond the first provider you log in with is optional. If you
                    upload a custom avatar through your profile, that image (up to 2&nbsp;MB, PNG/JPEG/WebP)
                    is stored keyed to your account id and served back at <code>/avatars/&#123;your-id&#125;</code>.
                </p>
            </section>

            <section>
                <h2>Single sign-on cookie</h2>
                <p>
                    Logging in to any app that uses EggIdentity sets one shared session cookie, scoped to
                    the whole davidarthurcole.me domain, so you do not have to log in again on the other
                    apps. The cookie holds a signed token containing your account id, role, and a session
                    id - it does not carry your password or provider tokens. Signing out of any app clears
                    the cookie everywhere, and the session id is revoked server-side so a copy of an old
                    cookie cannot be reused. If you revoke access from the OAuth provider's own settings,
                    that provider notifies EggIdentity's Authentik instance directly and your session on
                    every app ends the same way.
                </p>
            </section>

            <section>
                <h2>Supporter sync (GitHub Sponsors)</h2>
                <p>
                    If you link a GitHub account and sponsor the operator through GitHub Sponsors,
                    EggIdentity checks your sponsorship status against the GitHub API, or receives it
                    instantly through a signed GitHub webhook, and stores whether you are currently a
                    sponsor along with when that was last checked. This is used to grant a Discord role and
                    related supporter perks in the apps that use EggIdentity. No payment details are ever
                    seen by EggIdentity; GitHub Sponsors handles all billing.
                </p>
            </section>

            <section>
                <h2>Cookies and local storage</h2>
                <p>
                    EggIdentity uses only strictly-necessary storage. There are no advertising or tracking
                    cookies, so there is no consent banner to click through.
                </p>
                <ul>
                    <li><strong>eggidentity_session.</strong> The shared sign-in cookie described above. HttpOnly, scoped to davidarthurcole.me, removed on logout or expiry.</li>
                    <li><strong>eggidentity_idhint.</strong> A short-lived, HttpOnly cookie used only to support clean logout and account re-linking with the OAuth provider. Not used to identify you anywhere else.</li>
                    <li><strong>Antiforgery token cookie.</strong> Present on the admin panel only, protects form submissions against cross-site request forgery. Does not identify you.</li>
                </ul>
            </section>

            <section>
                <h2>Data retention</h2>
                <p>
                    Account data is kept while your account is active. Expired or revoked sessions are
                    cleared automatically. Deleting your account, as described below, removes your linked
                    identities and any stored avatar along with it.
                </p>
            </section>

            <section>
                <h2>Removing your data</h2>
                <p>To remove the data stored about you:</p>
                <ul>
                    <li>Revoke access from your Discord, Google, Microsoft, or GitHub account's authorized-apps settings to cut the connection at the provider.</li>
                    <li>Clear cookies for davidarthurcole.me sites to remove the local session.</li>
                    <li>
                        Contact the operator through the
                        <a href="https://github.com/DavidArthurCole/eggidentity" target="_blank" rel="noopener">GitHub repository</a>
                        or the project
                        <a href="https://discord.davidarthurcole.me" target="_blank" rel="noopener">Discord server</a>
                        to request deletion of your stored account record.
                    </li>
                </ul>
            </section>

            <section>
                <h2>Changes</h2>
                <p>
                    This policy may change as the project evolves. The date at the top reflects the latest
                    version. For the terms that govern use of the service, see the
                    <a href="/terms">Terms of Service</a>.
                </p>
            </section>
        </main>
        </body>
        </html>
        """;

    public static readonly string Terms = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>EggIdentity - Terms of Service</title>
        {Style}
        </head>
        <body>
        <main>
            <h1>Terms of Service</h1>
            <p class="updated">Last updated 25 August 2026</p>

            <div class="disclaimer">
                EggIdentity, like the apps it serves, is an independent, fan-made project and is not
                affiliated with, endorsed by, or sponsored by Auxbrain Inc. Egg, Inc. and related marks
                are the property of their respective owners.
            </div>

            <section>
                <h2>1. What this is</h2>
                <p>
                    EggIdentity is shared login and session infrastructure built for the operator's own
                    suite of Egg, Inc. fan tools, currently
                    <a href="https://eggledger.davidarthurcole.me" target="_blank" rel="noopener">EggLedger</a>
                    and <a href="https://eggincognito.davidarthurcole.me" target="_blank" rel="noopener">EggIncognito</a>,
                    plus the small deploy/ops agent used to run them. It is not a general-purpose identity
                    provider and is not offered as a standalone product; you encounter it only because it
                    is what one of those apps uses to sign you in. It is not a game client, a cheat, or a
                    service operated by Egg, Inc.'s developer. The hosted instance at
                    eggidentity.davidarthurcole.me is run by an individual, David Cole, not a company.
                </p>
            </section>

            <section>
                <h2>2. Provided as-is, no warranty</h2>
                <p>
                    Source for EggIdentity is available at
                    <a href="https://github.com/DavidArthurCole/eggidentity" target="_blank" rel="noopener">github.com/DavidArthurCole/eggidentity</a>.
                    It, and the hosted instance, are offered "as is", without warranty of any kind, express
                    or implied. There is no guarantee it will be available, uninterrupted, or accurate. You,
                    and any app that relies on it, use it at your own risk.
                </p>
            </section>

            <section>
                <h2>3. Acceptable use</h2>
                <p>When you use this service, directly or through an app that relies on it, you agree not to:</p>
                <ul>
                    <li>Attempt to bypass its authentication, session, or access controls.</li>
                    <li>Impersonate another user or forge session credentials.</li>
                    <li>Send excessive or abusive load to its login, session, or sponsor-sync endpoints.</li>
                    <li>Use it to violate Egg, Inc.'s own terms of service or the rules of the game.</li>
                    <li>Use it for any unlawful purpose or to distribute malware.</li>
                </ul>
            </section>

            <section>
                <h2>4. Role and perk accuracy</h2>
                <p>
                    Your role and supporter status are derived from what Discord, GitHub, and the
                    operator's own records report at the time they are checked. EggIdentity makes a
                    reasonable effort to keep them in sync but does not guarantee real-time accuracy; a
                    role or perk shown by an app that relies on EggIdentity can lag behind a change made at
                    the provider.
                </p>
            </section>

            <section>
                <h2>5. Supporter program</h2>
                <p>
                    Supporting the project through GitHub Sponsors is entirely voluntary. Billing, refunds,
                    and cancellation are governed by GitHub Sponsors' own terms, not by EggIdentity. Perks
                    granted through this sync are offered in good faith and may change, be added, or be
                    withdrawn at any time.
                </p>
            </section>

            <section>
                <h2>6. Changes to the service</h2>
                <p>
                    This is a personal project. The operator may change, suspend, or discontinue any part
                    of it, including for the apps that depend on it, at any time and without notice. These
                    terms may also be updated. Continued use after a change means you accept the updated
                    terms.
                </p>
            </section>

            <section>
                <h2>7. Limitation of liability</h2>
                <p>
                    To the fullest extent permitted by law, the operator is not liable for any damages
                    arising from your use of, or inability to use, this service or any app that relies on
                    it. This is a free tool maintained in spare time. You accept all risk of using it.
                </p>
            </section>

            <section>
                <h2>8. Contact</h2>
                <p>
                    Questions about these terms can go to the operator through the
                    <a href="https://github.com/DavidArthurCole/eggidentity" target="_blank" rel="noopener">GitHub repository</a>
                    or the project
                    <a href="https://discord.davidarthurcole.me" target="_blank" rel="noopener">Discord server</a>.
                </p>
            </section>
        </main>
        </body>
        </html>
        """;
}
