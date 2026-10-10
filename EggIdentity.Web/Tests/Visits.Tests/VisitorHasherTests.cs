using System.Reflection;

namespace EggIdentity.Visits.Tests;

public class VisitorHasherTests {
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SameInputsSameHashWithinDay() {
        var hasher = new VisitorHasher(new FixedClock(Start));
        var a = hasher.Hash("site", "1.2.3.4", "ua");
        var b = hasher.Hash("site", "1.2.3.4", "ua");
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void DifferentInputsDifferentHash() {
        var hasher = new VisitorHasher(new FixedClock(Start));
        var a = hasher.Hash("site", "1.2.3.4", "ua");
        Assert.NotEqual(a, hasher.Hash("site", "1.2.3.5", "ua"));
        Assert.NotEqual(a, hasher.Hash("other", "1.2.3.4", "ua"));
        Assert.NotEqual(a, hasher.Hash("site", "1.2.3.4", "ub"));
    }

    [Fact]
    public void HashChangesAfterUtcMidnightRotation() {
        var clock = new FixedClock(Start);
        var hasher = new VisitorHasher(clock);
        var before = hasher.Hash("site", "1.2.3.4", "ua");
        clock.Advance(TimeSpan.FromHours(11));
        Assert.Equal(before, hasher.Hash("site", "1.2.3.4", "ua"));
        clock.Advance(TimeSpan.FromHours(2));
        Assert.NotEqual(before, hasher.Hash("site", "1.2.3.4", "ua"));
    }

    [Fact]
    public void TwoProcessesNeverAgree() {
        var clock = new FixedClock(Start);
        Assert.NotEqual(new VisitorHasher(clock).Hash("site", "1.2.3.4", "ua"), new VisitorHasher(clock).Hash("site", "1.2.3.4", "ua"));
    }

    [Fact]
    public void SaltIsNotExposed() {
        var members = typeof(VisitorHasher).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(members, m => m is FieldInfo or PropertyInfo);
        string[] names = [.. members.OfType<MethodInfo>().Where(m => !m.IsSpecialName).Select(m => m.Name).Distinct()];
        Assert.Equal(["Hash"], names);
    }
}
