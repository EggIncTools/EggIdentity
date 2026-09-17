using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed record DeployStack {
    public string Name { get; init; } = "";
    public int StackId { get; init; }
    public int EndpointId { get; init; }
    public bool Enabled { get; init; } = true;

    public bool HasPortainerIds => StackId > 0 && EndpointId > 0;
}

public static class DeployStacks {
    public const string Key = "deploy.stacks";

    public static CollectionDescriptor Descriptor { get; } = new(
        Key, "Deploy stacks", "Deploy",
        [
            new FieldDescriptor("name", "Stack name", SettingKind.Text) {
                Required = true,
                Description = "Referenced by the stack field on deploy.apps rows. By convention the Portainer stack and Gitea repo name.",
            },
            new FieldDescriptor("stack_id", "Portainer stack id", SettingKind.Number) {
                Required = true,
                Description = "From the stack's URL in Portainer. Git config, env and webhook state are read from the stack, not copied here.",
            },
            new FieldDescriptor("endpoint_id", "Portainer endpoint id", SettingKind.Number) {
                Required = true,
                Description = "The endpointId in Portainer stack URLs.",
            },
            new FieldDescriptor("enabled", "Enabled", SettingKind.Bool) { Default = "true" },
        ],
        "name", "name") {
        Description = "One row per Portainer stack the agent manages. Git stacks redeploy through their GitOps webhook and need force redeploy on; web-editor stacks redeploy through a stack update.",
    };

    public static ICollectionProvider Provider { get; } = new StaticCollectionProvider([Descriptor]);
}
