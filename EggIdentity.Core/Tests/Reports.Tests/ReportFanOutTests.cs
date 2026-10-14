namespace EggIdentity.Reports.Tests;

public class ReportFanOutTests {
    private sealed record Consume(string Id, string Cell, IReadOnlyList<Out> Drops);
    private sealed record Out(string Name, int Count);
    private sealed record Row(string Event, string Item, int Count);

    private static IReadOnlyList<Row> Run(IEnumerable<Consume> events) {
        return ReportFanOut.Rows(
            events,
            e => e.Cell,
            e => e.Drops,
            o => o.Name,
            (e, key, item) => new Row(e.Id, key, item?.Count ?? 0));
    }

    [Fact]
    public void ZeroFillsCandidatesWithinCell() {
        var events = new[] {
            new Consume("e1", "a", [new Out("gusset", 2)]),
            new Consume("e2", "a", [new Out("feather", 1)]),
        };

        var rows = Run(events);

        Assert.Equal(
            [
                new Row("e1", "feather", 0),
                new Row("e1", "gusset", 2),
                new Row("e2", "feather", 1),
                new Row("e2", "gusset", 0),
            ],
            rows);
    }

    [Fact]
    public void CandidatesDoNotCrossCells() {
        var events = new[] {
            new Consume("e1", "a", [new Out("gusset", 1)]),
            new Consume("e2", "b", [new Out("feather", 1)]),
        };

        var rows = Run(events);

        Assert.Equal(
            [
                new Row("e1", "gusset", 1),
                new Row("e2", "feather", 1),
            ],
            rows);
    }

    [Fact]
    public void EventWithNoDropsGetsAllZeros() {
        var events = new[] {
            new Consume("e1", "a", [new Out("gusset", 3)]),
            new Consume("e2", "a", []),
        };

        var rows = Run(events);

        Assert.Contains(new Row("e2", "gusset", 0), rows);
    }

    [Fact]
    public void DuplicateItemEmitsPerOccurrence() {
        var events = new[] {
            new Consume("e1", "a", [new Out("gusset", 1), new Out("gusset", 4)]),
        };

        var rows = Run(events);

        Assert.Equal(
            [
                new Row("e1", "gusset", 1),
                new Row("e1", "gusset", 4),
            ],
            rows);
    }

    [Fact]
    public void CellWithNoItemsEmitsNothing() {
        var events = new[] {
            new Consume("e1", "a", []),
        };

        var rows = Run(events);

        Assert.Empty(rows);
    }
}
