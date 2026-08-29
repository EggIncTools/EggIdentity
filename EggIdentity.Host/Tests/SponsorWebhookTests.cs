using System.Security.Cryptography;
using System.Text;
using EggIdentity.Host;
using Xunit;

namespace EggIdentity.Host.Tests;

public class SponsorWebhookTests {
    private static string Sign(string secret, byte[] body) {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    [Fact]
    public void VerifySignature_ValidSignature_ReturnsTrue() {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"created\"}");
        var signature = Sign("secret1", body);

        Assert.True(SponsorWebhook.VerifySignature("secret1", body, signature));
    }

    [Fact]
    public void VerifySignature_WrongSecret_ReturnsFalse() {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"created\"}");
        var signature = Sign("secret1", body);

        Assert.False(SponsorWebhook.VerifySignature("secret2", body, signature));
    }

    [Fact]
    public void VerifySignature_MissingPrefix_ReturnsFalse() {
        var body = Encoding.UTF8.GetBytes("{}");

        Assert.False(SponsorWebhook.VerifySignature("secret1", body, "deadbeef"));
    }

    [Fact]
    public void VerifySignature_NullHeader_ReturnsFalse() {
        var body = Encoding.UTF8.GetBytes("{}");

        Assert.False(SponsorWebhook.VerifySignature("secret1", body, null));
    }

    [Fact]
    public void ParsePayload_ValidEvent_ReturnsActionAndSubject() {
        var json = """
            {"action":"created","sponsorship":{"sponsor":{"id":12345,"login":"octocat"},"sponsorable":{"login":"DavidArthurCole"}}}
            """;

        var result = SponsorWebhook.ParsePayload(json);

        Assert.NotNull(result);
        Assert.Equal("created", result!.Action);
        Assert.Equal("12345", result.SponsorSubject);
    }

    [Fact]
    public void ParsePayload_MissingSponsorship_ReturnsNull() {
        var result = SponsorWebhook.ParsePayload("{\"action\":\"created\"}");

        Assert.Null(result);
    }

    [Fact]
    public void ParsePayload_MissingAction_ReturnsNull() {
        var json = """
            {"sponsorship":{"sponsor":{"id":1,"login":"x"}}}
            """;

        Assert.Null(SponsorWebhook.ParsePayload(json));
    }

    [Fact]
    public void ResolveIsSponsor_Created_ReturnsTrue() {
        Assert.True(SponsorWebhook.ResolveIsSponsor("created"));
    }

    [Fact]
    public void ResolveIsSponsor_Cancelled_ReturnsFalse() {
        Assert.False(SponsorWebhook.ResolveIsSponsor("cancelled"));
    }

    [Fact]
    public void ResolveIsSponsor_TierChanged_ReturnsNull() {
        Assert.Null(SponsorWebhook.ResolveIsSponsor("tier_changed"));
    }
}
