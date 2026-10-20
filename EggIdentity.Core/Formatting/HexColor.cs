using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace EggIdentity.Formatting;

public static class HexColor {
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

    public static bool IsHex([NotNullWhen(true)] string? color) {
        if (color is not { Length: 4 or 7 } || color[0] != '#') return false;
        return color.AsSpan(1).IndexOfAnyExcept(HexDigits) < 0;
    }

    public static string? Valid(string? color) => IsHex(color) ? color : null;
}
