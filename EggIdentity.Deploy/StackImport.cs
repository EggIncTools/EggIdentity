using EggIdentity.Contract;

namespace EggIdentity.Deploy;

public enum StackImportState {
    New,
    Registered,
    Conflict,
}

public sealed record StackImportCandidate(StackService Service, string ContainerName, string ProposedName, StackImportState State, string? ExistingApp);

public static class StackImport {
    public static IReadOnlyList<StackImportCandidate> Plan(string stack, IReadOnlyList<StackService> services, IReadOnlyList<SuiteApp> rows) {
        ArgumentException.ThrowIfNullOrWhiteSpace(stack);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rows);
        return [.. services.Select(service => Classify(stack.Trim(), service, rows))];
    }

    public static bool NameTaken(string name, IReadOnlyList<SuiteApp> rows) {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.Any(r => string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, string?> Values(string stack, StackImportCandidate candidate, string name, string environment) {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        return new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = trimmed,
            ["display_name"] = trimmed,
            ["environment"] = environment,
            ["enabled"] = "true",
            ["stack"] = stack.Trim(),
            ["container"] = string.Equals(candidate.ContainerName, trimmed, StringComparison.Ordinal) ? null : candidate.ContainerName,
            ["auto_deploy"] = "false",
        };
    }

    private static StackImportCandidate Classify(string stack, StackService service, IReadOnlyList<SuiteApp> rows) {
        var container = string.IsNullOrWhiteSpace(service.ContainerName) ? service.Service : service.ContainerName.Trim();
        var registered = rows.FirstOrDefault(r =>
            string.Equals(r.Stack?.Trim(), stack, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.ContainerName, container, StringComparison.OrdinalIgnoreCase));
        if (registered is not null) return new StackImportCandidate(service, container, container, StackImportState.Registered, registered.Name);
        var state = NameTaken(container, rows) ? StackImportState.Conflict : StackImportState.New;
        return new StackImportCandidate(service, container, container, state, null);
    }
}
