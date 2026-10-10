export async function send(input, url, timeoutMs) {
  const file = input.files && input.files[0];
  if (!file) return null;
  const body = new FormData();
  body.append("file", file);
  const abort = new AbortController();
  const timer = timeoutMs > 0 ? setTimeout(() => abort.abort(), timeoutMs) : 0;
  try {
    const response = await fetch(url, { method: "POST", body, credentials: "same-origin", signal: abort.signal });
    return { status: response.status, body: await response.text() };
  } catch (e) {
    return { status: 0, body: abort.signal.aborted ? "timeout" : String(e && e.message || e) };
  } finally {
    clearTimeout(timer);
    input.value = "";
  }
}
