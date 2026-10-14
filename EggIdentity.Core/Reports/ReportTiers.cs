namespace EggIdentity.Reports;

public enum ReportTier {
    Live,
    Snapshot,
}

public sealed record ReportTierCaps(int MaxDimensions, int MaxCells, int MaxBuckets) {
    public static ReportTierCaps Unlimited { get; } = new(int.MaxValue, int.MaxValue, int.MaxValue);
}

public enum ReportRejectionKind {
    DimensionNotInGrain,
    BucketNotDerivable,
    MeasureNotMergeable,
    FilterNotInGrain,
    DimensionCap,
    CellCap,
    BucketCap,
    NoSnapshot,
    UnknownKey,
}

public sealed record ReportRejection(ReportRejectionKind Kind, string Key, string Message);

public sealed record ReportValidation(IReadOnlyList<ReportRejection> Rejections) {
    public static ReportValidation Ok { get; } = new([]);

    public bool Allowed => Rejections.Count == 0;
}

public sealed class ReportRejectedException : Exception {
    public ReportRejectedException() {
        Rejections = [];
    }

    public ReportRejectedException(string message) : base(message) {
        Rejections = [];
    }

    public ReportRejectedException(string message, Exception innerException) : base(message, innerException) {
        Rejections = [];
    }

    public ReportRejectedException(IReadOnlyList<ReportRejection> rejections) : base(Join(rejections)) {
        Rejections = rejections;
    }

    public IReadOnlyList<ReportRejection> Rejections { get; }

    private static string Join(IReadOnlyList<ReportRejection> rejections) {
        ArgumentNullException.ThrowIfNull(rejections);
        return string.Join(" ", rejections.Select(r => r.Message));
    }
}
