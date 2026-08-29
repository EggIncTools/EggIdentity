using System.Collections.Immutable;
using EggIdentity.StyleVerify;

namespace EggIdentity.StyleVerify.Tests;

public class SnapshotDifferTests {
    private static ElementSnapshot Element(string role, string name, string path, params (string Prop, string Value)[] styles) =>
        new(new StructuralKey(role, name, path), styles.ToImmutableSortedDictionary(s => s.Prop, s => s.Value, StringComparer.Ordinal));

    [Fact]
    public void Diff_IdenticalSnapshots_ReturnsNoDeltas() {
        var snapshot = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)", ("color", "#ffffff"))]);

        var deltas = SnapshotDiffer.Diff(snapshot, snapshot);

        Assert.Empty(deltas);
    }

    [Fact]
    public void Diff_ChangedPropertyBeyondTolerance_ReportsPropertyChanged() {
        var golden = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)", ("color", "#ffffff"))]);
        var candidate = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)", ("color", "#000000"))]);

        var deltas = SnapshotDiffer.Diff(golden, candidate);

        var delta = Assert.Single(deltas);
        Assert.Equal(DeltaKind.PropertyChanged, delta.Kind);
        Assert.Equal("color", delta.Property);
        Assert.Equal("#ffffff", delta.OldValue);
        Assert.Equal("#000000", delta.NewValue);
    }

    [Fact]
    public void Diff_ChangedPropertyWithinNumericTolerance_ReportsNoDelta() {
        var golden = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)", ("width", "100px"))]);
        var candidate = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)", ("width", "100.3px"))]);

        var deltas = SnapshotDiffer.Diff(golden, candidate);

        Assert.Empty(deltas);
    }

    [Fact]
    public void Diff_ElementMissingInCandidate_ReportsElementMissing() {
        var golden = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)")]);
        var candidate = new PageSnapshot("home", []);

        var deltas = SnapshotDiffer.Diff(golden, candidate);

        var delta = Assert.Single(deltas);
        Assert.Equal(DeltaKind.ElementMissingInCandidate, delta.Kind);
    }

    [Fact]
    public void Diff_ElementAddedInCandidate_ReportsElementAdded() {
        var golden = new PageSnapshot("home", []);
        var candidate = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)")]);

        var deltas = SnapshotDiffer.Diff(golden, candidate);

        var delta = Assert.Single(deltas);
        Assert.Equal(DeltaKind.ElementAddedInCandidate, delta.Kind);
    }

    [Fact]
    public void Diff_PropertyOnlyOnOneSide_ReportsPropertyChangedWithNullOtherSide() {
        var golden = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)", ("box-shadow", "none"))]);
        var candidate = new PageSnapshot("home", [Element("button", "Save", "button:nth-of-type(1)")]);

        var deltas = SnapshotDiffer.Diff(golden, candidate);

        var delta = Assert.Single(deltas);
        Assert.Equal(DeltaKind.PropertyChanged, delta.Kind);
        Assert.Equal("box-shadow", delta.Property);
        Assert.Equal("none", delta.OldValue);
        Assert.Null(delta.NewValue);
    }
}
