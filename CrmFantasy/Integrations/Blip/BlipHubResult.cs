using System.Text.Json.Nodes;

namespace CrmFantasy.Integrations.Blip;

/// <summary>
/// Resultado da chamada ao hub. <see cref="Error"/> só é preenchido quando a chamada não chegou a ter resposta
/// (rede, timeout). Respostas HTTP de erro do hub chegam em <see cref="Body"/>/<see cref="RawBody"/>.
/// </summary>
public sealed record BlipHubResult(JsonNode? Body, string RawBody, string? Error);
