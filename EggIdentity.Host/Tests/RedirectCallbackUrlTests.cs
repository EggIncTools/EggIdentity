namespace EggIdentity.Host.Tests;

public class RedirectCallbackUrlTests {
    [Fact]
    public void BuildRedirectCallbackUrl_EscapesCodeValue() {
        var url = Program.BuildRedirectCallbackUrl("https://app.example.com", code: "a b&c", error: null);

        Assert.Equal("https://app.example.com?code=a%20b%26c", url);
    }
}
