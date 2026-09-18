using System.Text.Json.Serialization;

namespace EggIdentity.Contract;

public sealed class ConsentResponse {
    [JsonPropertyName("functional")]
    public bool Functional { get; set; }

    [JsonPropertyName("analytics")]
    public bool Analytics { get; set; }

    [JsonPropertyName("policyVersion")]
    public int PolicyVersion { get; set; }

    [JsonPropertyName("decidedAt")]
    public DateTimeOffset DecidedAt { get; set; }
}

public sealed class ConsentRequest {
    [JsonPropertyName("functional")]
    public bool Functional { get; set; }

    [JsonPropertyName("analytics")]
    public bool Analytics { get; set; }

    [JsonPropertyName("policyVersion")]
    public int PolicyVersion { get; set; }

    [JsonPropertyName("decidedAt")]
    public DateTimeOffset DecidedAt { get; set; }
}
