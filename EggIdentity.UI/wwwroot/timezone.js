(function () {
  const name = "tz=";

  window.eggTimeZoneBrowser = function () {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
    } catch (e) {
      return null;
    }
  };

  window.eggTimeZoneWrite = function (tz) {
    document.cookie = name + encodeURIComponent(tz) + "; Path=/; Max-Age=31536000; SameSite=Lax; Secure";
  };

  if (document.cookie.split("; ").some(c => c.startsWith(name))) return;
  const tz = window.eggTimeZoneBrowser();
  if (tz) window.eggTimeZoneWrite(tz);
})();
