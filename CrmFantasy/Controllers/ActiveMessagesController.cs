using CrmFantasy.Http;
using CrmFantasy.Models.ActiveMessages;
using CrmFantasy.Services.ActiveMessages;
using Microsoft.AspNetCore.Mvc;

namespace CrmFantasy.Controllers;

[ApiController]
[Route("api/active-messages")]
[RequireApiKey]
public sealed class ActiveMessagesController(ActiveMessageService service) : ControllerBase
{
    /// <summary>
    /// Envia a mensagem ativa do Blip descrita no registro de Integracoes e registra o resultado no CRM.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<SendActiveMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SendActiveMessageResponse>> Send(
        SendActiveMessageRequest request,
        CancellationToken cancellationToken)
    {
        var integrationId = long.Parse(request.IntegrationId);

        var result = await service.SendAsync(integrationId, cancellationToken);

        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Integração não encontrada",
                detail: "O registro de Integracoes ou o registro de origem não existe, ou a integração não tem um Request válido.");
        }

        return result;
    }
}
