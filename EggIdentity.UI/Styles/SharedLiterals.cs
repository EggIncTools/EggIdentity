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
        @keyframes scroll-marker {
          to {
            top: calc(100% - var(--scroll-marker-h,12%));
          }
        }
        @supports not (animation-timeline: scroll()) {
          .scroll-marker {
            animation: none;
          }
        }
        body:has(.modal-backdrop) {
          overflow: hidden;
        }
        @position-try --tt-top-right {
          position-area: top span-right;
          justify-self: start;
          margin: 0 0 10px;
        }
        @position-try --tt-top-left {
          position-area: top span-left;
          justify-self: end;
          margin: 0 0 10px;
        }
        @position-try --tt-bottom-right {
          position-area: bottom span-right;
          justify-self: start;
          margin: 10px 0 0;
        }
        @position-try --tt-bottom-left {
          position-area: bottom span-left;
          justify-self: end;
          margin: 10px 0 0;
        }
        @container anchored(fallback: flip-block) or anchored(fallback: --tt-bottom-right) or anchored(fallback: --tt-bottom-left) {
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
        @container anchored(fallback: --tt-top-right) or anchored(fallback: --tt-bottom-right) {
          .tooltip-floating.tooltip-fixed::before, .tooltip-floating.tooltip-fixed::after {
            left: 16px;
          }
        }
        @container anchored(fallback: --tt-top-left) or anchored(fallback: --tt-bottom-left) {
          .tooltip-floating.tooltip-fixed::before, .tooltip-floating.tooltip-fixed::after {
            left: calc(100% - 16px);
          }
        }
        @supports (anchor-name: --m) {
          .menu-wrap {
            anchor-name: --menu;
            anchor-scope: --menu;
          }
          .menu, .menu.open {
            position: fixed;
            position-anchor: --menu;
            position-area: bottom span-left;
            position-try-fallbacks: flip-block;
            inset: auto;
            margin: 6px 0 0;
          }
          .menu.menu-start {
            position-area: bottom span-right;
          }
        }
        @supports (anchor-name: --tt) and (not (container-type: anchored)) {
          .tooltip-floating.tooltip-fixed::before, .tooltip-floating.tooltip-fixed::after {
            display: none;
          }
        }
        @supports not (anchor-name: --tt) {
          .tooltip-trigger, .tooltip-point {
            position: relative;
          }
          .tooltip-point {
            position: absolute;
          }
          .tooltip-floating.tooltip-fixed {
            position: absolute;
            left: 50%;
            bottom: 100%;
            top: auto;
            margin: 0;
            transform: translateX(calc(-50% + var(--tt-shift-x,0px))) translateY(-10px) scale(var(--tt-scale,1));
          }
        }
        @media (max-width: 44rem) {
          .suite-switcher.suite-bar {
            flex-wrap: wrap;
            gap: 0;
            padding: 0;
          }
          .suite-bar .suite-wordmark {
            order: 1;
            padding: var(--suite-wordmark-padding-narrow,0.5rem 0.6rem);
          }
          .suite-account.suite-bar {
            order: 2;
            margin-left: auto;
            padding: var(--suite-account-padding-narrow,0 1.1rem 0 0.5rem);
          }
          .suite-bar .suite-tools-nav {
            order: 3;
            flex-basis: 100%;
          }
          .suite-bar .suite-tools {
            flex: 1;
            justify-content: space-evenly;
            border-inline: 0;
            border-top: var(--suite-tools-border,1px solid var(--color-border));
          }
          .suite-bar .suite-tool-item {
            flex: 1;
          }
          .suite-bar .suite-tool {
            flex: 1;
            padding: var(--suite-tool-padding-narrow,0.45rem 0.4rem);
            font-size: var(--suite-tool-size-narrow,0.85em);
          }
          .suite-bar .suite-tool.suite-tool-soon {
            margin-left: 0;
          }
        }
        @container (max-width: 34rem) {
          .fb-row {
            flex-wrap: wrap;
          }
          .fb-field, .fb-op {
            flex: 1 1 calc(50% - 0.5rem);
          }
          .fb-value {
            flex-basis: 100%;
          }
        }
        @media (prefers-reduced-motion: reduce) {
          .fb-select, .fb-btn,
          .modal-card, .modal-card.wb-card, .wb-rail, .wb-main, .wb-drawer, .wb-drawer.wb-drawer-open, .wb-drawer-tab, .tooltip-floating, .popover, .toast, .cal-period, .cal-row, .suite-tool, .fab-bubble, .progress-fill, .caret, .mask, .menu, .menu.open, .drop-zone,
          .report-bar, .report-point, .report-slice, .report-heat-cell, .report-dim, .report-builder-add, .report-builder-remove, .report-color-swatch, .report-card {
            transition: none;
          }
          .pulse, .pane-in, .pane-in-reverse, .wb-pane-in {
            animation: none;
          }
          .scroll-marker {
            animation-timing-function: step-end;
          }
        }
        """;
}
