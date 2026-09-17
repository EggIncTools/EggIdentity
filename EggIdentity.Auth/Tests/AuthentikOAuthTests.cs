namespace EggIdentity.Auth.Tests;

public class AuthentikOAuthTests {
    [Fact]
    public void BuildAuthParams_ContainsPkceAndClientId() {
        var oauth = new AuthentikOAuth("https://auth.example.com/application/o/eggidentity-login/", "client123", "secret", "https://identity.example.com/auth/callback");
        var (query, state, verifier) = oauth.BuildAuthParams();

        Assert.Contains("client_id=client123", query);
        Assert.Contains("state=" + state, query);
        Assert.Contains("code_challenge=", query);
        Assert.Contains("code_challenge_method=S256", query);
        Assert.Contains("response_type=code", query);
        Assert.Contains("scope=openid+profile+email+discord_id", query);
        Assert.True(verifier.Length >= 43); // RFC 7636 minimum verifier length
    }

    [Fact]
    public void BuildAuthParams_StateAndVerifierAreRandomPerCall() {
        var oauth = new AuthentikOAuth("https://auth.example.com/application/o/eggidentity-login/", "client123", "secret", "https://identity.example.com/auth/callback");
        var (_, state1, verifier1) = oauth.BuildAuthParams();
        var (_, state2, verifier2) = oauth.BuildAuthParams();

        Assert.NotEqual(state1, state2);
        Assert.NotEqual(verifier1, verifier2);
    }

    [Fact]
    public void BuildAuthParams_ContainsSameParamsAsAuthUrl_WithoutAuthorizeEndpointPrefix() {
        var oauth = new AuthentikOAuth("https://auth.example.com", "client123", "secret", "https://identity.example.com/auth/callback");
        var (query, state, verifier) = oauth.BuildAuthParams();

        Assert.DoesNotContain("/application/o/authorize/", query);
        Assert.DoesNotContain("?", query); // raw query params only, no leading '?'
        Assert.Contains("client_id=client123", query);
        Assert.Contains("state=" + state, query);
        Assert.Contains("code_challenge=", query);
        Assert.Contains("code_challenge_method=S256", query);
        Assert.Contains("response_type=code", query);
        Assert.Contains("scope=openid+profile+email+discord_id", query);
        Assert.True(verifier.Length >= 43);
    }

    [Fact]
    public void Authority_AndBuildAuthParams_ComposeIntoAuthorizeUrl() {
        var oauth = new AuthentikOAuth("https://auth.example.com", "client123", "secret", "https://identity.example.com/auth/callback");
        var (query, state, verifier) = oauth.BuildAuthParams();
        var url = $"{oauth.Authority}/application/o/authorize/?{query}";

        Assert.StartsWith("https://auth.example.com/application/o/authorize/?", url);
        Assert.Contains("client_id=client123", url);
        Assert.Contains("state=" + state, url);
        Assert.True(verifier.Length >= 43);
    }

    [Fact]
    public void BuildAuthParams_ScopeIncludesAllFourSourceClaims() {
        var oauth = new AuthentikOAuth("https://auth.example.com", "client123", "secret", "https://identity.example.com/auth/callback");
        var (query, _, _) = oauth.BuildAuthParams();

        Assert.Contains("scope=openid+profile+email+discord_id+google_id+microsoft_id+github_id", query);
    }

    [Fact]
    public void AuthentikTokenResult_PerSourceIds_MapsAllFourProviders() {
        var token = new AuthentikTokenResult(
            Sub: "sub123", DiscordId: "d1", GoogleId: "g1", MicrosoftId: "m1", GitHubId: "h1",
            Username: "user", Avatar: null, Sid: null, IdToken: null);

        Assert.Equal("d1", token.PerSourceIds["discord"]);
        Assert.Equal("g1", token.PerSourceIds["google"]);
        Assert.Equal("m1", token.PerSourceIds["microsoft"]);
        Assert.Equal("h1", token.PerSourceIds["github"]);
    }

    [Fact]
    public void AuthentikTokenResult_PerSourceIds_NullClaimsStayNull() {
        var token = new AuthentikTokenResult(
            Sub: "sub123", DiscordId: "d1", GoogleId: null, MicrosoftId: null, GitHubId: null,
            Username: "user", Avatar: null, Sid: null, IdToken: null);

        Assert.Equal("d1", token.PerSourceIds["discord"]);
        Assert.Null(token.PerSourceIds["google"]);
        Assert.Null(token.PerSourceIds["microsoft"]);
        Assert.Null(token.PerSourceIds["github"]);
    }

    [Fact]
    public void CodeChallenge_IsS256OfVerifier() {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        var challenge = AuthentikOAuth.ComputeCodeChallenge(verifier);

        Assert.Equal(expectedChallenge, challenge);
    }

    private static string JwsWith(string payloadJson) {
        static string Enc(string raw) {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        return $"{Enc("{\"alg\":\"RS256\"}")}.{Enc(payloadJson)}.sig";
    }

    [Fact]
    public void DescribeIdTokenProblem_NamesJweWhenNoKeyIsConfigured() {
        var jwe = "header.encryptedkey.iv.ciphertext.tag";

        var problem = AuthentikOAuth.DescribeIdTokenProblem(jwe);

        Assert.Contains("encrypted (JWE)", problem);
        Assert.Null(AuthentikOAuth.ReadSessionIdFromIdToken(jwe));
    }

    [Fact]
    public void DescribeIdTokenProblem_DistinguishesAbsentFromUnparseableFromMissingSid() {
        Assert.Contains("no id_token", AuthentikOAuth.DescribeIdTokenProblem(null));
        Assert.Contains("no id_token", AuthentikOAuth.DescribeIdTokenProblem(""));
        Assert.Contains("not a JWS", AuthentikOAuth.DescribeIdTokenProblem("nodots"));
        Assert.Contains("did not decode as JSON", AuthentikOAuth.DescribeIdTokenProblem("aaa.!!!not-base64!!!.ccc"));
        Assert.Contains("no sid claim", AuthentikOAuth.DescribeIdTokenProblem(JwsWith("""{"aud":"client123"}""")));
    }

    [Fact]
    public void DescribeIdTokenProblem_IsNullWhenTheTokenCarriesASid() {
        var token = JwsWith("""{"sid":"session-1"}""");

        Assert.Null(AuthentikOAuth.DescribeIdTokenProblem(token));
        Assert.Equal("session-1", AuthentikOAuth.ReadSessionIdFromIdToken(token));
    }

    [Fact]
    public void ReadAudienceFromIdToken_StillReturnsNullForAnEncryptedTokenRatherThanThrowing() {
        Assert.Null(AuthentikOAuth.ReadAudienceFromIdToken("header.encryptedkey.iv.ciphertext.tag"));
        Assert.Null(AuthentikOAuth.ReadAudienceFromIdToken(null));
    }

    [Fact]
    public void ReadRsaPrivateKey_ReturnsNullForAbsentPemAndAKeyForAValidOne() {
        Assert.Null(AuthentikOAuth.ReadRsaPrivateKey(null));
        Assert.Null(AuthentikOAuth.ReadRsaPrivateKey("   "));

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        Assert.NotNull(AuthentikOAuth.ReadRsaPrivateKey(rsa.ExportPkcs8PrivateKeyPem()));
    }
}
