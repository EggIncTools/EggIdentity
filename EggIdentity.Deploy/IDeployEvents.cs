using System.Threading.Channels;
using EggIdentity.Contract;

namespace EggIdentity.Deploy;

public interface IDeployEvents {
    event Action<DeployEvent>? Received;
    long LastEventId { get; }
    ChannelReader<DeployEvent> Subscribe();
    void Unsubscribe(ChannelReader<DeployEvent> reader);
    IReadOnlyList<DeployEvent> Recent(string? app = null);
}
