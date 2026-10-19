using EggIdentity.Contract;
using Microsoft.AspNetCore.Components;

namespace EggIdentity.UI.Suite;

public sealed class SuiteProfileTemplates {
    public string? CardClass { get; init; }
    public RenderFragment<SuiteProfileAvatarContext>? Avatar { get; init; }
    public RenderFragment<SuiteProfileIdentitiesContext>? Identities { get; init; }
    public RenderFragment<SuiteProfilePreferencesContext>? Preferences { get; init; }
    public RenderFragment<SuiteProfileEidContext>? Eid { get; init; }
}

public sealed record SuiteProfileAvatarContext(
    string? AvatarUrl,
    bool AvatarIsCustom,
    string Username,
    RenderFragment Upload);

public sealed record SuiteProfileIdentityItem(
    ProfileIdentityResponse Identity,
    string ProviderIcon,
    string ProviderName,
    bool IsAvatarSource,
    bool CanUnlink,
    string? UnlinkReason,
    EventCallback OnSelectAvatar,
    EventCallback OnUnlink);

public sealed record SuiteProfileIdentitiesContext(
    IReadOnlyList<SuiteProfileIdentityItem> Items,
    EventCallback OnSync);

public sealed record SuiteProfilePreferencesContext(
    string? Timezone,
    string? Language,
    string Theme,
    IReadOnlyList<string> Themes,
    bool ReducedMotion,
    EventCallback<string?> OnTimezone,
    EventCallback<string?> OnLanguage,
    EventCallback<string> OnTheme,
    EventCallback<bool> OnReducedMotion);

public sealed record SuiteProfileEidContext(
    string? Eid,
    string? Label,
    int MaxEidLength,
    int MaxLabelLength,
    EventCallback<(string? Eid, string? Label)> OnSave);
