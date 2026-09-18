namespace EggIdentity.DbClone.Tests;

public class SqlTextTests {
    [Fact]
    public void Quote_WrapsAndDoublesEmbeddedQuotes() {
        Assert.Equal("\"users\"", SqlIdentifier.Quote("users"));
        Assert.Equal("\"we\"\"ird\"", SqlIdentifier.Quote("we\"ird"));
        Assert.Equal("\"a; DROP TABLE b\"", SqlIdentifier.Quote("a; DROP TABLE b"));
    }

    [Fact]
    public void Quote_RejectsBlankAndNul() {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.Quote(" "));
        Assert.Throws<ArgumentException>(() => SqlIdentifier.Quote("a\0b"));
    }

    [Fact]
    public void GeneratedSql_QuotesEveryIdentifier() {
        var policy = new TablePolicy("users", ClonePolicy.Full) { Where = "active" };

        Assert.Equal("SELECT count(*) FROM \"users\" WHERE active", CloneRunner.CountSql(policy));
        Assert.Equal("COPY (SELECT \"id\", \"name\" FROM \"users\" WHERE active) TO STDOUT", CloneRunner.ExportSql(policy, ["id", "name"]));
        Assert.Equal("COPY \"users\" (\"id\", \"name\") FROM STDIN", CloneRunner.ImportSql("users", ["id", "name"]));
        Assert.Equal("TRUNCATE TABLE \"a\", \"b\"", CloneRunner.TruncateSql(["a", "b"]));
        Assert.Equal(
            "SELECT count(*) FROM \"t\" WHERE \"user_id\" IS NOT NULL AND NOT (\"user_id\"::text = ANY($1))",
            CloneVerifier.OrphanSql("t", "user_id"));
    }
}
