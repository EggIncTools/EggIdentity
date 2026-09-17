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
                Description = "Key that deploy.apps rows reference in their stack field. By convention the Portainer stack name and the Gitea repo name.",
            },
            new FieldDescriptor("stack_id", "Portainer stack id", SettingKind.Number) {
                Required = true,
                Description = "Numeric id from the stack's URL in Portainer. The agent reads git config, env and webhook state from the stack itself, so nothing else is copied here.",
            },
            new FieldDescriptor("endpoint_id", "Portainer endpoint id", SettingKind.Number) {
                Required = true,
                Description = "Numeric environment id, the endpointId in Portainer stack URLs.",
            },
            new FieldDescriptor("enabled", "Enabled", SettingKind.Bool) { Default = "true" },
        ],
        "name", "name") {
        Description = "Portainer stacks the agent manages. A redeploy covers the whole stack, so apps that share a stack share one row. Git stacks deploy through their GitOps webhook and need force redeploy on; web-editor stacks deploy through a stack update. The Fleet pane shows which is missing.",
    };

    public static ICollectionProvider Provider { get; } = new StaticCollectionProvider([Descriptor]);
}
