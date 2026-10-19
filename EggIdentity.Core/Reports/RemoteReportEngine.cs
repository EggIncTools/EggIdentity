using System.Text.Json;

namespace EggIdentity.Reports;

public sealed class RemoteReportEngine(Func<string, string, string, CancellationToken, Task<string>> run) : IReportEngine {
    public async Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);

        var queryJson = ReportWire.WriteQuery(query);
        string resultJson;
        try {
            resultJson = await run(queryJson, context.Zone.Id, context.Tier.ToString(), ct).ConfigureAwait(false);
        } catch (Exception e) {
            if (TryReadRejections(e.Message, out var rejections)) throw new ReportRejectedException(rejections);
            throw;
        }

        return ReportWire.ReadCube(resultJson);
    }

    private static bool TryReadRejections(string message, out IReadOnlyList<ReportRejection> rejections) {
        try {
            var parsed = JsonSerializer.Deserialize(message, ReportJsonContext.Default.ReportRejectionArray);
            if (parsed is null) {
                rejections = [];
                return false;
            }
            rejections = parsed;
            return true;
        } catch (JsonException) {
            rejections = [];
            return false;
        }
    }
}
