(function () {
  if (window.__eggTooltipInit) return;
  window.__eggTooltipInit = true;

  const Edge = 8;
  const Gap = 10;
  let current = null;

  function popOf(trigger) {
    return trigger.querySelector(":scope > [data-tt-pop]");
  }

  function triggerOf(node) {
    return node instanceof Element ? node.closest("[data-tt]") : null;
  }

  function visibleRect(el) {
    const r = el.getBoundingClientRect();
    let top = r.top;
    let left = r.left;
    let right = r.right;
    let bottom = r.bottom;
    for (let p = el.parentElement; p && p !== document.body; p = p.parentElement) {
      const s = getComputedStyle(p);
      if (s.overflowX === "visible" && s.overflowY === "visible") continue;
      const c = p.getBoundingClientRect();
      top = Math.max(top, c.top);
      left = Math.max(left, c.left);
      right = Math.min(right, c.right);
      bottom = Math.min(bottom, c.bottom);
    }
    return { top, left, right: Math.max(left, right), bottom: Math.max(top, bottom) };
  }

  function place(trigger, pop) {
    const a = visibleRect(trigger);
    const center = (a.left + a.right) / 2;
    pop.classList.remove("tooltip-below");
    pop.style.setProperty("--tt-shift-x", "0px");
    pop.style.setProperty("--arrow-offset", "0px");
    pop.style.setProperty("--tt-left", center + "px");
    pop.style.setProperty("--tt-top", a.top + "px");
    const width = pop.offsetWidth;
    const height = pop.offsetHeight;
    const left = center - width / 2;
    const max = Math.max(Edge, window.innerWidth - width - Edge);
    const shift = Math.min(Math.max(left, Edge), max) - left;
    pop.style.setProperty("--tt-shift-x", shift + "px");
    pop.style.setProperty("--arrow-offset", -shift + "px");
    if (a.top - height - Gap < Edge) {
      pop.classList.add("tooltip-below");
      pop.style.setProperty("--tt-top", a.bottom + "px");
    }
  }

  function hide() {
    if (!current) return;
    const pop = popOf(current);
    current = null;
    if (!pop) return;
    pop.classList.remove("show");
    if (pop.hidePopover && pop.matches(":popover-open")) pop.hidePopover();
  }

  function show(trigger) {
    if (current === trigger) return;
    const pop = popOf(trigger);
    if (!pop) return;
    hide();
    current = trigger;
    if (pop.showPopover && !pop.matches(":popover-open")) pop.showPopover();
    place(trigger, pop);
    pop.classList.add("show");
  }

  document.addEventListener("pointerover", e => {
    const t = triggerOf(e.target);
    if (t) show(t);
  });
  document.addEventListener("pointerout", e => {
    if (current && !current.contains(e.relatedTarget)) hide();
  });
  document.addEventListener("focusin", e => {
    const t = triggerOf(e.target);
    if (t) show(t);
  });
  document.addEventListener("focusout", e => {
    if (current && !current.contains(e.relatedTarget)) hide();
  });
  document.addEventListener("keydown", e => {
    if (e.key === "Escape") hide();
  });
  window.addEventListener("scroll", hide, true);
  window.addEventListener("resize", hide);
})();
