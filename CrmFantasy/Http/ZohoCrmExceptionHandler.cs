using CrmFantasy.Integrations.Zoho;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CrmFantasy.Http;

/// <summary>Traduz falhas do Zoho CRM em 502, sem devolver a mensagem interna do CRM.</summary>
public sealed class ZohoCrmExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ZohoCrmExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ZohoCrmException) return false;

        logger.LogError(exception, "Falha na comunicação com o Zoho CRM");

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Falha ao comunicar com o CRM",
            },
        });
    }
}
