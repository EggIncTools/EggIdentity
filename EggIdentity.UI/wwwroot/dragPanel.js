export function attach(panel, target) {
  const handle = panel.querySelector(":scope > .drag-handle");
  let start = null;
  let timer = 0;

  const report = () => {
    const r = panel.getBoundingClientRect();
    target.invokeMethodAsync("OnPlaced", Math.round(r.left), Math.round(r.top), Math.round(r.width), Math.round(r.height));
  };
  const clamp = (v, max) => Math.min(Math.max(0, v), Math.max(0, max));

  const down = e => {
    if (e.button !== 0 || !handle) return;
    const r = panel.getBoundingClientRect();
    start = { dx: e.clientX - r.left, dy: e.clientY - r.top };
    handle.setPointerCapture(e.pointerId);
    e.preventDefault();
  };
  const move = e => {
    if (!start) return;
    panel.style.left = clamp(e.clientX - start.dx, innerWidth - panel.offsetWidth) + "px";
    panel.style.top = clamp(e.clientY - start.dy, innerHeight - panel.offsetHeight) + "px";
  };
  const up = () => {
    if (!start) return;
    start = null;
    report();
  };

  handle?.addEventListener("pointerdown", down);
  handle?.addEventListener("pointermove", move);
  handle?.addEventListener("pointerup", up);
  handle?.addEventListener("pointercancel", up);

  let first = true;
  const sizes = new ResizeObserver(() => {
    if (first) { first = false; return; }
    clearTimeout(timer);
    timer = setTimeout(report, 250);
  });
  sizes.observe(panel);

  return {
    stop: () => {
      clearTimeout(timer);
      sizes.disconnect();
      handle?.removeEventListener("pointerdown", down);
      handle?.removeEventListener("pointermove", move);
      handle?.removeEventListener("pointerup", up);
      handle?.removeEventListener("pointercancel", up);
    },
  };
}
