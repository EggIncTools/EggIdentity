using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EggIdentity.Contract;
using Microsoft.IdentityModel.Tokens;

namespace EggIdentity.Auth;

public sealed record AuthentikTokenResult(
    string Sub, string? DiscordId, string? GoogleId, string? MicrosoftId, string? GitHubId,
    string? Username, string? Avatar, string? Sid, string? IdToken) {
    public IReadOnlyDictionary<string, string?> PerSourceIds => new Dictionary<string, string?>(StringComparer.Ordinal) {
        [IdentityWire.Discord] = DiscordId,
        [IdentityWire.Google] = GoogleId,
        [IdentityWire.Microsoft] = MicrosoftId,
        [IdentityWire.GitHub] = GitHubId,
    };
}

public sealed class AuthentikOAuth(string authority, string clientId, string clientSecret, string callbackUrl, string? tokenDecryptionKeyPem = null) {
    private static readonly HttpClient Http = new();

    public SecurityKey? TokenDecryptionKey { get; } = ReadRsaPrivateKey(tokenDecryptionKeyPem);

    public static SecurityKey? ReadRsaPrivateKey(string? pem) {
        if (string.IsNullOrWhiteSpace(pem)) return null;
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return new RsaSecurityKey(rsa);
    }

    public string Authority { get; } = authority.TrimEnd('/');

    public string ClientId { get; } = clientId;

    public string CallbackUrl { get; } = callbackUrl;

    public (string Query, string State, string CodeVerifier) BuildAuthParams() {
        var state = OAuthCrypto.RandomHex(16);
        var verifier = GenerateCodeVerifier();
        var challenge = ComputeCodeChallenge(verifier);

        var query = new Dictionary<string, string> {
            ["client_id"] = ClientId,
            ["redirect_uri"] = CallbackUrl,
            ["response_type"] = "code",
            ["scope"] = "openid+profile+email+discord_id+google_id+microsoft_id+github_id",
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };
        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value).Replace("%2B", "+")}"));
        return (qs, state, verifier);
    }

    public async Task<AuthentikTokenResult> HandleCallbackAsync(string code, string codeVerifier, CancellationToken ct = default) {
        var tokenResp = await Http.PostAsync($"{Authority}/application/o/token/", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["client_id"] = ClientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = CallbackUrl,
            ["code_verifier"] = codeVerifier,
        }), ct);
        tokenResp.EnsureSuccessStatusCode();
        using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(ct));
        var accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
        if (string.IsNullOrEmpty(accessToken))
            throw new InvalidOperationException("Authentik token response missing access_token");

        var idToken = tokenDoc.RootElement.TryGetProperty("id_token", out var itEl) ? itEl.GetString() : null;
        var sid = ReadSessionIdFromIdToken(idToken, TokenDecryptionKey);
        if (string.IsNullOrEmpty(sid))
            Console.Error.WriteLine($"authentik callback for client {ClientId}: no session id, revocation is disabled for this session: {DescribeIdTokenProblem(idToken, TokenDecryptionKey)}");

        using var userInfoReq = new HttpRequestMessage(HttpMethod.Get, $"{Authority}/application/o/userinfo/");
        userInfoReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResp = await Http.SendAsync(userInfoReq, ct);
        userInfoResp.EnsureSuccessStatusCode();
        using var userInfoDoc = JsonDocument.Parse(await userInfoResp.Content.ReadAsStringAsync(ct));
        var root = userInfoDoc.RootElement;

        var sub = root.TryGetProperty("sub", out var subEl) ? subEl.GetString() : null;
        if (string.IsNullOrEmpty(sub))
            throw new InvalidOperationException("Authentik userinfo response missing sub");

        return new AuthentikTokenResult(
            Sub: sub,
            DiscordId: ReadClaimAsString(root, "discord_id"),
            GoogleId: ReadClaimAsString(root, "google_id"),
            MicrosoftId: ReadClaimAsString(root, "microsoft_id"),
            GitHubId: ReadClaimAsString(root, "github_id"),
            Username: root.TryGetProperty("preferred_username", out var un) ? un.GetString() : null,
            Avatar: root.TryGetProperty("picture", out var av) ? av.GetString() : null,
            Sid: sid,
            IdToken: idToken);
    }

    private static string? ReadClaimAsString(JsonElement root, string propertyName) {
        if (!root.TryGetProperty(propertyName, out var el)) return null;
        return el.ValueKind switch {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            _ => null,
        };
    }

    public static string? ReadSessionIdFromIdToken(string? idToken, SecurityKey? decryptionKey = null) {
        using var doc = DecodeIdTokenPayload(idToken, decryptionKey);
        if (doc is null) return null;
        return doc.RootElement.TryGetProperty("sid", out var sidEl) ? sidEl.GetString() : null;
    }

    public static string? ReadAudienceFromIdToken(string? idToken, SecurityKey? decryptionKey = null) {
        using var doc = DecodeIdTokenPayload(idToken, decryptionKey);
        if (doc is null) return null;
        if (!doc.RootElement.TryGetProperty("aud", out var audEl)) return null;
        if (audEl.ValueKind == JsonValueKind.String) return audEl.GetString();
        if (audEl.ValueKind == JsonValueKind.Array) {
            foreach (var el in audEl.EnumerateArray())
                if (el.ValueKind == JsonValueKind.String) return el.GetString();
        }
        return null;
    }

    public static string? DescribeIdTokenProblem(string? idToken, SecurityKey? decryptionKey = null) {
        if (string.IsNullOrEmpty(idToken)) return "the token response carried no id_token";
        var parts = idToken.Split('.');
        if (parts.Length == EncryptedTokenParts && decryptionKey is null)
            return "the id_token is encrypted (JWE) and this host holds no decryption key; set authentik.token_decryption_key or unset the encryption key on the Authentik provider";
        if (parts.Length < 2)
            return $"the id_token is not a JWS ({parts.Length} part(s))";
        using var doc = DecodeIdTokenPayload(idToken, decryptionKey);
        if (doc is null) {
            return parts.Length == EncryptedTokenParts
                ? "the id_token did not decrypt with the configured key"
                : "the id_token payload did not decode as JSON";
        }
        return doc.RootElement.TryGetProperty("sid", out _) ? null : "the id_token carried no sid claim";
    }

    private const int EncryptedTokenParts = 5;

    private static JsonDocument? DecodeIdTokenPayload(string? idToken, SecurityKey? decryptionKey) {
        if (string.IsNullOrEmpty(idToken)) return null;
        var parts = idToken.Split('.');
        if (parts.Length == EncryptedTokenParts) {
            var payloadJson = decryptionKey is null ? null : DecryptPayloadJson(idToken, decryptionKey);
            if (payloadJson is null) return null;
            try {
                return JsonDocument.Parse(payloadJson);
            } catch (Exception) {
                return null;
            }
        }
        if (parts.Length < 2) return null;
        try {
            var payload = Convert.FromBase64String(PadBase64Url(parts[1]));
            return JsonDocument.Parse(payload);
        } catch (Exception) {
            return null;
        }
    }

    private static string? DecryptPayloadJson(string jwe, SecurityKey decryptionKey) {
        try {
            new JwtSecurityTokenHandler().ValidateToken(jwe, new TokenValidationParameters {
                TokenDecryptionKey = decryptionKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = false,
                RequireSignedTokens = false,
                SignatureValidator = (token, _) => new JwtSecurityToken(token),
            }, out var validated);
            return (validated as JwtSecurityToken)?.Payload.SerializeToJson();
        } catch (Exception) {
            return null;
        }
    }

    private static string PadBase64Url(string value) {
        var s = value.Replace('-', '+').Replace('_', '/');
        return (s.Length % 4) switch { 2 => s + "==", 3 => s + "=", _ => s };
    }

    private static string GenerateCodeVerifier() {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    public static string ComputeCodeChallenge(string verifier) {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
