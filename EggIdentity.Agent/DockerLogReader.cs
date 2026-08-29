using System.Diagnostics;

namespace EggIdentity.Agent;

public static class DockerLogReader {
    public static async Task<string> TailAsync(string container, int lines, CancellationToken ct) {
        try {
            var psi = new ProcessStartInfo("docker", $"logs --tail {lines} {container}") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return stdout.Length > 0 ? stdout : stderr;
        } catch (Exception e) {
            return $"docker logs failed: {e.Message}";
        }
    }
}
