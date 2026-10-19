using System.Text.Json;
using System.Text.Json.Nodes;
using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Integrations.Zoho.Interfaces;
using CrmFantasy.Options;
using Microsoft.Extensions.Options;

namespace CrmFantasy.Services.CrmIntegrations;

/// <summary>
/// Ciclo de vida de um registro do módulo de integrações, comum às funções que processam integrações:
/// carregar a integração (com ou sem o registro de origem), gravar o resultado de cada tentativa
/// (<c>qtdTentativa</c>, <c>Response</c>, <c>Status</c>).
/// </summary>
public sealed class CrmIntegrationService(
    IZohoCrmClient crm,
    IOptions<CrmIntegrationOptions> options,
    ILogger<CrmIntegrationService> logger)
{
    /// <summary>
    /// Carrega a integração. Com <paramref name="includeSource"/>, busca também o registro de origem
    /// (<c>Modulo</c>/<c>Identificador</c>) e devolve <c>null</c> se ele não existir; sem ele, só a integração é lida
    /// e <see cref="CrmIntegration.Source"/> fica <c>null</c>.
    /// Devolve <c>null</c> quando a integração não existe. Nada é alterado no CRM.
    /// </summary>
    public async Task<CrmIntegration?> LoadAsync(
        long integrationId,
        bool includeSource,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // Integração
        var integration = await crm.GetRecordAsync(settings.Module, integrationId, cancellationToken);

        if (integration is null)
        {
            logger.LogWarning("Integração {IntegrationId} não encontrada", integrationId);
            return null;
        }

        var sourceModule = integration.GetString("Modulo");
        var sourceId = integration.GetLong("Identificador");
        var request = ParseRequest(integration.GetString("Request"));

        if (!includeSource)
        {
            return new CrmIntegration(integration, sourceModule, sourceId, null, request);
        }

        // Registro de origem
        var source = sourceModule is not null && sourceId is > 0
            ? await crm.GetRecordAsync(sourceModule, sourceId.Value, cancellationToken)
            : null;

        if (source is null)
        {
            logger.LogWarning(
                "Registro de origem {Module}/{SourceId} da integração {IntegrationId} não encontrado",
                sourceModule,
                sourceId,
                integrationId);

            return null;
        }

        return new CrmIntegration(integration, sourceModule, sourceId, source, request);
    }

    /// <summary>
    /// Grava uma tentativa: incrementa <c>qtdTentativa</c>, guarda <paramref name="response"/> e decide o
    /// <c>Status</c>. <paramref name="extraFields"/> entram na mesma atualização (ex.: <c>ID_Campanha</c>).
    /// Só as automações de <paramref name="triggers"/> são disparadas (padrão: nenhuma). Falha ao gravar lança <see cref="ZohoCrmException"/>.
    /// </summary>
    public async Task<CrmIntegrationAttempt> RecordAttemptAsync(
        CrmIntegration integration,
        bool success,
        string? response,
        IReadOnlyDictionary<string, object?>? extraFields,
        CancellationToken cancellationToken,
        ZohoTriggers triggers = ZohoTriggers.None)
    {
        var settings = options.Value;

        // Resultado
        var attempt = (integration.Record.GetInt("qtdTentativa") ?? 0) + 1;
        var status = CrmIntegrationStatus.Decide(success, attempt, settings.MaxAttempts);

        // Atualização
        var changes = new Dictionary<string, object?>(extraFields ?? new Dictionary<string, object?>())
        {
            ["qtdTentativa"] = attempt,
            ["Response"] = response,
            ["Status"] = status,
        };

        try
        {
            await crm.UpdateRecordAsync(settings.Module, integration.Id, changes, cancellationToken, triggers);
        }
        catch (ZohoCrmException ex)
        {
            // A ação externa já aconteceu (ou falhou); registra o resultado para não perder o rastro.
            logger.LogError(
                ex,
                "Tentativa {Attempt} da integração {IntegrationId} (sucesso: {Success}) não foi gravada no CRM",
                attempt,
                integration.Id,
                success);

            throw;
        }

        logger.LogInformation(
            "Integração {IntegrationId}: tentativa {Attempt}, status {Status}",
            integration.Id,
            attempt,
            status);

        return new CrmIntegrationAttempt(attempt, status);
    }

    private static JsonObject? ParseRequest(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
