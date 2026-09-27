using Microsoft.AspNetCore.Components;

namespace EggIdentity.Suite.AdminUi;

public sealed record SuiteExtraView(string Key, string Label, RenderFragment Content);
