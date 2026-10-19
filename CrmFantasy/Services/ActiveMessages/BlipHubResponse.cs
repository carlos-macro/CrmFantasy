using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrmFantasy.Services.ActiveMessages;

/// <summary>Leitura da resposta do hub.macro para a mensagem ativa (rota v2).</summary>
public static class BlipHubResponse
{
    /// <summary>Sucesso quando o hub responde <c>success: true</c>.</summary>
    public static bool IsSuccess(JsonNode? hubResponse) => IsTrue(JsonText.At(hubResponse, "success"));

    /// <summary>ID da campanha em <c>data.response.resource.id</c>; ausente quando o envio falha.</summary>
    public static string? CampaignId(JsonNode? hubResponse) =>
        JsonText.Of(JsonText.At(hubResponse, "data", "response", "resource", "id"));

    private static bool IsTrue(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.True;
}
