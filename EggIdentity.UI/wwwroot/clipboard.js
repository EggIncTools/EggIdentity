(function () {
  if (window.__eggClipboardInit) return;
  window.__eggClipboardInit = true;

  window.eggClipboardWrite = async function (text) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch (e) {
    }
    const area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "");
    area.style.position = "fixed";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.select();
    let ok = false;
    try {
      ok = document.execCommand("copy");
    } catch (e) {
      ok = false;
    }
    area.remove();
    return ok;
  };
})();
