using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EggIdentity.Styles.Css;

public sealed record MotionViolation(string Selector, string Property, string Value, string Reason);

public static partial class MotionGuard {
    public const string LiteralDuration = "literal duration";
    public const string LiteralEasing = "literal easing";
    public const string TransitionAll = "transition: all";
    public const string LocalKeyframes = "local @keyframes";

    private const string KeyframesPrefix = "@keyframes ";

    private static readonly HashSet<string> TimedProperties = [
        "transition", "transition-duration", "transition-delay", "transition-timing-function",
        "animation", "animation-duration", "animation-delay", "animation-timing-function",
    ];

    [GeneratedRegex(@"(?<![\w.#-])(\d*\.?\d+)(ms|s)\b")]
    private static partial Regex Duration();

    [GeneratedRegex(@"\b(?:cubic-bezier|steps)\(|(?<![\w-])(?:ease|ease-in|ease-out|ease-in-out)(?![\w-])")]
    private static partial Regex Easing();

    public static IReadOnlyList<MotionViolation> Check(CssSheet sheet, IEnumerable<string>? allowedKeyframes = null) {
        ArgumentNullException.ThrowIfNull(sheet);
        var allowed = (allowedKeyframes ?? []).ToHashSet(StringComparer.Ordinal);
        var keyframes = sheet.Containers
            .Where(c => c.StartsWith(KeyframesPrefix, StringComparison.Ordinal) && !allowed.Contains(c[KeyframesPrefix.Length..].Trim()))
            .Select(c => new MotionViolation(c, "", "", LocalKeyframes));
        var rules = sheet.Rules
            .Where(r => r.Container is null || !r.Container.StartsWith(KeyframesPrefix, StringComparison.Ordinal))
            .SelectMany(CheckRule);
        return [.. keyframes, .. rules];
    }

    private static IEnumerable<MotionViolation> CheckRule(CssRule rule) {
        foreach (var decl in rule.Declarations.Where(d => TimedProperties.Contains(d.Property))) {
            foreach (var reason in Reasons(decl)) yield return new MotionViolation(rule.Selector, decl.Property, decl.Value, reason);
        }
        if (rule["transition-property"] is { } props && props.Split(',').Any(p => p.Trim() == "all")) {
            yield return new MotionViolation(rule.Selector, "transition-property", props, TransitionAll);
        }
    }

    private static IEnumerable<string> Reasons(CssDeclaration decl) {
        var bare = StripVars(decl.Value);
        if (Duration().Matches(bare).Any(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) != 0)) yield return LiteralDuration;
        if (Easing().IsMatch(bare)) yield return LiteralEasing;
        if (decl.Property == "transition" && ImpliesAll(decl.Value)) yield return TransitionAll;
    }

    private static bool ImpliesAll(string value) {
        if (value.Trim() is "none" or "initial" or "inherit" or "unset" or "revert") return false;
        foreach (var segment in SplitTopLevel(value)) {
            var first = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (first == "all" || first.StartsWith("var(", StringComparison.Ordinal) || Duration().IsMatch(first)) return true;
        }
        return false;
    }

    private static IEnumerable<string> SplitTopLevel(string value) {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < value.Length; i++) {
            if (value[i] == '(') {
                depth++;
            } else if (value[i] == ')') {
                depth--;
            } else if (value[i] == ',' && depth == 0) {
                yield return value[start..i].Trim();
                start = i + 1;
            }
        }
        yield return value[start..].Trim();
    }

    private static string StripVars(string value) {
        var sb = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length) {
            var at = value.IndexOf("var(", i, StringComparison.Ordinal);
            if (at < 0) {
                sb.Append(value, i, value.Length - i);
                break;
            }
            sb.Append(value, i, at - i).Append(' ');
            var depth = 1;
            var j = at + 4;
            while (j < value.Length && depth > 0) {
                if (value[j] == '(') depth++;
                else if (value[j] == ')') depth--;
                j++;
            }
            i = j;
        }
        return sb.ToString();
    }
}
