(function () {
    const name = "eggidentity_consent=";

    window.eggConsentRead = function () {
        const hit = document.cookie.split("; ").find(c => c.startsWith(name));
        return hit ? decodeURIComponent(hit.slice(name.length)) : null;
    };

    window.eggConsentWrite = function (value, domain, days) {
        let cookie = name + encodeURIComponent(value) + "; Path=/; Max-Age=" + (days * 86400) + "; SameSite=Lax; Secure";
        if (domain) cookie += "; Domain=" + domain;
        document.cookie = cookie;
    };
})();
