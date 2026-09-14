using EggIdentity.Auth;
using Xunit;

namespace EggIdentity.Auth.Tests;

public class TokenHashTests {
    [Fact]
    public void Of_MatchesTheKnownSha256HexVector() {
        Assert.Equal(
            "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
            TokenHash.Of("hello"));
    }

    [Fact]
    public void Of_IsStable() {
        Assert.Equal(TokenHash.Of("abc"), TokenHash.Of("abc"));
        Assert.NotEqual(TokenHash.Of("abc"), TokenHash.Of("abd"));
    }

    [Fact]
    public void AMintedToken_IsNotItsOwnHash() {
        var token = TokenHash.Mint();

        Assert.NotEqual(token, TokenHash.Of(token));
        Assert.Equal(TokenHash.TokenBytes * 2, token.Length);
    }

    [Fact]
    public void MintedTokens_DoNotRepeat() {
        var tokens = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 256; i++) Assert.True(tokens.Add(TokenHash.Mint()));
    }

    [Fact]
    public void Matches_AcceptsTheTokenAndRejectsEverythingElse() {
        var token = TokenHash.Mint();
        var stored = TokenHash.Of(token);

        Assert.True(TokenHash.Matches(token, stored));
        Assert.False(TokenHash.Matches(token + "x", stored));
        Assert.False(TokenHash.Matches(token, ""));
        Assert.False(TokenHash.Matches("", stored));
    }

    [Fact]
    public void Hex_IsLowercase() {
        var hash = TokenHash.Of("Hello");
        var token = TokenHash.Mint();

        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Equal(token.ToLowerInvariant(), token);
    }
}
