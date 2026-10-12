const StateEvent = "components-reconnect-state-changed";
const BannerId = "components-reconnect-modal";
const PollMs = 2000;
const MaxPolls = 150;
const UpdatedReloadMs = 1500;
const BackOnlineMs = 2000;
const ReconnectionOptions = {
  maxRetries: 60,
  retryIntervalMilliseconds: attempt => Math.min(500 * 2 ** Math.min(attempt, 3), 3000),
};
const FrameworkClasses = [
  "components-reconnect-show",
  "components-reconnect-hide",
  "components-reconnect-retrying",
  "components-reconnect-failed",
  "components-reconnect-rejected",
  "components-reconnect-paused",
  "components-reconnect-resume-failed",
];

let banner = null;
let state = "hide";
let timer = null;
let backTimer = null;
let polls = 0;
let updating = false;
let observer = null;
const inerted = new Set();

function exempt(el) {
  return el === banner || el.tagName === "SCRIPT" || el.contains?.(banner);
}

function inertChild(el) {
  if (!(el instanceof HTMLElement) || exempt(el) || el.inert) return;
  el.inert = true;
  inerted.add(el);
}

function lockPage() {
  for (const el of document.body.children) inertChild(el);
  if (observer) return;
  observer = new MutationObserver(records => {
    for (const r of records) for (const el of r.addedNodes) inertChild(el);
  });
  observer.observe(document.body, { childList: true });
}

function unlockPage() {
  observer?.disconnect();
  observer = null;
  for (const el of inerted) el.inert = false;
  inerted.clear();
}

function show() {
  clearTimeout(backTimer);
  backTimer = null;
  banner.classList.remove("rcn-is-back");
  banner.classList.add("rcn-visible");
  requestAnimationFrame(() => banner.classList.add("rcn-shown"));
  lockPage();
}

function hide() {
  unlockPage();
  banner.classList.remove("rcn-shown", "rcn-visible", "rcn-is-back");
}

function backOnline() {
  unlockPage();
  banner.classList.add("rcn-is-back", "rcn-visible", "rcn-shown");
  clearTimeout(backTimer);
  backTimer = setTimeout(hide, BackOnlineMs);
}

function startPolling() {
  if (timer !== null || polls > MaxPolls) return;
  timer = setTimeout(poll, PollMs);
}

function stopPolling() {
  clearTimeout(timer);
  timer = null;
}

async function fetchVersion() {
  try {
    const res = await fetch(banner.dataset.versionUrl, { cache: "no-store" });
    if (!res.ok) return null;
    const body = await res.json();
    return {
      version: typeof body.version === "string" ? body.version : "",
      display: typeof body.display === "string" ? body.display : "",
    };
  } catch {
    return null;
  }
}

async function poll() {
  timer = null;
  if (updating || state === "hide" || ++polls > MaxPolls) return;
  const served = await fetchVersion();
  if (served !== null) {
    const loaded = banner.dataset.version;
    if (served.version && loaded && served.version !== loaded) {
      showUpdated(served.display);
      return;
    }
    if (state === "failed" || state === "resume-failed") {
      location.reload();
      return;
    }
  }
  if (state !== "hide") timer = setTimeout(poll, PollMs);
}

function showUpdated(display) {
  updating = true;
  stopPolling();
  banner.classList.remove(...FrameworkClasses);
  banner.classList.add("rcn-is-updated");
  banner.querySelector(".rcn-new-version").textContent = display || "a new build";
  show();
  setTimeout(() => location.reload(), UpdatedReloadMs);
}

async function resume() {
  try {
    if (await Blazor.resumeCircuit()) return;
  } catch {
  }
  location.reload();
}

document.addEventListener(StateEvent, e => {
  if (e.target.id !== BannerId || updating) return;
  banner = e.target;
  const previous = state;
  state = e.detail.state;
  if (state === "hide") {
    stopPolling();
    polls = 0;
    if (previous === "hide") hide();
    else backOnline();
    return;
  }
  show();
  if (state === "rejected") {
    stopPolling();
    location.reload();
    return;
  }
  startPolling();
}, true);

document.addEventListener("click", e => {
  const button = e.target.closest?.("[data-rcn-action]");
  if (!button?.closest(`#${BannerId}`)) return;
  if (button.dataset.rcnAction === "resume") void resume();
  else location.reload();
});

function boot() {
  if (document.getElementById(BannerId)?.dataset.boot !== "true") return;
  const start = () => Blazor.start({ circuit: { reconnectionOptions: ReconnectionOptions } });
  if (document.prerendering) document.addEventListener("prerenderingchange", start, { once: true });
  else start();
}

boot();
