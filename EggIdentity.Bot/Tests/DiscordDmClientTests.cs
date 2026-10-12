using System.Text.Json.Nodes;
using EggIdentity.Contract;

namespace EggIdentity.Bot.Tests;

public class DiscordDmClientTests {
    [Fact]
    public void MapError_50007_IsCannotDm() {
        var result = DiscordDmClient.MapError(403, """{"message":"Cannot send messages to this user","code":50007}""", null);

        Assert.Equal(DmSendStatus.CannotDm, result.Status);
        Assert.Equal(403, result.HttpStatus);
        Assert.Equal(50007, result.DiscordCode);
    }

    [Fact]
    public void MapError_10013_IsUnknownUser() {
        var result = DiscordDmClient.MapError(400, """{"message":"Unknown User","code":10013}""", null);

        Assert.Equal(DmSendStatus.UnknownUser, result.Status);
        Assert.Equal(10013, result.DiscordCode);
    }

    [Fact]
    public void MapError_429_ReadsFractionalRetryAfterFromBody() {
        var result = DiscordDmClient.MapError(429, """{"message":"You are being rate limited.","retry_after":1.5,"global":false}""", TimeSpan.FromSeconds(9));

        Assert.Equal(DmSendStatus.RateLimited, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(1.5), result.RetryAfter);
    }

    [Fact]
    public void MapError_429_FallsBackToHeader() {
        var result = DiscordDmClient.MapError(429, "", TimeSpan.FromSeconds(3));

        Assert.Equal(DmSendStatus.RateLimited, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(3), result.RetryAfter);
    }

    [Fact]
    public void MapError_Plain500_IsFailedWithStatus() {
        var result = DiscordDmClient.MapError(500, "oops", null);

        Assert.Equal(DmSendStatus.Failed, result.Status);
        Assert.Equal(500, result.HttpStatus);
        Assert.Null(result.DiscordCode);
    }

    [Fact]
    public void MapError_403WithOtherCode_IsFailed() {
        var result = DiscordDmClient.MapError(403, """{"message":"Missing Access","code":50001}""", null);

        Assert.Equal(DmSendStatus.Failed, result.Status);
        Assert.Equal(403, result.HttpStatus);
        Assert.Equal(50001, result.DiscordCode);
    }

    [Fact]
    public async Task BuildContent_WithFiles_IsMultipartWithPayloadAndFiles() {
        var message = new DmMessage {
            Content = "hi",
            Files = [new DmFile("report.csv", [1, 2, 3], "text/csv")],
        };

        using var content = DiscordDmClient.BuildContent(message);

        var multipart = Assert.IsType<MultipartFormDataContent>(content);
        var parts = multipart.ToList();
        var payload = Assert.Single(parts, p => Name(p) == "payload_json");
        var file = Assert.Single(parts, p => Name(p) == "files[0]");
        Assert.Equal("report.csv", file.Headers.ContentDisposition?.FileName?.Trim('"'));
        var json = Assert.IsType<JsonObject>(JsonNode.Parse(await payload.ReadAsStringAsync()));
        Assert.Equal("hi", (string?)json["content"]);
        var attachments = Assert.IsType<JsonArray>(json["attachments"]);
        var attachment = Assert.IsType<JsonObject>(Assert.Single(attachments));
        Assert.Equal(0, (int?)attachment["id"]);
        Assert.Equal("report.csv", (string?)attachment["filename"]);
    }

    [Fact]
    public void BuildContent_WithoutFiles_IsJson() {
        using var content = DiscordDmClient.BuildContent(new DmMessage { Content = "hi" });

        Assert.Equal("application/json", content.Headers.ContentType?.MediaType);
    }

    private static string? Name(HttpContent part) => part.Headers.ContentDisposition?.Name?.Trim('"');
}
