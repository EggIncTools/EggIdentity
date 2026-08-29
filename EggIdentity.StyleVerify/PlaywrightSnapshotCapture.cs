using System.Collections.Immutable;
using Microsoft.Playwright;

namespace EggIdentity.StyleVerify;

public sealed record CapturedElement(string Role, string AccessibleName, string DomPath, Dictionary<string, string> Styles);

public static class PlaywrightSnapshotCapture {
    private const string CaptureScript = """
        ({ rootSelector, props }) => {
            const root = document.querySelector(rootSelector);
            if (!root) return [];

            function domPath(el) {
                const parts = [];
                let node = el;
                while (node && node !== document.body && node.parentElement) {
                    const parent = node.parentElement;
                    const siblings = Array.from(parent.children).filter(c => c.tagName === node.tagName);
                    const idx = siblings.indexOf(node) + 1;
                    parts.unshift(`${node.tagName.toLowerCase()}:nth-of-type(${idx})`);
                    node = parent;
                }
                return parts.join('>');
            }

            function accessibleName(el) {
                return el.getAttribute('aria-label') || el.getAttribute('alt') || el.getAttribute('title') || (el.textContent || '').trim().slice(0, 80);
            }

            function role(el) {
                return el.getAttribute('role') || el.tagName.toLowerCase();
            }

            const results = [];
            for (const el of root.querySelectorAll('*')) {
                const computed = getComputedStyle(el);
                const styles = {};
                for (const p of props) styles[p] = computed.getPropertyValue(p);
                results.push({ role: role(el), accessibleName: accessibleName(el), domPath: domPath(el), styles });
            }
            return results;
        }
        """;

    public static async Task<PageSnapshot> CaptureAsync(IPage page, string scenario, string rootSelector = "body", ImmutableArray<string>? properties = null) {
        var props = properties ?? ComputedStyleProperties.Default;
        var captured = await page.EvaluateAsync<List<CapturedElement>>(CaptureScript, new { rootSelector, props });

        var elements = captured
            .Select(c => new ElementSnapshot(
                new StructuralKey(c.Role, c.AccessibleName, c.DomPath),
                c.Styles.ToImmutableSortedDictionary(StringComparer.Ordinal)))
            .ToImmutableArray();

        return new PageSnapshot(scenario, elements);
    }
}
