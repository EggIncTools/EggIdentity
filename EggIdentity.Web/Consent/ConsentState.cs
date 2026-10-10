namespace EggIdentity.Consent;

public sealed record ConsentState(bool Functional, bool Analytics, int PolicyVersion, DateTimeOffset DecidedAt);
