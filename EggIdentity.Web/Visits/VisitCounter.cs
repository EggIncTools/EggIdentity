using EggIdentity.UI.Suite;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.Visits;

public sealed class VisitCounter : ComponentBase, IDisposable {
    private const string StateKey = "eggidentity-visitor";

    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private VisitsOptions Options { get; set; } = default!;
    [Inject] private VisitorHasher Hasher { get; set; } = default!;
    [Inject] private VisitTracker Tracker { get; set; } = default!;
    [Inject] private TimeProvider Time { get; set; } = default!;
    [Inject] private PersistentComponentState State { get; set; } = default!;

    [CascadingParameter] private HttpContext? HttpContext { get; set; }

    private string? _visitor;
    private string _path = "/";
    private DateTimeOffset _since;
    private bool _listening;
    private PersistingComponentStateSubscription _persisting;

    private sealed record Handoff(string Visitor, bool Pending);

    protected override void OnInitialized() {
        _path = VisitRequest.PathOf(Nav.Uri);
        _since = Time.GetUtcNow();
        if (State.TryTakeFromJson<Handoff>(StateKey, out var restored) && restored is not null) {
            _visitor = restored.Visitor;
            if (restored.Pending && RendererInfo.IsInteractive) Tracker.Record(_visitor, _path, 0);
        } else if (HttpContext is not null) {
            _visitor = VisitRequest.VisitorOf(HttpContext, Options, Hasher);
            if (_visitor is null) return;
            var speculative = Speculation.IsSpeculative(HttpContext);
            if (!speculative) Tracker.Record(_visitor, _path, 0);
            _persisting = State.RegisterOnPersisting(() => {
                State.PersistAsJson(StateKey, new Handoff(_visitor, speculative));
                return Task.CompletedTask;
            });
        }

        if (_visitor is null || !RendererInfo.IsInteractive) return;
        Nav.LocationChanged += OnLocationChanged;
        _listening = true;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) {
        var path = VisitRequest.PathOf(e.Location);
        if (path == _path || _visitor is null) return;
        var now = Time.GetUtcNow();
        var seconds = (int)Math.Clamp((now - _since).TotalSeconds, 0, Options.SessionGap.TotalSeconds);
        if (seconds > 0) Tracker.Record(_visitor, _path, seconds);
        Tracker.Record(_visitor, path, 0);
        _path = path;
        _since = now;
    }

    public void Dispose() {
        if (_listening) Nav.LocationChanged -= OnLocationChanged;
        _persisting.Dispose();
    }
}
