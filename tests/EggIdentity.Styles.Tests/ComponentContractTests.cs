using System.Linq;

namespace EggIdentity.Styles.Tests;

public class ComponentContractTests {
    [Fact]
    public void All_Keys_MatchGoldenSelectorSet() {
        var expected = new HashSet<string> {
            ".badge", ".badge:hover", ".badge.badge-ok", ".badge.badge-err", ".badge.badge-accent", ".badge.badge-accent2", ".badge.badge-muted",
            ".btn-primary", ".btn-primary:disabled", ".btn-primary:hover:not(:disabled)", ".btn-secondary", ".btn-secondary:hover", ".btn-accent", ".btn-accent:disabled", ".btn-accent:hover:not(:disabled)", ".btn-mini", ".btn-mini:hover", ".btn-danger.btn-danger", ".btn-danger.btn-danger:hover", ".icon-btn", ".icon-btn:hover", ".icon-btn.active",
            ".panel", ".panel-head", ".panel-title",
            ".segmented", ".segmented-opt", ".segmented-opt.active", ".segmented-opt:disabled",
            ".popover-wrap", ".popover", ".popover.open", ".popover.popover-sm", ".popover.popover-lg", ".popover.popover-combo", ".popover-combo-opt", ".popover-combo-opt:hover",
            ".modal-backdrop", ".modal-card", ".modal-head", ".modal-title", ".modal-body",
            ".fab-bubble", ".fab-bubble:hover",
            ".form-input", ".form-input:focus", ".form-select", ".form-select:focus", ".form-check",
            ".data-table", ".data-table thead th", ".data-table td", ".data-table tbody tr", ".data-table tbody tr:hover", ".stat-tile", ".stat-tile-label", ".stat-tile-value",
            ".toast-container", ".toast", ".toast.show", ".toast.leaving", ".toast-msg", ".toast-time", ".toast.toast-ok", ".toast.toast-err", ".toast.toast-info",
            ".tooltip-floating", ".tooltip-anchored", ".tooltip-floating::before", ".tooltip-floating::after",
            ".prose-legal", ".prose-legal h2", ".prose-legal p", ".prose-legal ul", ".prose-legal a", ".prose-legal a:hover",
        };

        Assert.Equal(expected, [.. ComponentClasses.All.Keys]);
    }

    [Fact]
    public void Tokens_MatchGoldenNames() {
        string[] expectedRequired = [
            "--color-bg", "--color-panel", "--color-panel2", "--color-fg", "--color-muted",
            "--color-accent", "--color-accent2", "--color-ok", "--color-err", "--color-border",
        ];
        Assert.Equal<string>(expectedRequired, [.. ComponentTokens.Required]);

        string[] expectedOptional = ["--color-panel0"];
        Assert.Equal<string>(expectedOptional, [.. ComponentTokens.Optional]);
    }
}
