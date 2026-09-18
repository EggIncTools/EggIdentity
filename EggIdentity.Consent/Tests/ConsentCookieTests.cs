namespace EggIdentity.Consent.Tests;

public class ConsentCookieTests {
    [Fact]
    public void Format_ProducesSpecShape() {
        var state = new ConsentState(true, false, 1, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        Assert.Equal("v=1&f=1&a=0&t=1700000000", ConsentCookie.Format(state));
    }

    [Fact]
    public void Parse_RoundTripsFormat() {
        var state = new ConsentState(false, true, 2, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        var parsed = ConsentCookie.Parse(ConsentCookie.Format(state), 1);

        Assert.Equal(state, parsed);
    }

    [Fact]
    public void Parse_OlderPolicyVersion_CountsAsAbsent() {
        Assert.Null(ConsentCookie.Parse("v=1&f=1&a=1&t=1700000000", 2));
        Assert.NotNull(ConsentCookie.Parse("v=2&f=1&a=1&t=1700000000", 2));
        Assert.NotNull(ConsentCookie.Parse("v=3&f=1&a=1&t=1700000000", 2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v=1&f=1&a=0")]
    [InlineData("v=x&f=1&a=0&t=1700000000")]
    [InlineData("v=1&f=1&a=0&t=1700000000&z=1")]
    public void Parse_Malformed_ReturnsNull(string? value) {
        Assert.Null(ConsentCookie.Parse(value, 1));
    }
}
