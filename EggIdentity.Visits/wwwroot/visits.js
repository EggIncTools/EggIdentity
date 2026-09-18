(function () {
    if (!navigator.sendBeacon) return;
    if (navigator.doNotTrack === "1" || navigator.globalPrivacyControl === true) return;
    var consent = document.cookie.match(/(?:^|; )eggidentity_consent=([^;]*)/);
    if (consent && /(?:^|&)a=0(?:&|$)/.test(decodeURIComponent(consent[1]))) return;

    var endpoint = (document.currentScript && document.currentScript.dataset.endpoint) || "/_visits";
    var path = location.pathname;
    var last = Date.now();

    function beat(seconds) {
        navigator.sendBeacon(endpoint, JSON.stringify({ path: path, seconds: seconds }));
    }

    function view() {
        path = location.pathname;
        last = Date.now();
        beat(0);
    }

    function leave() {
        var seconds = Math.floor((Date.now() - last) / 1000);
        last = Date.now();
        if (seconds > 0) beat(seconds);
    }

    function navigate() {
        if (location.pathname === path) return;
        leave();
        view();
    }

    var pushState = history.pushState;
    history.pushState = function () {
        pushState.apply(this, arguments);
        navigate();
    };
    addEventListener("popstate", navigate);
    addEventListener("pagehide", leave);
    view();
})();
