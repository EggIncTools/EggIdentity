namespace EggIdentity.Styles;

public static class SharedLiterals {
    public const string Tail = """
        @keyframes pulse {
          50% {
            opacity: .5;
          }
        }
        @keyframes pane-in {
          from {
            opacity: 0;
            transform: translateX(var(--pane-shift,-0.75rem));
          }
        }
        @keyframes pane-in-reverse {
          from {
            opacity: 0;
            transform: translateX(calc(-1 * var(--pane-shift,-0.75rem)));
          }
        }
        body:has(.modal-backdrop) {
          overflow: hidden;
        }
        @container anchored(fallback: flip-block) {
          .tooltip-floating.tooltip-fixed::before, .tooltip-floating.tooltip-fixed::after {
            top: auto;
            bottom: 100%;
            border-top-color: transparent;
          }
          .tooltip-floating.tooltip-fixed::before {
            border-bottom-color: var(--tooltip-border,var(--color-border));
          }
          .tooltip-floating.tooltip-fixed::after {
            border-bottom-color: var(--tooltip-bg,rgba(33,37,41,.95));
          }
        }
        @media (prefers-reduced-motion: reduce) {
          .modal-card, .modal-card.wb-card, .wb-rail, .wb-main, .wb-drawer, .wb-drawer.wb-drawer-open, .wb-drawer-tab, .tooltip-floating, .popover, .toast, .cal-period, .cal-row, .cal-range-trigger, .fab-bubble, .progress-fill, .caret, .mask, .menu, .menu.open {
            transition: none;
          }
          .pulse, .pane-in, .pane-in-reverse, .wb-pane-in {
            animation: none;
          }
        }
        """;
}
