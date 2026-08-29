(function () {
  if (window.__modalScrollLockInit) return;
  window.__modalScrollLockInit = true;

  let depth = 0;
  let previousOverflow = "";

  window.modalScrollLock = function () {
    if (depth === 0) {
      previousOverflow = document.body.style.overflow;
      document.body.style.overflow = "hidden";
    }
    depth++;
  };

  window.modalScrollUnlock = function () {
    depth = Math.max(0, depth - 1);
    if (depth === 0) {
      document.body.style.overflow = previousOverflow;
    }
  };
})();
