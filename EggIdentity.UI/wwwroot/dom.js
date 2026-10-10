export function insertAtCaret(el, text) {
  const start = el.selectionStart ?? el.value.length;
  const end = el.selectionEnd ?? start;
  el.setRangeText(text, start, end, "end");
  el.dispatchEvent(new Event("input", { bubbles: true }));
  return el.value;
}

export function scrollTo(el, top, left, smooth) {
  el.scrollTo({ top, left, behavior: smooth ? "smooth" : "instant" });
}
