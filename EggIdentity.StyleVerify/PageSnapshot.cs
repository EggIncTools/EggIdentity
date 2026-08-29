using System.Collections.Immutable;

namespace EggIdentity.StyleVerify;

public sealed record PageSnapshot(string Scenario, ImmutableArray<ElementSnapshot> Elements);
