using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace EggIdentity.Reports;

[SupportedOSPlatform("browser")]
public static partial class ReportWorkerHost {
    private static readonly ReportWorkerHostCore Core = new();

    [JSExport]
    public static void Configure(string capsJson) => Core.Configure(capsJson);

    [JSExport]
    public static void Describe(string descriptorJson) => Core.Describe(descriptorJson);

    [JSExport]
    public static void Load(byte[] snapshot, string? schemaJson) => Core.Load(snapshot, schemaJson);

    [JSExport]
    public static string? Version(string source) => Core.Version(source);

    [JSExport]
    public static void Unload(string source) => Core.Unload(source);

    [JSExport]
    public static Task<string> Run(string queryJson, string zoneId, string tier) => Core.Run(queryJson, zoneId, tier);
}
