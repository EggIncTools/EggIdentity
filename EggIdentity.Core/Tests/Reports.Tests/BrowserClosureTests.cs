namespace EggIdentity.Reports.Tests;

public class BrowserClosureTests {
    [Fact]
    public void CoreReferencesNoAspNetFramework() {
        var refs = typeof(ReportCube).Assembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain(refs, n => n!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }
}
