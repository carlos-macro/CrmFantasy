using System.Text.Json.Nodes;
using CrmFantasy.Options;
using Microsoft.Extensions.Options;

namespace CrmFantasy.Integrations.Zoho;

/// <summary>
/// Access token do Zoho a partir do refresh token. Fica só em memória e é renovado um pouco antes de expirar
/// (ou quando o CRM o recusa). Singleton: o token é compartilhado por todas as requisições.
/// </summary>
public sealed class ZohoTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ZohoOptions> options,
    ILogger<ZohoTokenProvider> logger)
{
    public const string HttpClientName = "ZohoAccounts";

    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private volatile CachedToken? _token;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is { IsValid: true } cached) return cached.Value;

        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            // Outra requisição pode ter renovado enquanto esta esperava.
            if (_token is { IsValid: true } renewed) return renewed.Value;

            _token = await RefreshAsync(cancellationToken);

            return _token.Value;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>Descarta <paramref name="rejectedToken"/>, se ainda for o atual, para a próxima chamada renovar.</summary>
    public void Invalidate(string rejectedToken)
    {
        var current = _token;

        if (current?.Value == rejectedToken)
        {
            _token = null;
        }
    }

    private async Task<CachedToken> RefreshAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var endpoints = ZohoEndpoints.From(settings);

        // Credenciais no corpo, não na URL, para não aparecerem em logs de requisição.
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoints.Accounts, "oauth/v2/token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["refresh_token"] = settings.RefreshToken,
            }),
        };

        JsonNode? body;

        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);

            body = ZohoJson.TryParse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex) when (ZohoJson.IsTransportFailure(ex, cancellationToken))
        {
            throw new ZohoCrmException($"Falha ao renovar o token do Zoho: {ex.Message}", ex);
        }

        // O Zoho responde HTTP 200 com "error" quando recusa o refresh token.
        var accessToken = (string?)body?["access_token"];
        var expiresIn = (int?)body?["expires_in"] ?? 0;

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new ZohoCrmException($"Zoho recusou a renovação do token: {(string?)body?["error"] ?? "resposta inesperada"}.");
        }

        logger.LogInformation("Token do Zoho renovado, válido por {ExpiresIn}s", expiresIn);

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn) - ExpiryMargin;

        return new CachedToken(accessToken, expiresAt);
    }

    private sealed record CachedToken(string Value, DateTimeOffset ExpiresAt)
    {
        public bool IsValid => DateTimeOffset.UtcNow < ExpiresAt;
    }
}
