namespace EggIdentity.UI.Tests;

public class BrowserClosureTests {
    [Fact]
    public void UiReferencesOnlyComponentsFromAspNet() {
        var refs = typeof(EggIdentity.UI.Reports.ReportExplorer).Assembly.GetReferencedAssemblies().Select(a => a.Name);
        foreach (var name in refs) {
            if (name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
                Assert.StartsWith("Microsoft.AspNetCore.Components", name, StringComparison.Ordinal);
            Assert.NotEqual("EggIdentity.Auth", name);
        }
    }
}
