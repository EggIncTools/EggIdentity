using System.Collections.Immutable;

namespace EggIdentity.StyleVerify;

public static class SnapshotDiffer {
    public static ImmutableArray<StyleDelta> Diff(PageSnapshot golden, PageSnapshot candidate, double numericTolerance = 0.5) {
        var goldenByKey = golden.Elements.ToDictionary(e => e.Key, e => e);
        var candidateByKey = candidate.Elements.ToDictionary(e => e.Key, e => e);
        var deltas = ImmutableArray.CreateBuilder<StyleDelta>();

        foreach (var golden_ in golden.Elements) {
            if (!candidateByKey.TryGetValue(golden_.Key, out var matched)) {
                deltas.Add(new StyleDelta(DeltaKind.ElementMissingInCandidate, golden_.Key, null, null, null));
                continue;
            }
            DiffElement(golden_, matched, numericTolerance, deltas);
        }

        foreach (var candidate_ in candidate.Elements) {
            if (!goldenByKey.ContainsKey(candidate_.Key)) {
                deltas.Add(new StyleDelta(DeltaKind.ElementAddedInCandidate, candidate_.Key, null, null, null));
            }
        }

        return deltas.ToImmutable();
    }

    private static void DiffElement(ElementSnapshot golden, ElementSnapshot candidate, double numericTolerance, ImmutableArray<StyleDelta>.Builder deltas) {
        var properties = golden.Styles.Keys.Union(candidate.Styles.Keys, StringComparer.Ordinal);
        foreach (var property in properties) {
            var hasOld = golden.Styles.TryGetValue(property, out var oldValue);
            var hasNew = candidate.Styles.TryGetValue(property, out var newValue);
            if (!hasOld || !hasNew) {
                deltas.Add(new StyleDelta(DeltaKind.PropertyChanged, golden.Key, property, hasOld ? oldValue : null, hasNew ? newValue : null));
                continue;
            }
            if (!StyleValueComparer.AreEquivalent(oldValue!, newValue!, numericTolerance)) {
                deltas.Add(new StyleDelta(DeltaKind.PropertyChanged, golden.Key, property, oldValue, newValue));
            }
        }
    }
}
