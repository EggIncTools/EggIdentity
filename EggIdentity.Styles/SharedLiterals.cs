namespace EggIdentity.Styles;

public static class SharedLiterals {
    public const string Tail = """
        @keyframes wb-pane-in {
          from {
            opacity: 0;
            transform: translateX(var(--wb-pane-shift,-0.75rem));
          }
        }
        @media (prefers-reduced-motion: reduce) {
          .modal-card.wb-card, .wb-rail, .wb-main, .wb-drawer, .wb-drawer.wb-drawer-open, .wb-drawer-tab {
            transition: none;
          }
          .wb-pane-in {
            animation: none;
          }
        }
        """;
}
