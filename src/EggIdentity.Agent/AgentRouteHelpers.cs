namespace EggIdentity.Agent;

internal static class AgentRouteHelpers {
    public static int ClampLogLines(int? requested) => Math.Clamp(requested ?? 200, 1, 2000);
}
