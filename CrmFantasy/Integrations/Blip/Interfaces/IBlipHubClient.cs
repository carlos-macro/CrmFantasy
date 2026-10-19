using System.Text.Json.Nodes;

namespace CrmFantasy.Integrations.Blip.Interfaces;

public interface IBlipHubClient
{
    Task<BlipHubResult> SendAsync(JsonObject request, CancellationToken cancellationToken);
}
