namespace EggIdentity.UI.Suite;

public enum SuitePlacement {
    Bar,
    Rail,
    Tabs,
    Drawer,
    Inline,
}

public static class SuitePlacements {
    public static string Css(SuitePlacement placement) =>
        placement switch {
            SuitePlacement.Rail => "suite-rail",
            SuitePlacement.Tabs => "suite-tabs",
            SuitePlacement.Drawer => "suite-drawer",
            SuitePlacement.Inline => "suite-inline",
            _ => "suite-bar",
        };
}
