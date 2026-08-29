using System.Globalization;
using System.Text.RegularExpressions;

namespace EggIdentity.StyleVerify;

public static partial class StyleValueComparer {
    [GeneratedRegex(@"^(-?\d+(?:\.\d+)?)(px|rem|em|%|deg)$")]
    private static partial Regex NumericWithUnit();

    public static bool AreEquivalent(string oldValue, string newValue, double numericTolerance = 0.5) {
        if (oldValue == newValue) return true;

        var oldMatch = NumericWithUnit().Match(oldValue);
        var newMatch = NumericWithUnit().Match(newValue);
        if (!oldMatch.Success || !newMatch.Success) return false;
        if (oldMatch.Groups[2].Value != newMatch.Groups[2].Value) return false;

        var oldNum = double.Parse(oldMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        var newNum = double.Parse(newMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        return Math.Abs(oldNum - newNum) <= numericTolerance;
    }
}
