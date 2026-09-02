using System.Diagnostics;

namespace EggIdentity.Agent;

public static class DockerContainer {
    public static async Task<string?> RestartAsync(string container, CancellationToken ct) {
        try {
            var psi = new ProcessStartInfo("docker", $"restart {container}") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(psi)!;
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0 ? null : $"docker restart failed: {stderr.Trim()}";
        } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) {
            return $"docker restart failed: {e.Message}";
        }
    }
}
