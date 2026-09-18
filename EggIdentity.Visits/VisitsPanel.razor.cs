using System.Globalization;
using System.Security.Claims;
using System.Text;
using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Visits;

public sealed partial class VisitsPanel {
    internal static readonly int[] Ranges = [7, 30, 90];
    internal const int SparkWidth = 240;
    internal const int SparkHeight = 40;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    [CascadingParameter] public Task<AuthenticationState>? AuthState { get; set; }

    [Parameter] public Func<ClaimsPrincipal, bool>? Authorize { get; set; }

    [Parameter] public int InitialDays { get; set; } = 30;

    private bool _authorized;
    private VisitsStore? _store;
    private VisitsOptions? _options;
    private VisitsSummary? _summary;
    private string? _error;
    private int _days;

    protected override async Task OnInitializedAsync() {
        _days = Ranges.Contains(InitialDays) ? InitialDays : Ranges[1];
        if (AuthState is null) return;
        var principal = (await AuthState).User;
        _authorized = Authorize?.Invoke(principal) ?? principal.IsAtLeast(UserRole.Admin);
        if (!_authorized) return;

        _store = Services.GetService<VisitsStore>();
        _options = Services.GetService<VisitsOptions>();
        if (_options is null) _store = null;
        await LoadAsync();
    }

    private async Task SelectRangeAsync(int days) {
        _days = days;
        await LoadAsync();
    }

    private async Task LoadAsync() {
        if (_store is null || _options is null) return;
        _summary = null;
        _error = null;
        try {
            _summary = await _store.QueryAsync(_options.Site, _days);
        } catch (Exception e) when (e is not OperationCanceledException) {
            _error = "Visits query failed: " + e.Message;
        }
    }

    private string RangeClass(int range) => range == _days ? "skv-range skv-range-on" : "skv-range";

    private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private string AverageVisit() {
        if (_summary is null || _summary.Visits == 0) return "-";
        var seconds = _summary.DurationSeconds / _summary.Visits;
        return seconds >= 60
            ? $"{seconds / 60}m {seconds % 60:00}s"
            : $"{seconds}s";
    }

    private IEnumerable<(string X, string Y, VisitsDay Day)> SparkPoints() {
        if (_summary is null || _summary.ByDay.Count == 0) yield break;
        var days = _summary.ByDay;
        var max = Math.Max(1, days.Max(d => d.Visitors));
        var step = days.Count > 1 ? (double)SparkWidth / (days.Count - 1) : 0;
        for (var i = 0; i < days.Count; i++) {
            var x = days.Count > 1 ? i * step : SparkWidth / 2.0;
            var y = SparkHeight - days[i].Visitors / (double)max * (SparkHeight - 6) - 3;
            yield return (F(x), F(y), days[i]);
        }
    }

    private string Spark() {
        var sb = new StringBuilder();
        foreach (var (x, y, _) in SparkPoints()) {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(x).Append(',').Append(y);
        }
        return sb.ToString();
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
