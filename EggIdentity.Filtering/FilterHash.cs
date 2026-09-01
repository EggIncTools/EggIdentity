using System.Text;

namespace EggIdentity.Filtering;

public static class FilterHash {
    public static string Compute<TField>(string scope, Filter<TField> filter) where TField : notnull {
        var sb = new StringBuilder(scope);
        sb.Append('#');
        foreach (var group in filter.Groups) {
            sb.Append('(');
            foreach (var c in group.Conditions) {
                sb.Append(c.Field).Append('~').Append(c.Operator).Append('~').Append(c.Value).Append(';');
            }
            sb.Append(')');
        }
        return sb.ToString();
    }
}
