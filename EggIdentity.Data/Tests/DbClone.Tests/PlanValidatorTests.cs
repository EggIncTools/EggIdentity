namespace EggIdentity.DbClone.Tests;

public class PlanValidatorTests {
    private static ClonePlan Plan(params TablePolicy[] tables) =>
        new("app", "app_subprod", tables) { IgnoredTables = ["app_migrations"] };

    [Fact]
    public void Coverage_PassesWhenEveryTargetTableIsGovernedOrIgnored() {
        var plan = Plan(new TablePolicy("users", ClonePolicy.Full), new TablePolicy("sessions", ClonePolicy.SchemaOnly));

        var errors = PlanValidator.Coverage(plan, ["users", "sessions"], ["users", "sessions", "app_migrations", PlanValidator.StampTable]);

        Assert.Empty(errors);
    }

    [Fact]
    public void Coverage_RejectsUngovernedTargetTable() {
        var plan = Plan(new TablePolicy("users", ClonePolicy.Full));

        var errors = PlanValidator.Coverage(plan, ["users", "secrets"], ["users", "secrets"]);

        Assert.Contains(errors, e => e.StartsWith("secrets", StringComparison.Ordinal));
    }

    [Fact]
    public void Coverage_RejectsTableAbsentFromBothDatabases() {
        var plan = Plan(new TablePolicy("ghost", ClonePolicy.Skip));

        var errors = PlanValidator.Coverage(plan, [], []);

        Assert.Contains("ghost exists in neither database", errors);
    }

    [Fact]
    public void Coverage_RejectsCopyWhoseSourceIsMissing() {
        var plan = Plan(new TablePolicy("users", ClonePolicy.Full));

        var errors = PlanValidator.Coverage(plan, [], ["users"]);

        Assert.Single(errors);
        Assert.Contains("missing from the source", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Coverage_RejectsScrubWithoutScrubSql() {
        var plan = Plan(new TablePolicy("users", ClonePolicy.Scrub));

        var errors = PlanValidator.Coverage(plan, ["users"], ["users"]);

        Assert.Contains(errors, e => e.Contains("no ScrubSql", StringComparison.Ordinal));
    }

    [Fact]
    public void Intersect_KeepsSharedColumnsInTargetOrderAndReportsBothSides() {
        var report = PlanValidator.Intersect("users", ["id", "name", "legacy"], ["name", "id", "added"]);

        Assert.Equal(["name", "id"], report.Columns);
        Assert.Equal(["legacy"], report.SourceOnly);
        Assert.Equal(["added"], report.TargetOnly);
    }
}
