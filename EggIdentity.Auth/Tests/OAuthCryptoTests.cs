using EggIdentity.Auth;
using Xunit;

namespace EggIdentity.Auth.Tests;

public class OAuthCryptoTests {
    [Fact]
    public void GenerateEncryptionKey_Is64HexChars() {
        var key = OAuthCrypto.GenerateEncryptionKey();
        Assert.Equal(64, key.Length);
        Assert.Matches("^[0-9a-f]{64}$", key);
    }

    [Fact]
    public void GenerateEncryptionKey_IsRandom() {
        Assert.NotEqual(OAuthCrypto.GenerateEncryptionKey(), OAuthCrypto.GenerateEncryptionKey());
    }

    [Fact]
    public void RandomHex_ProducesRequestedLength() {
        var hex = OAuthCrypto.RandomHex(16);
        Assert.Equal(32, hex.Length);
        Assert.Matches("^[0-9a-f]{32}$", hex);
    }
}
