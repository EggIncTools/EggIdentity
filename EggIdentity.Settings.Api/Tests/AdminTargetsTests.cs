namespace EggIdentity.Settings.Api.Tests;

public class AdminTargetsTests {
    private static AdminTargetRow Row(string name = "eggledger", string url = "http://eggledger:5015", bool enabled = true) =>
        new() { Name = name, AdminBaseUrl = url, Enabled = enabled };

    private static Func<string, string?> Env(params (string Key, string Value)[] pairs) {
        var map = pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return key => map.GetValueOrDefault(key);
    }

    [Fact]
    public void SecretEnvKey_IsDerivedFromTheAppName() {
        Assert.Equal("ADMIN_SECRET_EGGLEDGER", AdminTargets.SecretEnvKey("eggledger"));
        Assert.Equal("ADMIN_SECRET_EGGINCOGNITO_RUNNER", AdminTargets.SecretEnvKey("eggincognito-runner"));
        Assert.Equal("ADMIN_SECRET_EGG_ABACUS", AdminTargets.SecretEnvKey("egg.abacus"));
    }

    [Fact]
    public void AResolvedTarget_TakesItsSecretFromTheEnvironment_NotTheRow() {
        var target = AdminTargets.Resolve(Row(), Env(("ADMIN_SECRET_EGGLEDGER", "s3cret")));

        Assert.NotNull(target);
        Assert.Equal("eggledger", target.App);
        Assert.Equal(new Uri("http://eggledger:5015"), target.BaseUrl);
        Assert.Equal("s3cret", target.Secret);
    }

    [Fact]
    public void WithoutASecretInTheEnvironment_TheAppIsNotAdministrable() {
        Assert.Null(AdminTargets.Resolve(Row(), Env()));
        Assert.Null(AdminTargets.Resolve(Row(), Env(("ADMIN_SECRET_EGGLEDGER", "   "))));
    }

    [Fact]
    public void ADisabledRow_ResolvesToNothing() {
        Assert.Null(AdminTargets.Resolve(Row(enabled: false), Env(("ADMIN_SECRET_EGGLEDGER", "s3cret"))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("eggledger:5015")]
    [InlineData("/admin")]
    public void ARowWithoutAnAbsoluteUrl_IsNotUsable(string url) {
        var row = Row(url: url);

        Assert.False(row.IsUsable);
        Assert.Null(AdminTargets.Resolve(row, Env(("ADMIN_SECRET_EGGLEDGER", "s3cret"))));
    }

    [Fact]
    public void AMissingSecret_NamesTheVariableAnOperatorMustSet() {
        var status = AdminTargets.Describe(Row(), Env());

        Assert.False(status.Administrable);
        Assert.False(status.SecretPresent);
        Assert.Equal("ADMIN_SECRET_EGGLEDGER", status.SecretEnvKey);
        Assert.Equal("ADMIN_SECRET_EGGLEDGER is not set on this host", status.Unavailable);
    }

    [Fact]
    public void EachUnusableReason_IsDistinguishable() {
        Assert.Equal("disabled in admin.targets", Unavailable(Row(enabled: false)));
        Assert.Equal("admin base URL is not an absolute URL", Unavailable(Row(url: "eggledger:5015")));
        Assert.Equal("this row has no app name", Unavailable(Row(name: "")));

        static string? Unavailable(AdminTargetRow row) {
            return AdminTargets.Describe(row, Env(("ADMIN_SECRET_EGGLEDGER", "s3cret"))).Unavailable;
        }
    }

    [Theory]
    [InlineData("s3cret")]
    [InlineData("")]
    [InlineData("   ")]
    public void NoStringOnAStatus_EverCarriesTheSecretValue(string secret) {
        var statuses = new[] {
            AdminTargets.Describe(Row(), Env(("ADMIN_SECRET_EGGLEDGER", secret))),
            AdminTargets.Describe(Row(enabled: false), Env(("ADMIN_SECRET_EGGLEDGER", secret))),
            AdminTargets.Describe(Row(url: "nope"), Env(("ADMIN_SECRET_EGGLEDGER", secret))),
        };

        foreach (var status in statuses) {
            foreach (var text in Strings(status)) {
                Assert.DoesNotContain("s3cret", text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static IEnumerable<string> Strings(AdminTargetStatus status) =>
        typeof(AdminTargetStatus)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.GetValue(status) as string)
            .Where(value => value is not null)
            .Select(value => value!);

    [Fact]
    public void AnAvailableTarget_ReportsNoProblem() {
        var status = AdminTargets.Describe(Row(), Env(("ADMIN_SECRET_EGGLEDGER", "s3cret")));

        Assert.True(status.Administrable);
        Assert.True(status.SecretPresent);
        Assert.Null(status.Unavailable);
    }

    [Fact]
    public void TheDescriptorStoresNoSecretField() {
        Assert.DoesNotContain(AdminTargets.Descriptor.Fields, f => f.IsSecret);
        Assert.False(AdminTargets.Descriptor.HasSecrets);
        Assert.Equal(["name", "admin_base_url", "enabled"], AdminTargets.Descriptor.Fields.Select(f => f.Name));
    }
}
