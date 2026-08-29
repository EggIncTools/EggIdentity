namespace EggIdentity.StyleVerify;

public sealed record StructuralKey(string Role, string AccessibleName, string DomPath) {
    public override string ToString() => $"{Role}[{AccessibleName}]@{DomPath}";
}
