export function attach(stage, target, cellSize) {
  const grid = () => stage.querySelector(":scope > .canvas-grid");
  let drag = null;

  const cellOf = (px) => Math.round(px / cellSize);
  const stageRect = () => grid().getBoundingClientRect();

  const begin = (e) => {
    if (e.button !== 0) return;
    const handle = e.target.closest("[data-canvas-handle]");
    const item = e.target.closest("[data-canvas-item]");
    const palette = e.target.closest("[data-canvas-kind]");
    if (palette) {
      drag = { kind: palette.dataset.canvasKind, palette: true };
      palette.setPointerCapture(e.pointerId);
      e.preventDefault();
      return;
    }
    if (!item) return;
    const r = item.getBoundingClientRect();
    drag = {
      id: item.dataset.canvasItem,
      el: item,
      dir: handle ? handle.dataset.canvasHandle : null,
      startX: e.clientX,
      startY: e.clientY,
      left: r.left,
      top: r.top,
      width: r.width,
      height: r.height,
      moved: false,
    };
    item.setPointerCapture(e.pointerId);
    item.classList.add("dragging");
    e.preventDefault();
    target.invokeMethodAsync("OnPicked", drag.id);
  };

  const preview = (e) => {
    if (!drag || drag.palette) return;
    const dx = e.clientX - drag.startX;
    const dy = e.clientY - drag.startY;
    if (Math.abs(dx) + Math.abs(dy) > 2) drag.moved = true;
    const s = stageRect();
    let { left, top, width, height } = drag;
    if (!drag.dir) {
      left += dx;
      top += dy;
    } else {
      if (drag.dir.includes("e")) width += dx;
      if (drag.dir.includes("s")) height += dy;
      if (drag.dir.includes("w")) { left += dx; width -= dx; }
      if (drag.dir.includes("n")) { top += dy; height -= dy; }
    }
    const snapped = {
      x: Math.max(0, cellOf(left - s.left)),
      y: Math.max(0, cellOf(top - s.top)),
      w: Math.max(1, cellOf(width)),
      h: Math.max(1, cellOf(height)),
    };
    drag.result = snapped;
    drag.el.style.left = snapped.x * cellSize + "px";
    drag.el.style.top = snapped.y * cellSize + "px";
    drag.el.style.width = snapped.w * cellSize + "px";
    drag.el.style.height = snapped.h * cellSize + "px";
  };

  const end = (e) => {
    if (!drag) return;
    const d = drag;
    drag = null;
    if (d.palette) {
      const s = stageRect();
      if (e.clientX >= s.left && e.clientX <= s.right && e.clientY >= s.top && e.clientY <= s.bottom) {
        target.invokeMethodAsync("OnDropped", d.kind, cellOf(e.clientX - s.left) + 1, cellOf(e.clientY - s.top) + 1);
      }
      return;
    }
    d.el.classList.remove("dragging");
    if (!d.moved || !d.result) return;
    const r = d.result;
    if (d.dir) target.invokeMethodAsync("OnResized", d.id, r.x + 1, r.y + 1, r.w, r.h);
    else target.invokeMethodAsync("OnMoved", d.id, r.x + 1, r.y + 1);
  };

  const root = stage.parentElement ?? stage;
  root.addEventListener("pointerdown", begin);
  root.addEventListener("pointermove", preview);
  root.addEventListener("pointerup", end);
  root.addEventListener("pointercancel", end);

  return {
    stop: () => {
      root.removeEventListener("pointerdown", begin);
      root.removeEventListener("pointermove", preview);
      root.removeEventListener("pointerup", end);
      root.removeEventListener("pointercancel", end);
    },
  };
}
