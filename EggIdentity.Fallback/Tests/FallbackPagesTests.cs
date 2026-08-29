using EggIdentity.Styles;

namespace EggIdentity.Fallback.Tests;

public class FallbackPagesTests {
    private static readonly FallbackBranding Branding = new("TestApp", new Dictionary<string, string> {
        ["--color-bg"] = "#0b0d12",
        ["--color-fg"] = "#f3f5f9",
    });

    [Fact]
    public void RenderError_AdminSeesStackTrace() {
        Exception exception;
        try { throw new InvalidOperationException("boom"); } catch (Exception e) { exception = e; }

        var html = FallbackPages.RenderError(Branding, exception, showTrace: true, correlationId: "corr-1");

        Assert.Contains("boom", html);
        Assert.Contains("InvalidOperationException", html);
        Assert.Contains("corr-1", html);
    }

    [Fact]
    public void RenderError_NonAdminDoesNotSeeStackTrace() {
        Exception exception;
        try { throw new InvalidOperationException("boom"); } catch (Exception e) { exception = e; }

        var html = FallbackPages.RenderError(Branding, exception, showTrace: false, correlationId: "corr-2");

        Assert.DoesNotContain("boom", html);
        Assert.DoesNotContain("InvalidOperationException", html);
        Assert.Contains("corr-2", html);
    }

    [Fact]
    public void RenderMaintenance_ContainsAppName() {
        var html = FallbackPages.RenderMaintenance(Branding);
        Assert.Contains("TestApp", html);
    }

    [Fact]
    public void RenderNotFound_ContainsAppName() {
        var html = FallbackPages.RenderNotFound(Branding);
        Assert.Contains("TestApp", html);
    }

    [Fact]
    public void RenderDown_AdminLink_ShowsLogsUrl() {
        var html = FallbackPages.RenderDown(Branding, showAdminLink: true, logsUrl: "/logs/testapp/tail");
        Assert.Contains("/logs/testapp/tail", html);
    }

    [Fact]
    public void RenderDown_NoAdminLink_HidesLogsUrl() {
        var html = FallbackPages.RenderDown(Branding, showAdminLink: false, logsUrl: "/logs/testapp/tail");
        Assert.DoesNotContain("/logs/testapp/tail", html);
    }

    [Fact]
    public void AllPages_EmitSuppliedTokenValues() {
        var html = FallbackPages.RenderNotFound(Branding);
        Assert.Contains("#0b0d12", html);
        Assert.Contains("#f3f5f9", html);
    }

    [Fact]
    public void StyleBlock_RejectsTokensWithAngleBrackets() {
        var maliciousBranding = new FallbackBranding("TestApp", new Dictionary<string, string> {
            ["--color-bg"] = "red</style><script>alert(1)</script>",
            ["--color-fg"] = "#fff",
        });
        var html = FallbackPages.RenderNotFound(maliciousBranding);
        Assert.DoesNotContain("</style><script>", html);
        Assert.DoesNotContain("alert(1)", html);
        Assert.Contains("--color-fg", html);
    }

    [Fact]
    public void StyleBlock_RejectsTokensWithOpenBracket() {
        var maliciousBranding = new FallbackBranding("TestApp", new Dictionary<string, string> {
            ["--color-bg"] = "red<script>",
        });
        var html = FallbackPages.RenderNotFound(maliciousBranding);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void StyleBlock_RejectsTokensWithCloseBracket() {
        var maliciousBranding = new FallbackBranding("TestApp", new Dictionary<string, string> {
            ["--color-bg"] = "red>evil",
        });
        var html = FallbackPages.RenderNotFound(maliciousBranding);
        Assert.DoesNotContain("red>evil", html);
    }

    [Fact]
    public void StyleBlock_RejectsSemicolonBreakoutWithoutAngleBrackets() {
        var maliciousBranding = new FallbackBranding("TestApp", new Dictionary<string, string> {
            ["--color-bg"] = "red; } body { display:none; } .x {color:red",
        });
        var html = FallbackPages.RenderNotFound(maliciousBranding);
        Assert.DoesNotContain("red; } body { display:none; } .x {color:red", html);
    }

    [Fact]
    public void RenderMaintenanceAdmin_ReflectsCurrentState() {
        var htmlOn = FallbackPages.RenderMaintenanceAdmin(Branding, isOn: true);
        Assert.Contains("/admin/maintenance/off", htmlOn);

        var htmlOff = FallbackPages.RenderMaintenanceAdmin(Branding, isOn: false);
        Assert.Contains("/admin/maintenance/on", htmlOff);
    }
}
