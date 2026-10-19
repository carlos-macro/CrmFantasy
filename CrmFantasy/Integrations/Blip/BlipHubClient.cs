using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrmFantasy.Integrations.Blip.Interfaces;
using CrmFantasy.Options;
using Microsoft.Extensions.Options;

namespace CrmFantasy.Integrations.Blip;

/// <summary>Envia a mensagem ativa pelo hub.macro (<c>POST /blip/mensagem</c>, corpo <c>OldMensagemAtivaBlipDto</c>).</summary>
public sealed class BlipHubClient(
    HttpClient httpClient,
    IOptions<BlipHubOptions> options,
    ILogger<BlipHubClient> logger) : IBlipHubClient
{
    public async Task<BlipHubResult> SendAsync(JsonObject request, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        using var message = new HttpRequestMessage(HttpMethod.Post, settings.Url)
        {
            Content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        message.Headers.TryAddWithoutValidation("x-api-key", settings.ApiKey);
        message.Headers.TryAddWithoutValidation("access-token", settings.AccessToken);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("Hub Blip respondeu HTTP {StatusCode}", (int)response.StatusCode);

            return new BlipHubResult(TryParse(raw), raw, null);
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            logger.LogWarning(ex, "Falha ao chamar o hub Blip");

            var error = ex is TaskCanceledException
                ? "Tempo esgotado ao chamar o hub."
                : $"Falha ao chamar o hub: {ex.Message}";

            return new BlipHubResult(null, string.Empty, error);
        }
    }

    private static bool IsTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException
        || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested;

    private static JsonNode? TryParse(string raw)
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
}
