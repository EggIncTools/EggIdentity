using EggIdentity.Filtering;

namespace EggIdentity.Filtering.Tests;

public class FilterHashTests {
    private enum TestField { Ship, Level }

    private static Filter<TestField> BuildFilter(int code) => new([
        new FilterGroup<TestField>([
            new Condition<TestField>(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(code)),
        ]),
    ]);

    [Fact]
    public void Compute_isDeterministicForSameInput() {
        var filter = BuildFilter(0);

        var first = FilterHash.Compute("scope", filter);
        var second = FilterHash.Compute("scope", filter);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_differsWhenConditionValueDiffers() {
        var a = FilterHash.Compute("scope", BuildFilter(0));
        var b = FilterHash.Compute("scope", BuildFilter(1));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Compute_differsWhenScopeDiffersOnEmptyFilter() {
        var filter = Filter<TestField>.Empty;

        var a = FilterHash.Compute("scope-a", filter);
        var b = FilterHash.Compute("scope-b", filter);

        Assert.NotEqual(a, b);
    }
}
