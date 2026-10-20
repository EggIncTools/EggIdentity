namespace EggIdentity.Contract;

public static class FaultId {
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string New() {
        Span<byte> bytes = stackalloc byte[16];
        Guid.NewGuid().TryWriteBytes(bytes);
        Span<char> chars = stackalloc char[9];
        for (int i = 0, c = 0; c < 9; c++) {
            if (c == 4) {
                chars[c] = '-';
                continue;
            }
            chars[c] = Alphabet[bytes[i++] % Alphabet.Length];
        }
        return new string(chars);
    }
}
