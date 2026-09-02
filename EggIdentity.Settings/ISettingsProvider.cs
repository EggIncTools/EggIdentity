using System.Reflection;

namespace EggIdentity.Settings;

public interface ISettingsProvider {
    IReadOnlyList<SettingDescriptor> Describe();
}

public sealed class StaticSettingsProvider(IReadOnlyList<SettingDescriptor> descriptors) : ISettingsProvider {
    public IReadOnlyList<SettingDescriptor> Describe() => descriptors;
}

public sealed class AttributeSettingsProvider<T> : ISettingsProvider {
    private readonly List<SettingDescriptor> _descriptors = [.. typeof(T)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(p => (Prop: p, Attr: p.GetCustomAttribute<SettingAttribute>()))
        .Where(x => x.Attr is not null)
        .Select(x => Build(x.Attr!))];

    public IReadOnlyList<SettingDescriptor> Describe() => _descriptors;

    private static SettingDescriptor Build(SettingAttribute a) =>
        new(a.Key, a.EnvKey, a.Label, a.Category, a.Kind, a.Tier, a.Sensitivity) {
            Description = a.Description,
            Required = a.Required,
            Default = a.Default,
            AllowBootstrapEdit = a.AllowBootstrapEdit,
        };
}
