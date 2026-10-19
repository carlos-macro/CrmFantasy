using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrmFantasy.Integrations.Zoho;

/// <summary>Utilitários comuns às chamadas HTTP ao Zoho.</summary>
internal static class ZohoJson
{
    public static JsonNode? TryParse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            return JsonNode.Parse(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Falha de rede ou timeout; cancelamento pedido pelo chamador não conta.</summary>
    public static bool IsTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException
        || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested;
}
