namespace EggIdentity.Filters.Tests;

public class FilterValidatorTests {
    private static readonly FilterFieldDef[] Fields = [
        new("ship", "Ship", FilterValueKind.Select, FilterOps.Comparison),
        new("multiplier", "Multiplier", FilterValueKind.Number, FilterOps.Comparison),
        new("ultra", "Ultra", FilterValueKind.Bool, FilterOps.Bool),
    ];

    [Fact]
    public void Validate_ReportsEachProblemWithRowAndMember() {
        var set = new FilterSet(
            [new("ship", "=", "3"), new("gone", "=", "1")],
            [[new("multiplier", ">", "abc")], []]);

        var problems = FilterValidator.Validate(set, Fields);

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p is { Row: 0, Member: 1 } && p.Reason.Contains("does not parse", StringComparison.Ordinal));
        Assert.Contains(problems, p => p is { Row: 1, Member: 0 } && p.Reason.Contains("unknown field", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_BlankMemberIsNotAProblem() => Assert.Null(FilterValidator.Check(new("", "", ""), Fields));

    [Fact]
    public void Check_FlagsMissingOpAndEmptyValue() {
        Assert.Equal("no operator", FilterValidator.Check(new("ship", "", "3"), Fields));
        Assert.Equal("empty value", FilterValidator.Check(new("ship", "=", ""), Fields));
    }

    [Fact]
    public void Check_AcceptsStoredBoolAndAliases() {
        Assert.Null(FilterValidator.Check(new("ultra", "true", ""), Fields));
        Assert.Null(FilterValidator.Check(new("ship", "gte", "3"), Fields));
    }

    [Fact]
    public void Check_HonoursOpsOverride() =>
        Assert.NotNull(FilterValidator.Check(new("ship", ">", "3"), Fields, _ => FilterOps.Equality));
}
