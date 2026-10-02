using EggIdentity.Styles.Css;

namespace EggIdentity.Styles.Tests;

public class MotionGuardTests {
    private static IReadOnlyList<string> Reasons(string css, params string[] allowed) =>
        [.. MotionGuard.Check(CssSheet.Parse(css), allowed).Select(v => v.Reason)];

    [Theory]
    [InlineData(".a { transition: color var(--motion-fast,120ms) var(--ease-standard,ease); }")]
    [InlineData(".a { transition: none; animation: none; }")]
    [InlineData(".a { transition: visibility 0s linear var(--motion-base,150ms); }")]
    [InlineData(".a { animation: k var(--motion-morph,220ms) var(--ease-standard,cubic-bezier(.2,0,0,1)) backwards; } @keyframes k { from { opacity: 0; } }")]
    public void TokenTimedMotionPasses(string css) {
        Assert.Empty(Reasons(css, "k"));
    }

    [Theory]
    [InlineData(".a { transition: color .12s; }", MotionGuard.LiteralDuration)]
    [InlineData(".a { animation-duration: 200ms; }", MotionGuard.LiteralDuration)]
    [InlineData(".a { transition: color var(--motion-fast) ease-out; }", MotionGuard.LiteralEasing)]
    [InlineData(".a { transition-timing-function: cubic-bezier(.4,0,.2,1); }", MotionGuard.LiteralEasing)]
    [InlineData(".a { transition: all var(--motion-fast); }", MotionGuard.TransitionAll)]
    [InlineData(".a { transition: var(--motion-fast); }", MotionGuard.TransitionAll)]
    [InlineData(".a { transition-property: all; }", MotionGuard.TransitionAll)]
    [InlineData("@keyframes spin { to { transform: rotate(1turn); } }", MotionGuard.LocalKeyframes)]
    public void LiteralMotionIsReported(string css, string reason) {
        Assert.Contains(reason, Reasons(css));
    }
}
