namespace EggIdentity.Db.Tests;

public class DatabaseTests {
    [Theory]
    [InlineData("postgres://app:pw@frame:5432/app")]
    [InlineData("postgresql://app:pw@frame:5432/app")]
    [InlineData("  POSTGRES://app@frame/app")]
    public void UriForm_IsRejectedWithAKeywordExample(string connStr) {
        var message = Database.DescribeUriForm(connStr);

        Assert.NotNull(message);
        Assert.Contains("keyword form", message, StringComparison.Ordinal);
        Assert.Contains("Host=", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Host=frame;Username=app;Password=pw;Database=app")]
    [InlineData("host=frame;database=app")]
    [InlineData("")]
    public void KeywordForm_IsAccepted(string connStr) =>
        Assert.Null(Database.DescribeUriForm(connStr));

    [Fact]
    public async Task InitAsync_FailsFastOnAUri_WithoutOpeningAConnection() {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Database.InitAsync("postgres://app:pw@frame:5432/app"));

        Assert.Contains("keyword form", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_StillRejectsEmpty() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Database.InitAsync(""));
}
