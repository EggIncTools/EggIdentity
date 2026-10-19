const DbName = "eggidentity-reports";
const StoreName = "snapshots";
const RevalidateMs = 15000;
const MaxBytes = 512 * 1024 * 1024;

let worker = null;
let workerReady = null;
let nextId = 1;
const pending = new Map();
let capsConfigured = false;
const described = new Map();
const loaded = new Map();
const chains = new Map();
let dbPromise = null;

function openDb() {
  if (dbPromise) return dbPromise;
  dbPromise = new Promise((resolve, reject) => {
    const req = indexedDB.open(DbName, 1);
    req.onupgradeneeded = () => {
      if (!req.result.objectStoreNames.contains(StoreName)) req.result.createObjectStore(StoreName);
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
  return dbPromise;
}

function tx(mode, work) {
  return openDb().then(db => new Promise((resolve, reject) => {
    const t = db.transaction(StoreName, mode);
    const result = work(t.objectStore(StoreName));
    t.oncomplete = () => resolve(result && "result" in result ? result.result : undefined);
    t.onerror = () => reject(t.error);
  }));
}

const idbGet = key => tx("readonly", store => store.get(key));
const idbPut = (key, value) => tx("readwrite", store => store.put(value, key));
const idbDelete = key => tx("readwrite", store => store.delete(key));

async function idbEntries() {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const entries = [];
    const req = db.transaction(StoreName, "readonly").objectStore(StoreName).openCursor();
    req.onsuccess = () => {
      const cursor = req.result;
      if (!cursor) {
        resolve(entries);
        return;
      }
      entries.push({ key: cursor.key, size: cursor.value.bytes.byteLength, touched: cursor.value.touched || 0 });
      cursor.continue();
    };
    req.onerror = () => reject(req.error);
  });
}

async function trimDb() {
  const entries = await idbEntries();
  let total = entries.reduce((sum, e) => sum + e.size, 0);
  entries.sort((a, b) => a.touched - b.touched);
  for (const e of entries) {
    if (total <= MaxBytes) break;
    await idbDelete(e.key);
    total -= e.size;
  }
}

function startWorker() {
  if (workerReady) return workerReady;
  workerReady = new Promise((resolve, reject) => {
    let settled = false;
    try {
      worker = new Worker(new URL("./reports-worker-thread.js", import.meta.url), { type: "module" });
    } catch (e) {
      settled = true;
      reject(unavailable(e));
      return;
    }
    worker.onmessage = e => {
      const msg = e.data;
      if (!settled && msg.type === "ready") {
        settled = true;
        resolve();
        return;
      }
      if (!settled && msg.type === "error") {
        settled = true;
        reject(new Error(`unavailable: ${msg.error}`));
        return;
      }
      if (msg.id !== undefined) dispatchReply(msg);
    };
    worker.onerror = e => {
      if (!settled) {
        settled = true;
        reject(unavailable(e));
      }
    };
  });
  return workerReady;
}

function dispatchReply(msg) {
  const resolver = pending.get(msg.id);
  if (!resolver) return;
  pending.delete(msg.id);
  if (msg.ok) resolver.resolve(msg);
  else resolver.reject(new Error(msg.error));
}

function post(type, payload, transfer) {
  const id = nextId++;
  const message = { id, type, ...payload };
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    if (transfer) worker.postMessage(message, transfer);
    else worker.postMessage(message);
  });
}

async function fetchJson(url) {
  const res = await fetch(url);
  if (res.status === 204) return null;
  if (!res.ok) throw new Error(`${url} -> ${res.status}`);
  return await res.text();
}

function unavailable(e) {
  if (e instanceof Error && e.message.startsWith("unavailable: ")) return e;
  const reason = e?.message ?? (typeof e === "string" ? e : "unknown error");
  return new Error(`unavailable: ${reason}`);
}

function dbKey(scope, partition, source) {
  return `${scope}|${partition ?? ""}|${source}`;
}

async function ensure(prefix, source, scope, partition) {
  try {
    await startWorker();
    if (!capsConfigured) {
      await post("configure", { capsJson: await fetchJson(`${prefix}/caps`) });
      capsConfigured = true;
    }
    if (!described.has(source)) {
      const descriptorJson = await fetchJson(`${prefix}/descriptor/${source}`);
      await post("describe", { descriptorJson });
      described.set(source, await fetchJson(`${prefix}/schema/${source}`));
    }
    const schemaJson = described.get(source);
    const key = dbKey(scope, partition, source);
    const current = loaded.get(source);
    if (current && current.key === key && Date.now() - current.checked < RevalidateMs) return;
    const stored = await idbGet(key);
    const headers = {};
    if (stored?.etag) headers["If-None-Match"] = stored.etag;
    const url = partition ? `${prefix}/snapshot/${source}?p=${encodeURIComponent(partition)}` : `${prefix}/snapshot/${source}`;
    const res = await fetch(url, { headers });
    let record = stored;
    if (res.status === 304 && stored) {
      if (current && current.key === key && current.etag === stored.etag) {
        current.checked = Date.now();
        return;
      }
    } else if (res.ok) {
      record = { etag: res.headers.get("ETag") || "", bytes: await res.arrayBuffer(), touched: Date.now() };
      await idbPut(key, record);
      await trimDb();
    } else {
      throw new Error(`snapshot/${source} -> ${res.status}`);
    }
    const bytesCopy = record.bytes.slice(0);
    await post("load", { bytes: bytesCopy, schemaJson }, [bytesCopy]);
    loaded.set(source, { key, etag: record.etag, checked: Date.now() });
  } catch (e) {
    throw unavailable(e);
  }
}

function serialized(source, work) {
  const previous = chains.get(source) || Promise.resolve();
  const next = previous.catch(() => {}).then(work);
  chains.set(source, next);
  return next;
}

function run(prefix, source, scope, partition, queryJson, zoneId, tier) {
  return serialized(source, async () => {
    await ensure(prefix, source, scope, partition);
    const msg = await post("run", { queryJson, zoneId, tier });
    return msg.cube;
  });
}

async function invalidate(scope, source, partition) {
  for (const [loadedSource, entry] of loaded) {
    if (source && loadedSource !== source) continue;
    if (partition !== undefined && partition !== null && entry.key !== dbKey(scope, partition, loadedSource)) continue;
    loaded.delete(loadedSource);
  }
}

async function clear(scope) {
  loaded.clear();
  const entries = await idbEntries();
  for (const e of entries) {
    if (String(e.key).startsWith(`${scope}|`)) await idbDelete(e.key);
  }
}

export { run, invalidate, clear };
