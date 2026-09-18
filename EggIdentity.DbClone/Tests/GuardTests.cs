using Npgsql;

namespace EggIdentity.DbClone.Tests;

public class GuardTests {
    private static readonly ClonePlan Plan = new("app", "app_subprod", []);

    [Fact]
    public void EnsureDatabase_AcceptsMatchingName() {
        Assert.Equal("app_subprod", SubProdGuard.EnsureDatabase("Host=h;Database=app_subprod", "app_subprod"));
    }

    [Fact]
    public void EnsureDatabase_RejectsOtherName() {
        var e = Assert.Throws<InvalidOperationException>(() => SubProdGuard.EnsureDatabase("Host=h;Database=app", "app_subprod"));
        Assert.Contains("app_subprod", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureDatabase_RejectsUriForm() {
        Assert.Throws<InvalidOperationException>(() => SubProdGuard.EnsureDatabase("postgres://u:p@h/app_subprod", "app_subprod"));
    }

    [Fact]
    public void Check_RejectsSameDatabaseOnBothSides() {
        Assert.Throws<InvalidOperationException>(() => CloneGuards.Check(Plan, "Host=h;Database=app_subprod", "Host=h;Database=app_subprod"));
    }

    [Fact]
    public void Check_AppendsReadOnlyOptionToSource() {
        var endpoints = CloneGuards.Check(Plan, "Host=h;Database=app;Options=-c statement_timeout=5s", "Host=h;Database=app_subprod");

        var options = new NpgsqlConnectionStringBuilder(endpoints.SourceConnection).Options;
        Assert.Equal("-c statement_timeout=5s " + CloneGuards.ReadOnlyOption, options);
        Assert.Equal("app", endpoints.SourceDatabase);
    }

    [Fact]
    public void EnsureSubProd_ThrowsUnlessSubProd() {
        Assert.Throws<InvalidOperationException>(() => SubProdGuard.EnsureSubProd(_ => null));
        SubProdGuard.EnsureSubProd(_ => "subprod");
    }
}
