using System.Collections.Immutable;
using EggIdentity.StyleVerify;

namespace EggIdentity.StyleVerify.Tests;

public class SnapshotSerializerTests {
    [Fact]
    public void ToJson_FromJson_RoundTrips() {
        var styles = new Dictionary<string, string> { { "color", "#ffffff" }, { "width", "100px" } }
            .ToImmutableSortedDictionary(StringComparer.Ordinal);
        var original = new PageSnapshot("home", [new ElementSnapshot(new StructuralKey("button", "Save", "button:nth-of-type(1)"), styles)]);

        var json = SnapshotSerializer.ToJson(original);
        var roundTripped = SnapshotSerializer.FromJson(json);

        Assert.Equal(original.Scenario, roundTripped.Scenario);
        var originalElement = Assert.Single(original.Elements);
        var roundTrippedElement = Assert.Single(roundTripped.Elements);
        Assert.Equal(originalElement.Key, roundTrippedElement.Key);
        Assert.Equal(originalElement.Styles, roundTrippedElement.Styles, StructuralEqualityComparer.Instance);
    }

    private sealed class StructuralEqualityComparer : IEqualityComparer<ImmutableSortedDictionary<string, string>> {
        public static readonly StructuralEqualityComparer Instance = new();

        public bool Equals(ImmutableSortedDictionary<string, string>? x, ImmutableSortedDictionary<string, string>? y) =>
            x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(ImmutableSortedDictionary<string, string> obj) => obj.Count;
    }
}
