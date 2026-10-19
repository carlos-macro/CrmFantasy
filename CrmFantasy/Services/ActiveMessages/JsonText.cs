using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrmFantasy.Services.ActiveMessages;

internal static class JsonText
{
    /// <summary>
    /// Texto de um valor JSON: strings sem aspas, números e booleanos como no JSON; objetos e listas, <c>null</c>.
    /// </summary>
    public static string? Of(JsonNode? node)
    {
        if (node is not JsonValue value) return null;

        return value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToJsonString(),
            _ => null,
        };
    }

    /// <summary>
    /// Segue <paramref name="path"/> por objetos aninhados; devolve <c>null</c> se algum nível faltar.
    /// </summary>
    public static JsonNode? At(JsonNode? node, params string[] path)
    {
        foreach (var key in path)
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue(key, out node))
            {
                return null;
            }
        }

        return node;
    }
}
