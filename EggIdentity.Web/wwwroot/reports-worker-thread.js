import { dotnet } from "./reports-worker/_framework/dotnet.js";

let host = null;

async function boot() {
  const { getAssemblyExports, getConfig } = await dotnet.create();
  const exports = await getAssemblyExports(getConfig().mainAssemblyName);
  host = exports.EggIdentity.Reports.ReportWorkerHost;
  postMessage({ type: "ready" });
}

async function handle(msg) {
  switch (msg.type) {
    case "configure":
      host.Configure(msg.capsJson);
      return null;
    case "describe":
      host.Describe(msg.descriptorJson);
      return null;
    case "load":
      host.Load(new Uint8Array(msg.bytes), msg.schemaJson ?? null);
      return null;
    case "run":
      return await host.Run(msg.queryJson, msg.zoneId, msg.tier);
    default:
      throw new Error(`unknown message type: ${msg.type}`);
  }
}

onmessage = async e => {
  const msg = e.data;
  try {
    const cube = await handle(msg);
    postMessage({ id: msg.id, ok: true, cube });
  } catch (err) {
    postMessage({ id: msg.id, ok: false, error: String(err && err.message || err) });
  }
};

boot().catch(error => {
  postMessage({ type: "error", error: String(error && error.message || error) });
});
