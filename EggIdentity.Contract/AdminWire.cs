namespace EggIdentity.Contract;

public sealed record AdminSettingWire {
    public string Key { get; init; } = "";
    public string EnvKey { get; init; } = "";
    public string Label { get; init; } = "";
    public string Category { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Tier { get; init; } = "";
    public string Source { get; init; } = "";
    public string? Description { get; init; }
    public string? Display { get; init; }
    public bool Required { get; init; }
    public bool Secret { get; init; }
    public bool Editable { get; init; }
    public bool PendingRestart { get; init; }
    public bool AllowBootstrapEdit { get; init; }
    public string? Default { get; init; }
    public IReadOnlyList<string> EnumValues { get; init; } = [];
}

public sealed record AdminSettingsResponse {
    public string App { get; init; } = "";
    public IReadOnlyList<AdminSettingWire> Settings { get; init; } = [];
    public IReadOnlyList<string> PendingRestartKeys { get; init; } = [];
}

public sealed record AdminSaveRequest {
    public string? Value { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed record AdminSaveResponse {
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public bool RestartRequired { get; init; }
}

public sealed record AdminFieldWire {
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string Kind { get; init; } = "";
    public string? Description { get; init; }
    public bool Required { get; init; }
    public bool Secret { get; init; }
    public IReadOnlyList<string> EnumValues { get; init; } = [];
}

public sealed record AdminCollectionWire {
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Category { get; init; } = "";
    public string? Description { get; init; }
    public string IdField { get; init; } = "";
    public IReadOnlyList<AdminFieldWire> Fields { get; init; } = [];
}

public sealed record AdminCollectionRowWire {
    public string Collection { get; init; } = "";
    public string Id { get; init; } = "";
    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>();
    public DateTimeOffset UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed record AdminCollectionResponse {
    public AdminCollectionWire Descriptor { get; init; } = new();
    public IReadOnlyList<AdminCollectionRowWire> Rows { get; init; } = [];
}

public sealed record AdminRowRequest {
    public string Id { get; init; } = "";
    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>();
    public string? UpdatedBy { get; init; }
}

public sealed record AdminDriftEntryWire {
    public string Key { get; init; } = "";
    public string Reason { get; init; } = "";
    public string? Origin { get; init; }
    public string? Detail { get; init; }
}

public sealed record AdminDriftResponse {
    public string App { get; init; } = "";
    public bool Available { get; init; }
    public string? Unavailable { get; init; }
    public int ProblemCount { get; init; }
    public IReadOnlyList<AdminDriftEntryWire> Entries { get; init; } = [];
}

public sealed record AdminAppWire {
    public string Name { get; init; } = "";
    public string? PublicUrl { get; init; }
    public string Environment { get; init; } = "";
    public bool Reachable { get; init; }
}
