export function watch(target) {
  const notify = () => target.invokeMethodAsync("OnVisibilityChanged", document.visibilityState !== "hidden");
  document.addEventListener("visibilitychange", notify);
  return {
    visible: () => document.visibilityState !== "hidden",
    stop: () => document.removeEventListener("visibilitychange", notify),
  };
}
