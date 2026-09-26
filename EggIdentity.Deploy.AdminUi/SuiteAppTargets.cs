using EggIdentity.Settings.Api;

namespace EggIdentity.Deploy.AdminUi;

public static class SuiteAppTargets {
    public static AdminTargetStatus Describe(SuiteApp app, Func<string, string?> environment) {
        ArgumentNullException.ThrowIfNull(app);
        return AdminTargets.Describe(
            new AdminTargetRow { Name = app.Name, AdminBaseUrl = app.AdminBaseUrl ?? "", Enabled = app.Enabled },
            environment);
    }
}
