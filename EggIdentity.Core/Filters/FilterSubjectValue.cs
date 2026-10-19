namespace EggIdentity.Filters;

public abstract record FilterSubjectValue {
    private FilterSubjectValue() {
    }

    public static FilterSubjectValue Absent { get; } = new AbsentValue();

    public static FilterSubjectValue Of(string? value) => value is null ? Absent : new TextValue(value);

    public static FilterSubjectValue Of(double? value) => value is { } v ? new NumberValue(v) : Absent;

    public static FilterSubjectValue Of(bool? value) => value is { } v ? new BoolValue(v) : Absent;

    public static FilterSubjectValue Of(DateTimeOffset? value) => value is { } v ? new DateValue(v) : Absent;

    public static FilterSubjectValue Of(IEnumerable<string>? values) => values is null ? Absent : new ManyValue([.. values]);

    public sealed record AbsentValue : FilterSubjectValue;

    public sealed record TextValue(string Value) : FilterSubjectValue;

    public sealed record NumberValue(double Value) : FilterSubjectValue;

    public sealed record BoolValue(bool Value) : FilterSubjectValue;

    public sealed record DateValue(DateTimeOffset Value) : FilterSubjectValue;

    public sealed record ManyValue(IReadOnlyList<string> Values) : FilterSubjectValue {
        public bool Equals(ManyValue? other) => other is not null && Values.SequenceEqual(other.Values, StringComparer.Ordinal);

        public override int GetHashCode() {
            var hash = new HashCode();
            foreach (var value in Values) hash.Add(value, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }
}
