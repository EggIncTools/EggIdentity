using Microsoft.Extensions.Time.Testing;

namespace EggIdentity.Formatting.Tests;

public class NumberFormatTests {
    [Theory]
    [InlineData(999, "999")]
    [InlineData(1000, "1k")]
    [InlineData(60907, "60.9k")]
    [InlineData(999_500, "1M")]
    [InlineData(3_800_000, "3.8M")]
    [InlineData(1_234_000_000, "1.23B")]
    [InlineData(4.5e12, "4.5T")]
    [InlineData(1.23e16, "12.3q")]
    [InlineData(1.5e39, "1.5D")]
    [InlineData(-60907, "-60.9k")]
    public void CompactUsesTheSuffixLadder(double value, string expected) => Assert.Equal(expected, NumberFormat.Compact(value));

    [Fact]
    public void CompactPastTheLadderFallsBackToExponent() => Assert.Equal("1.00e+45", NumberFormat.Compact(1e45));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonFiniteRendersADash(double value) {
        Assert.Equal("-", NumberFormat.Compact(value));
        Assert.Equal("-", NumberFormat.Full(value));
    }

    [Fact]
    public void LongOverloadMatchesDouble() => Assert.Equal(NumberFormat.Compact(60907.0), NumberFormat.Compact(60907L));

    [Fact]
    public void FullUsesGrouping() => Assert.Equal("60,907", NumberFormat.Full(60907));

    [Theory]
    [InlineData(0.088, "8.8%")]
    [InlineData(0.000_42, "0.042%")]
    [InlineData(0.000_4, "0.04%")]
    [InlineData(0.5, "50%")]
    [InlineData(0.123_45, "12%")]
    [InlineData(1, "100%")]
    [InlineData(0, "0%")]
    [InlineData(-0.088, "-8.8%")]
    public void PercentKeepsTwoSignificantDigits(double fraction, string expected) => Assert.Equal(expected, NumberFormat.Percent(fraction));

    [Theory]
    [InlineData(0.123, "12%")]
    [InlineData(0.001, "0.1%")]
    [InlineData(0.000_232, "1 in 4,310")]
    public void OddsSwitchesBelowOneInAThousand(double p, string expected) => Assert.Equal(expected, NumberFormat.Odds(p));

    [Theory]
    [InlineData(7_500, "2h 5m")]
    [InlineData(300, "5m")]
    [InlineData(12, "12s")]
    [InlineData(187_200, "2d 4h")]
    public void BriefKeepsTwoUnits(double seconds, string expected) =>
        Assert.Equal(expected, DurationFormat.Brief(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(5_400, "1.5h")]
    [InlineData(1_800, "30m")]
    [InlineData(45, "45s")]
    public void FractionalKeepsOneUnit(double seconds, string expected) =>
        Assert.Equal(expected, DurationFormat.Fractional(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void CountdownReadsNowOncePast() {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("1h", DurationFormat.Countdown(clock.GetUtcNow().AddHours(1), clock));
        Assert.Equal("now", DurationFormat.Countdown(clock.GetUtcNow().AddMinutes(-1), clock));
    }

    [Theory]
    [InlineData(512, "512 B", "512B")]
    [InlineData(1_433, "1.4 KB", "1.4KB")]
    [InlineData(3_355_443, "3.2 MB", "3.2MB")]
    [InlineData(1_181_116_006, "1.1 GB", "1.1GB")]
    public void BytesUseBase1024(long bytes, string human, string compact) {
        Assert.Equal(human, ByteFormat.Humanize(bytes));
        Assert.Equal(compact, ByteFormat.Compact(bytes));
    }

    [Theory]
    [InlineData("A1B2C3", "color", "color:#a1b2c3")]
    [InlineData("a1b2c3", "background-color", "background-color:#a1b2c3")]
    [InlineData("a1b2c", "color", null)]
    [InlineData("zzzzzz", "color", null)]
    [InlineData(null, "color", null)]
    [InlineData("a1b2c3", "color;x", null)]
    public void HexStyleGuardsBothParts(string? hex, string property, string? expected) =>
        Assert.Equal(expected, CssColor.HexStyle(hex, property));
}
