using Microsoft.JSInterop;

namespace EggIdentity.UI.Tests;

internal sealed class FakeJSObjectReference(object? result = null) : IJSObjectReference {
    public readonly List<string> Calls = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) {
        Calls.Add(identifier);
        return ValueTask.FromResult(result is TValue value ? value : default!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
        Calls.Add(identifier);
        return ValueTask.FromResult(result is TValue value ? value : default!);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
