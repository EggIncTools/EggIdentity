using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Auth;

public interface ICurrentUser {
    CurrentUser Current { get; }

    Task<CurrentUser> GetAsync();
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser {
    public CurrentUser Current => accessor.HttpContext?.User.ToCurrentUser() ?? CurrentUser.Anonymous;

    public Task<CurrentUser> GetAsync() => Task.FromResult(Current);
}

public sealed class CircuitCurrentUser : ICurrentUser, IDisposable {
    private readonly AuthenticationStateProvider _auth;
    private readonly IHttpContextAccessor? _http;
    private Task<AuthenticationState>? _state;

    public CircuitCurrentUser(AuthenticationStateProvider auth, IHttpContextAccessor? http = null) {
        _auth = auth;
        _http = http;
        auth.AuthenticationStateChanged += OnChanged;
    }

    public CurrentUser Current => Resolve() is { IsCompletedSuccessfully: true } state ? state.Result.User.ToCurrentUser() : FromHttp();

    public async Task<CurrentUser> GetAsync() {
        if (Resolve() is not { } state) return FromHttp();
        try {
            return (await state).User.ToCurrentUser();
        } catch (InvalidOperationException) {
            return FromHttp();
        }
    }

    private Task<AuthenticationState>? Resolve() {
        if (_state is not null) return _state;
        try {
            _state = _auth.GetAuthenticationStateAsync();
        } catch (InvalidOperationException) {
            return null;
        }
        return _state.IsFaulted ? null : _state;
    }

    private CurrentUser FromHttp() => (_http?.HttpContext?.User ?? new ClaimsPrincipal()).ToCurrentUser();

    private void OnChanged(Task<AuthenticationState> state) => _state = state;

    public void Dispose() => _auth.AuthenticationStateChanged -= OnChanged;
}

public static class CurrentUserServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityCurrentUser(this IServiceCollection services) {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser>(sp => sp.GetService<AuthenticationStateProvider>() is { } auth
            ? new CircuitCurrentUser(auth, sp.GetService<IHttpContextAccessor>())
            : new HttpCurrentUser(sp.GetRequiredService<IHttpContextAccessor>()));
        return services;
    }
}
