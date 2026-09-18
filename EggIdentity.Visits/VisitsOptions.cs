namespace EggIdentity.Visits;

public sealed record VisitsOptions(string Site) {
    public bool HostedBehindProxy { get; init; }
    public string BeaconPath { get; init; } = "/_visits";
    public TimeSpan SessionGap { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(60);
}
