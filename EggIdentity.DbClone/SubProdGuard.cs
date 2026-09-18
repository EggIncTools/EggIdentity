using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.DbClone;

public static class SubProdGuard {
    public static string DatabaseName(string connString) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connString);
        if (Database.DescribeUriForm(connString) is { } uriError) throw new InvalidOperationException(uriError);
        var name = new NpgsqlConnectionStringBuilder(connString).Database;
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("connection string names no database");
        return name;
    }

    public static string EnsureDatabase(string connString, string expected) {
        ArgumentException.ThrowIfNullOrWhiteSpace(expected);
        var name = DatabaseName(connString);
        if (!string.Equals(name, expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"refusing to run against database \"{name}\", expected \"{expected}\"");
        return name;
    }

    public static void EnsureSubProd(Func<string, string?> environment) {
        var value = EnvironmentSettings.Resolve(environment);
        if (!EnvironmentSettings.IsSubProd(value)) {
            throw new InvalidOperationException(
                $"{EnvironmentSettings.EnvKey} is \"{value}\", expected \"{EnvironmentSettings.SubProd}\"");
        }
    }
}
