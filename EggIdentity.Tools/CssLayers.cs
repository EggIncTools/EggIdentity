using System.Text;

namespace EggIdentity.Tools;

internal static class CssLayers {
    internal static string Unwrap(string compiled) {
        var result = new StringBuilder(compiled.Length);
        var i = 0;
        while (i < compiled.Length) {
            var layerIndex = compiled.IndexOf("@layer", i, StringComparison.Ordinal);
            if (layerIndex < 0) {
                result.Append(compiled, i, compiled.Length - i);
                break;
            }
            result.Append(compiled, i, layerIndex - i);
            var headEnd = layerIndex + "@layer".Length;
            while (headEnd < compiled.Length && compiled[headEnd] != '{' && compiled[headEnd] != ';') headEnd++;
            if (headEnd >= compiled.Length) break;
            if (compiled[headEnd] == ';') {
                i = headEnd + 1;
                continue;
            }
            var depth = 1;
            var bodyStart = headEnd + 1;
            var pos = bodyStart;
            while (pos < compiled.Length && depth > 0) {
                var c = compiled[pos];
                if (c == '{') depth++;
                else if (c == '}') depth--;
                pos++;
            }
            result.Append(compiled, bodyStart, pos - 1 - bodyStart);
            i = pos;
        }
        return result.ToString();
    }

    internal static string ExtractLayer(string compiled, string name) {
        var marker = "@layer " + name + " {";
        var start = compiled.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return "";
        var depth = 1;
        var bodyStart = start + marker.Length;
        var pos = bodyStart;
        while (pos < compiled.Length && depth > 0) {
            var c = compiled[pos];
            if (c == '{') depth++;
            else if (c == '}') depth--;
            pos++;
        }
        return compiled[bodyStart..(pos - 1)];
    }
}
