const StateEvent = "components-reconnect-state-changed";
const DialogId = "components-reconnect-modal";
const PollMs = 2000;
const MaxPolls = 150;
const UpdatedReloadMs = 1500;
const FrameworkClasses = [
  "components-reconnect-show",
  "components-reconnect-hide",
  "components-reconnect-retrying",
  "components-reconnect-failed",
  "components-reconnect-rejected",
  "components-reconnect-paused",
  "components-reconnect-resume-failed",
];

let dialog = null;
let state = "hide";
let timer = null;
let polls = 0;
let updating = false;

function open() {
  if (!dialog.open) dialog.showModal();
}

function close() {
  if (dialog.open) dialog.close();
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
    const res = await fetch(dialog.dataset.versionUrl, { cache: "no-store" });
    if (!res.ok) return null;
    const body = await res.json();
    return typeof body.version === "string" ? body.version : "";
  } catch {
    return null;
  }
}

async function poll() {
  timer = null;
  if (updating || state === "hide" || ++polls > MaxPolls) return;
  const version = await fetchVersion();
  if (version !== null) {
    const loaded = dialog.dataset.version;
    if (version && loaded && version !== loaded) {
      showUpdated(version);
      return;
    }
    if (state === "failed" || state === "resume-failed") {
      location.reload();
      return;
    }
  }
  if (state !== "hide") timer = setTimeout(poll, PollMs);
}

function showUpdated(version) {
  updating = true;
  stopPolling();
  dialog.classList.remove(...FrameworkClasses);
  dialog.classList.add("rcn-is-updated");
  dialog.querySelector(".rcn-new-version").textContent = version;
  open();
  setTimeout(() => location.reload(), UpdatedReloadMs);
}

async function retry() {
  try {
    if (await Blazor.reconnect() || await Blazor.resumeCircuit()) return;
  } catch {
  }
  location.reload();
}

async function resume() {
  try {
    if (await Blazor.resumeCircuit()) return;
  } catch {
  }
  location.reload();
}

document.addEventListener(StateEvent, e => {
  if (e.target.id !== DialogId || updating) return;
  dialog = e.target;
  state = e.detail.state;
  if (state === "hide") {
    stopPolling();
    polls = 0;
    close();
    return;
  }
  open();
  if (state === "rejected") {
    stopPolling();
    location.reload();
    return;
  }
  startPolling();
}, true);

document.addEventListener("cancel", e => {
  if (e.target.id === DialogId) e.preventDefault();
}, true);

document.addEventListener("click", e => {
  const button = e.target.closest?.("[data-rcn-action]");
  if (!button?.closest(`#${DialogId}`)) return;
  const action = button.dataset.rcnAction;
  if (action === "retry") void retry();
  else if (action === "resume") void resume();
  else location.reload();
});
