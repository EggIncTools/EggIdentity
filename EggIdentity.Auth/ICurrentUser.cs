using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Auth;

public interface ICurrentUser {
    Task<CurrentUser> GetAsync();
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser {
    public Task<CurrentUser> GetAsync() => Task.FromResult(accessor.HttpContext?.User.ToCurrentUser() ?? CurrentUser.Anonymous);
}

public sealed class CircuitCurrentUser : ICurrentUser, IDisposable {
    private readonly AuthenticationStateProvider _auth;
    private Task<AuthenticationState> _state;

    public CircuitCurrentUser(AuthenticationStateProvider auth) {
        _auth = auth;
        _state = auth.GetAuthenticationStateAsync();
        auth.AuthenticationStateChanged += OnChanged;
    }

    public async Task<CurrentUser> GetAsync() => (await _state).User.ToCurrentUser();

    private void OnChanged(Task<AuthenticationState> state) => _state = state;

    public void Dispose() => _auth.AuthenticationStateChanged -= OnChanged;
}

public static class CurrentUserServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityCurrentUser(this IServiceCollection services) {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser>(sp => sp.GetService<AuthenticationStateProvider>() is { } auth
            ? new CircuitCurrentUser(auth)
            : new HttpCurrentUser(sp.GetRequiredService<IHttpContextAccessor>()));
        return services;
    }
}
