namespace CrmFantasy.Models.ActiveMessages;

/// <param name="Status">Status gravado na integração: <c>Concluido</c>, <c>Pendente</c> ou <c>Erro</c>.</param>
/// <param name="Attempt">Número desta tentativa.</param>
/// <param name="CampaignId">ID da campanha no Blip, quando o hub devolve.</param>
public sealed record SendActiveMessageResponse(
    string IntegrationId,
    string Status,
    int Attempt,
    string? CampaignId);
