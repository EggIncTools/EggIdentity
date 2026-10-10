using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggIdentity.UI;

public static class UploadRoutes {
    public static IServiceCollection AddEggIdentityUploads(this IServiceCollection services) {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<UploadStore>();
        return services;
    }

    public static async Task<IResult> CompleteAsync(HttpContext ctx, UploadStore store, int status, string body = "") {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(store);
        var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
        store.Complete(form[UploadStore.TicketField], new UploadResult(status, body));
        return Results.Text(body, statusCode: status);
    }
}
