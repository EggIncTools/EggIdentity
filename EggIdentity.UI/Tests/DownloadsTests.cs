using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.UI.Tests;

public class DownloadsTests {
    private static async Task<HttpContext> Serve(string id, DownloadStore store) {
        var ctx = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        ctx.Response.Body = new MemoryStream();
        await DownloadRoutes.Serve(id, ctx, store).ExecuteAsync(ctx);
        return ctx;
    }

    [Fact]
    public async Task SavedFileIsServedOnceAsAttachment() {
        var store = new DownloadStore(new FakeTimeProvider());
        var downloads = new Downloads(store, new DownloadOptions());

        downloads.SaveText("report.csv", "a,b", "text/csv");
        var id = Assert.Single(downloads.Pending).Split('/')[^1];

        var first = await Serve(id, store);
        Assert.Equal(200, first.Response.StatusCode);
        Assert.Contains("attachment", first.Response.Headers.ContentDisposition.ToString());
        Assert.Contains("report.csv", first.Response.Headers.ContentDisposition.ToString());
        Assert.Equal(404, (await Serve(id, store)).Response.StatusCode);
    }

    [Fact]
    public async Task SavedStreamIsServedWhole() {
        var store = new DownloadStore(new FakeTimeProvider());
        var downloads = new Downloads(store, new DownloadOptions());
        using var source = new MemoryStream([1, 2, 3]);

        await downloads.SaveStreamAsync("x.xlsx", source);
        var served = await Serve(Assert.Single(downloads.Pending).Split('/')[^1], store);

        Assert.Equal([1, 2, 3], ((MemoryStream)served.Response.Body).ToArray());
    }

    [Fact]
    public async Task ExpiredFileIsNotServed() {
        var clock = new FakeTimeProvider();
        var store = new DownloadStore(clock);
        var id = store.Add("x.bin", "application/octet-stream", new byte[] { 1 });

        clock.Advance(DownloadStore.Lifetime);

        Assert.Equal(404, (await Serve(id, store)).Response.StatusCode);
    }

    [Fact]
    public void PendingFramesAreCapped() {
        var downloads = new Downloads(new DownloadStore(TimeProvider.System), new DownloadOptions());
        for (var i = 0; i < 10; i++) downloads.SaveBytes($"f{i}.bin", new byte[] { 1 });
        Assert.Equal(4, downloads.Pending.Count);
    }
}
