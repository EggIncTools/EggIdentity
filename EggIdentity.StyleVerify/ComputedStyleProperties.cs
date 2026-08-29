using System.Collections.Immutable;

namespace EggIdentity.StyleVerify;

public static class ComputedStyleProperties {
    public static readonly ImmutableArray<string> Default = [
        "color", "background-color",
        "font-family", "font-size", "font-weight", "font-style", "line-height",
        "margin-top", "margin-right", "margin-bottom", "margin-left",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "border-top-style", "border-right-style", "border-bottom-style", "border-left-style",
        "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
        "border-radius",
        "width", "height",
        "display",
        "flex-direction", "flex-wrap", "justify-content", "align-items", "align-content", "flex-grow", "flex-shrink", "flex-basis", "gap",
        "grid-template-columns", "grid-template-rows", "grid-column", "grid-row",
        "box-shadow",
        "z-index",
        "position",
        "top", "right", "bottom", "left",
        "opacity",
        "transform",
    ];
}
