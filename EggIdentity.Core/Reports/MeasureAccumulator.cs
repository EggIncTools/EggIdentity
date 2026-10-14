using EggIdentity.Filters;

namespace EggIdentity.Reports;

internal sealed class MeasureAccumulator(MeasureRequest request, ReportMeasure? measure) {
    private readonly HashSet<string> _entities = [];
    private readonly HashSet<string> _distinct = [];
    private readonly List<double> _numbers = [];
    private long _rows;

    public void Add(Func<string, FilterSubjectValue> value) {
        if (measure?.DistinctBy is { } by && (ReportValues.First(value(by)) is not { } entity || !_entities.Add(entity))) return;
        _rows++;
        if (request.IsRowCount || measure is null) return;
        var v = value(measure.Key);
        if (request.Aggregation == Aggregation.Distinct) {
            foreach (var text in ReportValues.Texts(v)) _distinct.Add(text);
            return;
        }
        if (v is FilterSubjectValue.NumberValue n && double.IsFinite(n.Value)) _numbers.Add(n.Value);
    }

    public double? Result() => request.Aggregation switch {
        Aggregation.Count => _rows,
        Aggregation.Distinct => _distinct.Count,
        Aggregation.Sum => _numbers.Sum(),
        Aggregation.Avg => _numbers.Count == 0 ? null : _numbers.Average(),
        Aggregation.Min => _numbers.Count == 0 ? null : _numbers.Min(),
        Aggregation.Max => _numbers.Count == 0 ? null : _numbers.Max(),
        _ => null,
    };

    public Distribution? Distribution() {
        if (measure is not { Kind: MeasureKind.Distribution }) return null;
        if (_numbers.Count == 0) return new Distribution([]);
        var total = (double)_numbers.Count;
        var binning = measure.Binning;
        return new Distribution([.. _numbers
            .GroupBy(v => Bin(v, binning))
            .OrderBy(g => g.Key)
            .Select(g => new DistributionBucket(g.Key, g.Count(), g.Count() / total))]);
    }

    private static double Bin(double value, double? binning) =>
        binning is { } b && b > 0 ? Math.Floor(value / b) * b : value;

    public static double? Empty(Aggregation aggregation) =>
        aggregation is Aggregation.Count or Aggregation.Distinct or Aggregation.Sum ? 0 : null;
}
