using System.Security.Cryptography;
using System.Text;
using CrmFantasy.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace CrmFantasy.Http;

/// <summary>Exige o header <c>x-api-key</c> igual a <see cref="ApiAccessOptions.ApiKey"/>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "x-api-key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var expected = services.GetRequiredService<IOptions<ApiAccessOptions>>().Value.ApiKey;
        var received = context.HttpContext.Request.Headers[HeaderName].ToString();

        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(received),
            Encoding.UTF8.GetBytes(expected));

        if (valid) return;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Chave de API inválida",
        };

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status401Unauthorized,
        };
    }
}
