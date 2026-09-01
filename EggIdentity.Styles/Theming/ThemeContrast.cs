namespace EggIdentity.Styles.Theming;

public sealed record ContrastFailure(string Check, string A, string B, double Measured, double Required, double? AtHue);

public sealed record ContrastResult(bool Passes, IReadOnlyList<ContrastFailure> Failures);

public static class ThemeContrast {
    public const double FgFloor = 4.5;
    public const double MutedFloor = 3.0;
    public const double StatusFloor = 3.0;
    public const double BorderFloor = 1.15;
    public const double DistinguishFloor = 0.04;
    private const int HueSteps = 24;

    private static readonly string[] Surfaces = ["bg", "panel0", "panel", "panel2"];

    public static ContrastResult Validate(IReadOnlyDictionary<string, ThemeColor> colors, ThemeChroma chroma, IReadOnlyList<string> statusTokens) {
        var failures = new List<ContrastFailure>();

        foreach (string surface in Surfaces) {
            Check(failures, "contrast", "fg", surface, ThemeColor.Contrast(colors["fg"], colors[surface]), FgFloor);
            Check(failures, "contrast", "muted", surface, ThemeColor.Contrast(colors["muted"], colors[surface]), MutedFloor);
            Check(failures, "contrast", "border", surface, ThemeColor.Contrast(colors["border"], colors[surface]), BorderFloor);
            foreach (string status in statusTokens) {
                Check(failures, "contrast", status, surface, ThemeColor.Contrast(colors[status], colors[surface]), StatusFloor);
            }
        }

        for (int i = 0; i < statusTokens.Count; i++) {
            for (int j = i + 1; j < statusTokens.Count; j++) {
                double d = ThemeColor.DeltaE(colors[statusTokens[i]], colors[statusTokens[j]]);
                Check(failures, "distinguish", statusTokens[i], statusTokens[j], d, DistinguishFloor);
            }
        }

        if (chroma.HueRotate is { Enabled: true }) SweepHues(failures, colors, statusTokens);

        return new ContrastResult(failures.Count == 0, failures);
    }

    private static void SweepHues(List<ContrastFailure> failures, IReadOnlyDictionary<string, ThemeColor> colors, IReadOnlyList<string> statusTokens) {
        var worstContrast = new Dictionary<string, (double Value, double Hue)>(StringComparer.Ordinal);
        var worstDelta = new Dictionary<string, (double Value, double Hue)>(StringComparer.Ordinal);
        for (int i = 0; i < HueSteps; i++) {
            var rotated = colors["accent"].RotateHue(i * (360.0 / HueSteps));
            foreach (string surface in Surfaces) {
                double c = ThemeColor.Contrast(rotated, colors[surface]);
                if (!worstContrast.TryGetValue(surface, out var cur) || c < cur.Value)
                    worstContrast[surface] = (c, rotated.H);
            }

            foreach (string status in statusTokens.Where(s => s != "accent")) {
                double d = ThemeColor.DeltaE(rotated, colors[status]);
                if (!worstDelta.TryGetValue(status, out var cur) || d < cur.Value)
                    worstDelta[status] = (d, rotated.H);
            }
        }

        foreach (var (surface, (value, hue)) in worstContrast) {
            if (value < StatusFloor)
                failures.Add(new ContrastFailure("contrast", "accent", surface, Round(value), StatusFloor, hue));
        }

        foreach (var (status, (value, hue)) in worstDelta) {
            if (value < DistinguishFloor)
                failures.Add(new ContrastFailure("distinguish", "accent", status, Round(value), DistinguishFloor, hue));
        }
    }

    private static void Check(List<ContrastFailure> failures, string check, string a, string b, double measured, double required) {
        if (measured < required) failures.Add(new ContrastFailure(check, a, b, Round(measured), required, null));
    }

    private static double Round(double v) => Math.Round(v, 4);
}
