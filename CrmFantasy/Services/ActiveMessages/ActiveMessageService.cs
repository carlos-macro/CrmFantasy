using System.Text.Json.Nodes;
using CrmFantasy.Integrations.Blip.Interfaces;
using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Models.ActiveMessages;
using CrmFantasy.Services.CrmIntegrations;
using CrmFantasy.Services.Notes;

namespace CrmFantasy.Services.ActiveMessages;

/// <summary>
/// Envio da mensagem ativa do Blip a partir de um registro de Integracoes, sempre pelo hub.macro (rota v2;
/// a v1 do AutomatusHub foi descontinuada).
/// Substitui a função Deluge <c>automation.EnviarMensagemAtivaBlip</c>
/// (zoho-functions: "CRM/Mensagens Ativas/Blip - Mensagem Ativa - Integração.deluge").
/// </summary>
public sealed class ActiveMessageService(
    CrmIntegrationService integrations,
    IBlipHubClient hub,
    CrmNoteService notes,
    ILogger<ActiveMessageService> logger)
{
    public const string NoteTitle = "Integração Externa - BLIP - Mensagem Ativa";

    public const string MissingIntegrationNote = "\nNão foi possível recuperar dados da Integração.";

    /// <summary>
    /// Processa a integração. Devolve <c>null</c>, sem enviar nada, quando a integração ou o registro de origem
    /// não existem, ou quando a integração não tem um <c>Request</c> válido.
    /// Falhas do hub ficam registradas na integração; falhas do CRM lançam <see cref="ZohoCrmException"/>.
    /// </summary>
    public async Task<SendActiveMessageResponse?> SendAsync(long integrationId, CancellationToken cancellationToken)
    {
        // Integração e pedido. Sem eles não há BSUID nem onde registrar a nota: não envia nem altera nada.
        var integration = await integrations.LoadAsync(integrationId, includeSource: true, cancellationToken);

        if (integration is not { Source: { } source, SourceModule: { } sourceModule }) return null;

        // Corpo da mensagem
        var request = integration.Request;

        if (request is null)
        {
            logger.LogWarning("Integração {IntegrationId} sem Request válido", integrationId);

            await notes.TryCreateAsync(
                sourceModule,
                source.Id,
                NoteTitle,
                MissingIntegrationNote,
                cancellationToken);

            return null;
        }

        var bsuid = source.GetString("Whatsapp_BSUID");

        if (!string.IsNullOrEmpty(bsuid))
        {
            request["BSUID"] = bsuid;
        }

        // Envio
        var result = await hub.SendAsync(request, cancellationToken);

        // Resultado na integração
        var success = result.Error is null && BlipHubResponse.IsSuccess(result.Body);
        var campaignId = BlipHubResponse.CampaignId(result.Body);
        var hubOutput = result.Error ?? result.RawBody;

        var extraFields = campaignId is null
            ? null  
            : new Dictionary<string, object?> { ["ID_Campanha"] = campaignId };

        var attempt = await integrations.RecordAttemptAsync(
            integration,
            success,
            hubOutput,
            extraFields,
            cancellationToken);

        // Nota no registro de origem
        var noteContent = "Resultado envio mensagem ativa: \n" + Summary(source, request);

        await notes.TryCreateAsync(
            sourceModule,
            source.Id,
            NoteTitle,
            noteContent,
            cancellationToken);

        return new SendActiveMessageResponse(
            integrationId.ToString(),
            attempt.Status,
            attempt.Attempt,
            campaignId);
    }

    private static string Summary(CrmRecord source, JsonObject request)
    {
        var template = request
            .FirstOrDefault(p => p.Key.Equals("chatbotmessagename", StringComparison.OrdinalIgnoreCase))
            .Value;

        return "ID Externo: " + source.GetString("C_digo_do_Pedido")
            + "\nEstágio: " + source.GetString("Stage")
            + "\nOcorrência: " + source.GetString("Ocorr_ncia")
            + "\nTemplate: " + JsonText.Of(template);
    }
}
