using Microsoft.JSInterop;

namespace EggIdentity.UI.Tests;

internal sealed class FakeJSRuntime(object? result = null) : IJSRuntime {
    public readonly List<(string Identifier, object?[]? Args)> Calls = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) {
        Calls.Add((identifier, args));
        return ValueTask.FromResult(result is TValue value ? value : default!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
        Calls.Add((identifier, args));
        return ValueTask.FromResult(result is TValue value ? value : default!);
    }
}
