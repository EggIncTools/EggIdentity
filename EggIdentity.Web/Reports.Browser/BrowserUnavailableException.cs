namespace EggIdentity.Reports.Browser;

public sealed class BrowserUnavailableException(string message, Exception? inner = null, bool prerender = false) : Exception(message, inner) {
    public bool Prerender => prerender;
}
