namespace EggIdentity.Bot;

public interface IBotConfigAdmin {
    Task<BotConfigView> GetAsync(CancellationToken ct = default);

    Task<SaveResult> SaveAsync(BotConfigInput input, CancellationToken ct = default);
}
