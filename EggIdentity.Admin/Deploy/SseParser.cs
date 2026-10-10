using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EggIdentity.Contract;

namespace EggIdentity.Deploy;

public sealed record SseMessage(long? Id, string? Event, string Data);

public sealed class SseParser {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly StringBuilder _data = new();
    private long? _lastId;
    private string? _event;
    private bool _hasData;

    public SseMessage? Feed(string line) {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length == 0) return Dispatch();
        if (line[0] == ':') return null;

        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var field = colon < 0 ? line : line[..colon];
        var value = colon < 0 ? "" : StripLeadingSpace(line[(colon + 1)..]);
        Apply(field, value);
        return null;
    }

    public static IReadOnlyList<SseMessage> Parse(IEnumerable<string> lines) {
        ArgumentNullException.ThrowIfNull(lines);
        var parser = new SseParser();
        var messages = new List<SseMessage>();
        foreach (var line in lines) {
            if (parser.Feed(line) is { } message) messages.Add(message);
        }
        if (parser.Dispatch() is { } trailing) messages.Add(trailing);
        return messages;
    }

    public static bool TryReadDeployEvent(SseMessage message, [NotNullWhen(true)] out DeployEvent? evt) {
        ArgumentNullException.ThrowIfNull(message);
        evt = null;
        if (message.Event is not (null or "deploy")) return false;
        try {
            var parsed = JsonSerializer.Deserialize<DeployEvent>(message.Data, Json);
            if (parsed is null) return false;
            evt = parsed;
            return true;
        } catch (JsonException) {
            return false;
        }
    }

    private void Apply(string field, string value) {
        switch (field) {
            case "data":
                if (_hasData) _data.Append('\n');
                _data.Append(value);
                _hasData = true;
                break;
            case "event":
                _event = value;
                break;
            case "id":
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) _lastId = id;
                break;
            default:
                break;
        }
    }

    private SseMessage? Dispatch() {
        if (!_hasData) {
            _event = null;
            return null;
        }
        var message = new SseMessage(_lastId, _event, _data.ToString());
        _data.Clear();
        _hasData = false;
        _event = null;
        return message;
    }

    private static string StripLeadingSpace(string value) => value.Length > 0 && value[0] == ' ' ? value[1..] : value;
}
