namespace EggIdentity.Agent.Models.Stack;

public sealed record MaskedEnvEntry(string Name, string Value, bool Masked) {
    public static MaskedEnvEntry From(StackEnvEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        return new MaskedEnvEntry(entry.Name, SecretMasking.Mask(entry.Name, entry.Value), SecretMasking.LooksSecret(entry.Name));
    }
}
