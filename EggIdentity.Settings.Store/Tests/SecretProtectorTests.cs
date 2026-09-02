using System.Security.Cryptography;

namespace EggIdentity.Settings.Store.Tests;

public class SecretProtectorTests {
    private static SecretProtector NewProtector() =>
        SecretProtector.FromKey(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)))!;

    [Fact]
    public void RoundTrip_RecoversPlaintext() {
        var protector = NewProtector();

        var sealedValue = protector.Protect("hunter2");

        Assert.NotEqual("hunter2", sealedValue);
        Assert.True(SecretProtector.IsProtected(sealedValue));
        Assert.Equal("hunter2", protector.Unprotect(sealedValue));
    }

    [Fact]
    public void SameInput_ProducesDifferentCiphertext() {
        var protector = NewProtector();

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void WrongKey_FailsClosed() {
        var sealedValue = NewProtector().Protect("hunter2");

        Assert.Null(NewProtector().Unprotect(sealedValue));
    }

    [Fact]
    public void TamperedPayload_FailsClosed() {
        var protector = NewProtector();
        var sealedValue = protector.Protect("hunter2");
        var tampered = sealedValue[..^4] + (sealedValue.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        Assert.Null(protector.Unprotect(tampered));
    }

    [Fact]
    public void UnprotectedValue_PassesThrough() {
        Assert.Equal("plain", NewProtector().Unprotect("plain"));
        Assert.False(SecretProtector.IsProtected("plain"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!")]
    public void BadKey_YieldsNoProtector(string? key) {
        Assert.Null(SecretProtector.FromKey(key));
    }

    [Fact]
    public void ShortKey_IsRejected() {
        Assert.Null(SecretProtector.FromKey(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))));
    }
}
