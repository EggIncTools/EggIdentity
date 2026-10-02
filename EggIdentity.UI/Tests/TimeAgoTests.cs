namespace EggIdentity.UI.Tests;

public class TimeAgoTests {
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-30, "just now")]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1m ago")]
    [InlineData(3599, "59m ago")]
    [InlineData(3600, "1h ago")]
    [InlineData(86399, "23h ago")]
    [InlineData(86400, "1d ago")]
    [InlineData(29 * 86400, "29d ago")]
    [InlineData(30 * 86400, "1mo ago")]
    [InlineData(364 * 86400, "12mo ago")]
    [InlineData(365 * 86400, "1y ago")]
    [InlineData(800 * 86400, "2y ago")]
    public void FormatsAgeBuckets(int secondsAgo, string expected) {
        Assert.Equal(expected, TimeAgo.Format(Now.AddSeconds(-secondsAgo), Now));
    }
}
