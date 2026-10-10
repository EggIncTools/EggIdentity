using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace EggIdentity.UI.Tests;

public class UploadStoreTests {
    [Fact]
    public async Task CompleteAsyncReadsTicketFromFormAndEchoesStatus() {
        var store = new UploadStore(new FakeTimeProvider());
        var ticket = store.Open();
        var ctx = new DefaultHttpContext();
        ctx.Request.Form = new FormCollection(new Dictionary<string, StringValues> { [UploadStore.TicketField] = ticket });

        var result = await UploadRoutes.CompleteAsync(ctx, store, 201, "stored");

        Assert.Equal(201, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal(new UploadResult(201, "stored"), await store.WaitAsync(ticket, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task CompletedTicketIsReturnedOnce() {
        var store = new UploadStore(new FakeTimeProvider());
        var ticket = store.Open();

        Assert.True(store.Complete(ticket, new UploadResult(201, "ok")));
        var result = await store.WaitAsync(ticket, TimeSpan.FromSeconds(1));

        Assert.Equal(new UploadResult(201, "ok"), result);
        Assert.Null(await store.WaitAsync(ticket, TimeSpan.FromSeconds(1)));
        Assert.False(store.Complete(ticket, new UploadResult(200, "")));
    }

    [Fact]
    public async Task WaitTimesOutToNull() {
        var clock = new FakeTimeProvider();
        var store = new UploadStore(clock);
        var ticket = store.Open();

        var wait = store.WaitAsync(ticket, TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Null(await wait);
        Assert.False(store.Complete(ticket, new UploadResult(200, "late")));
    }

    [Fact]
    public void UnknownOrNullTicketIsRejected() {
        var store = new UploadStore(new FakeTimeProvider());
        Assert.False(store.Complete(null, new UploadResult(200, "")));
        Assert.False(store.Complete("nope", new UploadResult(200, "")));
    }
}
