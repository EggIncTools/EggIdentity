(function () {
  if (window.__eggDownloadInit) return;
  window.__eggDownloadInit = true;

  window.eggDownload = function (name, type, bytes) {
    const url = URL.createObjectURL(new Blob([bytes], { type: type }));
    const a = document.createElement("a");
    a.href = url;
    a.download = name;
    a.rel = "noopener";
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 0);
  };
})();
