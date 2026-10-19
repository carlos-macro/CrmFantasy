using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrmFantasy.Integrations.Zoho.Interfaces;

namespace CrmFantasy.Integrations.Zoho;

/// <summary>
/// Acesso ao Zoho CRM pela API REST v8. O <see cref="HttpClient"/> já vem com a base <c>/crm/v8/</c>
/// do data center e ambiente configurados (ver <see cref="ZohoEndpoints"/>).
/// </summary>
public sealed class ZohoCrmClient(HttpClient httpClient, ZohoTokenProvider tokens) : IZohoCrmClient
{
    public async Task<CrmRecord?> GetRecordAsync(string module, long id, CancellationToken cancellationToken)
    {
        var action = $"ler {module}/{id}";

        var (status, body) = await SendAsync(HttpMethod.Get, $"{module}/{id}", null, action, cancellationToken);

        if (status == HttpStatusCode.NoContent) return null;

        if (status == HttpStatusCode.OK)
        {
            return body?["data"] is JsonArray { Count: > 0 } data && data[0] is JsonObject record
                ? ToCrmRecord(record)
                : null;
        }

        var (code, message) = ReadError(body);

        // ID inexistente ou inválido: tratado como registro não encontrado.
        if (code == "INVALID_DATA") return null;

        throw Failure(action, status, code, message);
    }

    public async Task UpdateRecordAsync(
        string module,
        long id,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken,
        ZohoTriggers triggers = ZohoTriggers.None)
    {
        var action = $"atualizar {module}/{id}";

        var record = new JsonObject();

        foreach (var (field, value) in changes)
        {
            record[field] = JsonSerializer.SerializeToNode(value);
        }

        var payload = new JsonObject
        {
            ["data"] = new JsonArray(record),
            ["trigger"] = ToTriggerArray(triggers),
        };

        var (status, body) = await SendAsync(HttpMethod.Put, $"{module}/{id}", payload, action, cancellationToken);

        EnsureSuccess(action, status, body);
    }

    public async Task CreateNoteAsync(
        string parentModule,
        long parentId,
        long ownerId,
        string title,
        string content,
        CancellationToken cancellationToken,
        ZohoTriggers triggers = ZohoTriggers.None)
    {
        var action = $"criar nota em {parentModule}/{parentId}";

        var note = new JsonObject
        {
            ["Note_Title"] = title,
            ["Note_Content"] = content,
            ["Owner"] = new JsonObject { ["id"] = ToIdString(ownerId) },
            ["Parent_Id"] = new JsonObject
            {
                ["module"] = new JsonObject { ["api_name"] = parentModule },
                ["id"] = ToIdString(parentId),
            },
        };

        var payload = new JsonObject
        {
            ["data"] = new JsonArray(note),
            ["trigger"] = ToTriggerArray(triggers),
        };

        var (status, body) = await SendAsync(HttpMethod.Post, "Notes", payload, action, cancellationToken);

        EnsureSuccess(action, status, body);
    }

    /// <summary>
    /// Envia a requisição com o access token. Se o Zoho recusar o token (401), renova e tenta mais uma vez.
    /// </summary>
    private async Task<(HttpStatusCode Status, JsonNode? Body)> SendAsync(
        HttpMethod method,
        string path,
        JsonObject? payload,
        string action,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var token = await tokens.GetAccessTokenAsync(cancellationToken);

            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", token);

            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload);
            }

            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
                {
                    tokens.Invalidate(token);
                    continue;
                }

                var raw = await response.Content.ReadAsStringAsync(cancellationToken);

                return (response.StatusCode, ZohoJson.TryParse(raw));
            }
            catch (Exception ex) when (ZohoJson.IsTransportFailure(ex, cancellationToken))
            {
                throw new ZohoCrmException($"Falha ao chamar o Zoho ao {action}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>Confere o resultado de uma escrita: o primeiro item de <c>data</c> precisa ter status <c>success</c>.</summary>
    private static void EnsureSuccess(string action, HttpStatusCode status, JsonNode? body)
    {
        var item = body?["data"] is JsonArray { Count: > 0 } data ? data[0] : null;

        if ((string?)item?["status"] == "success") return;

        var (code, message) = ReadError(item ?? body);

        throw Failure(action, status, code, message);
    }

    private static (string? Code, string? Message) ReadError(JsonNode? node) =>
        node is JsonObject error
            ? (error["code"]?.ToString(), error["message"]?.ToString())
            : (null, null);

    private static ZohoCrmException Failure(string action, HttpStatusCode status, string? code, string? message) =>
        code is null
            ? new ZohoCrmException($"Resposta inesperada ao {action}: HTTP {(int)status}.")
            : new ZohoCrmException($"Zoho recusou {action}: {code} {message}");

    /// <summary>Lista para a chave <c>trigger</c>. Sempre enviada: sem ela, o Zoho dispara todas as automações.</summary>
    private static JsonArray ToTriggerArray(ZohoTriggers triggers)
    {
        var names = Enum.GetValues<ZohoTriggers>()
            .Where(flag => flag is not ZohoTriggers.None and not ZohoTriggers.All && triggers.HasFlag(flag))
            .Select(flag => (JsonNode?)flag.ToString().ToLowerInvariant());

        return new JsonArray(names.ToArray());
    }

    private static string ToIdString(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static CrmRecord ToCrmRecord(JsonObject record)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (field, value) in record)
        {
            fields[field] = Normalize(value);
        }

        return new CrmRecord(ParseId(record["id"]) ?? 0, fields);
    }

    /// <summary>Lookups e usuários (<c>{ "id": ... }</c>) viram o ID; os demais valores ficam como vieram.</summary>
    private static object? Normalize(JsonNode? node) => node switch
    {
        null => null,
        JsonObject lookup when lookup.ContainsKey("id") => ParseId(lookup["id"]),
        JsonObject other => other,
        JsonArray array => array.Select(Normalize).ToList(),
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Number when value.TryGetValue<long>(out var integer) => integer,
            JsonValueKind.Number => value.GetValue<decimal>(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        },
        _ => null,
    };

    private static long? ParseId(JsonNode? node)
    {
        var parsed = long.TryParse(node?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id);

        return parsed ? id : null;
    }
}
