(function () {
  if (window.__pathRouteSyncInit) return;
  window.__pathRouteSyncInit = true;

  const registry = new Map();

  function matchPrefix(pathname) {
    let best = null;
    for (const prefix of registry.keys()) {
      if (pathname.startsWith(prefix)) {
        if (best === null || prefix.length > best.length) best = prefix;
      }
    }
    return best;
  }

  document.addEventListener("click", function (e) {
    const a = e.target.closest("a");
    if (!a) return;
    if (a.origin !== window.location.origin) return;

    const prefix = matchPrefix(a.pathname);
    if (prefix === null) return;

    e.preventDefault();
    history.pushState(null, "", a.href);
    registry.get(prefix).invokeMethodAsync("OnPathChanged", a.pathname);
  }, true);

  window.addEventListener("popstate", function () {
    const prefix = matchPrefix(location.pathname);
    if (prefix === null) return;
    registry.get(prefix).invokeMethodAsync("OnPathChanged", location.pathname);
  });

  window.pathRouteSyncListen = function (prefix, dotNetRef) {
    registry.set(prefix, dotNetRef);
  };

  window.pathRouteSyncUnlisten = function (prefix) {
    registry.delete(prefix);
  };

  window.pathRouteSyncPush = function (path, replace) {
    if (replace) {
      history.replaceState(null, "", path);
    } else {
      history.pushState(null, "", path);
    }
  };
})();
