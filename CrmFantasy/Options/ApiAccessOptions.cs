using System.ComponentModel.DataAnnotations;

namespace CrmFantasy.Options;

/// <summary>Chave que os consumidores enviam no header <c>x-api-key</c> para chamar o CrmFantasy.</summary>
public sealed class ApiAccessOptions
{
    public const string SectionName = "ApiAccess";

    [Required, MinLength(16)] public string ApiKey { get; set; } = string.Empty;
}
