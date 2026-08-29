using System.Collections.Immutable;

namespace EggIdentity.StyleVerify;

public sealed record ElementSnapshot(StructuralKey Key, ImmutableSortedDictionary<string, string> Styles);
