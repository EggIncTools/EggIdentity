namespace EggIdentity.Consent.Tests;

public class ConsentReaderTests {
    [Fact]
    public void Allows_FollowsCurrentAndRaisesChanged() {
        var reader = new ConsentReader();
        var raised = 0;
        reader.Changed += (_, _) => raised++;

        Assert.False(reader.Allows(ConsentCategory.Functional));

        reader.Set(new ConsentState(true, false, 1, DateTimeOffset.UnixEpoch));

        Assert.True(reader.Allows(ConsentCategory.Functional));
        Assert.False(reader.Allows(ConsentCategory.Analytics));
        Assert.Equal(1, raised);
    }
}
