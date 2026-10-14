namespace EggIdentity.Reports;

public enum Aggregation {
    Count,
    Sum,
    Avg,
    Min,
    Max,
    Distinct,
}

public enum MeasureKind {
    Scalar,
    Distribution,
}
