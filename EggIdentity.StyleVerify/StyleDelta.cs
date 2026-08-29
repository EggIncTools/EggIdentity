namespace EggIdentity.StyleVerify;

public enum DeltaKind {
    ElementMissingInCandidate,
    ElementAddedInCandidate,
    PropertyChanged,
}

public sealed record StyleDelta(DeltaKind Kind, StructuralKey Key, string? Property, string? OldValue, string? NewValue) {
    public override string ToString() => Kind switch {
        DeltaKind.ElementMissingInCandidate => $"missing: {Key}",
        DeltaKind.ElementAddedInCandidate => $"added: {Key}",
        DeltaKind.PropertyChanged => $"{Key} {Property}: '{OldValue}' -> '{NewValue}'",
        _ => Kind.ToString(),
    };
}
