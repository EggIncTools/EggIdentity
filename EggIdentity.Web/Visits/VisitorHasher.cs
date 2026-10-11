using System.Security.Cryptography;
using System.Text;

namespace EggIdentity.Visits;

public sealed class VisitorHasher(TimeProvider time) {
    private const int SaltBytes = 32;
    private readonly Lock _gate = new();
    private byte[] _salt = RandomNumberGenerator.GetBytes(SaltBytes);
    private DateOnly _saltDay = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    public string Hash(string site, string ip, string userAgent) {
        var payload = Encoding.UTF8.GetBytes($"{site}\n{ip}\n{userAgent}");
        var buffer = new byte[SaltBytes + payload.Length];
        CurrentSalt().CopyTo(buffer, 0);
        payload.CopyTo(buffer, SaltBytes);
        var hash = SHA256.HashData(buffer);
        CryptographicOperations.ZeroMemory(buffer);
        return Convert.ToHexStringLower(hash);
    }

    internal (DateOnly Day, byte[] Salt) Current() {
        var salt = CurrentSalt();
        lock (_gate) return (_saltDay, [.. salt]);
    }

    internal void Seed(DateOnly day, byte[] salt) {
        ArgumentNullException.ThrowIfNull(salt);
        if (salt.Length != SaltBytes) throw new ArgumentException($"salt must be {SaltBytes} bytes", nameof(salt));
        lock (_gate) {
            if (day != _saltDay) return;
            CryptographicOperations.ZeroMemory(_salt);
            _salt = [.. salt];
        }
    }

    private byte[] CurrentSalt() {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        lock (_gate) {
            if (today != _saltDay) {
                CryptographicOperations.ZeroMemory(_salt);
                _salt = RandomNumberGenerator.GetBytes(SaltBytes);
                _saltDay = today;
            }
            return _salt;
        }
    }
}
