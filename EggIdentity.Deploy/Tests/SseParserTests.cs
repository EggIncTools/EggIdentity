namespace EggIdentity.Deploy.Tests;

public class SseParserTests {
    [Fact]
    public void Parse_ReadsIdEventAndData() {
        var messages = SseParser.Parse(["id: 7", "event: deploy", """data: {"x":1}""", ""]);

        var message = Assert.Single(messages);
        Assert.Equal(7, message.Id);
        Assert.Equal("deploy", message.Event);
        Assert.Equal("""{"x":1}""", message.Data);
    }

    [Fact]
    public void Parse_IgnoresCommentKeepalives() {
        var messages = SseParser.Parse([": keepalive", "", ": keepalive", "", "data: a", ""]);

        var message = Assert.Single(messages);
        Assert.Equal("a", message.Data);
    }

    [Fact]
    public void Parse_JoinsMultipleDataLines() {
        var messages = SseParser.Parse(["data: one", "data: two", ""]);

        Assert.Equal("one\ntwo", Assert.Single(messages).Data);
    }

    [Fact]
    public void Parse_IdIsStickyAcrossMessages() {
        var messages = SseParser.Parse(["id: 3", "data: a", "", "data: b", ""]);

        Assert.Equal([3L, 3L], messages.Select(m => m.Id));
    }

    [Fact]
    public void Parse_BlankLineWithoutData_DispatchesNothing() {
        var messages = SseParser.Parse(["event: deploy", "", "", "data: x", ""]);

        var message = Assert.Single(messages);
        Assert.Null(message.Event);
    }

    [Fact]
    public void Parse_FlushesTrailingMessageWithoutBlankLine() {
        var messages = SseParser.Parse(["data: tail"]);

        Assert.Equal("tail", Assert.Single(messages).Data);
    }

    [Fact]
    public void Parse_FieldWithoutColon_TreatedAsEmptyValue() {
        var messages = SseParser.Parse(["data", ""]);

        Assert.Equal("", Assert.Single(messages).Data);
    }

    [Fact]
    public void TryReadDeployEvent_RejectsOtherEventNamesAndBadJson() {
        Assert.False(SseParser.TryReadDeployEvent(new SseMessage(1, "other", "{}"), out _));
        Assert.False(SseParser.TryReadDeployEvent(new SseMessage(1, "deploy", "not json"), out _));
    }
}
