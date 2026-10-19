using System.ComponentModel.DataAnnotations;

namespace CrmFantasy.Options;

public sealed class ZohoOptions
{
    public const string SectionName = "Zoho";

    [Required] public string ClientId { get; set; } = string.Empty;
    [Required] public string ClientSecret { get; set; } = string.Empty;
    [Required] public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Data center da conta: US, EU, IN, AU, JP ou CA.</summary>
    public string DataCenter { get; set; } = "US";

    /// <summary>Production, Sandbox ou Developer.</summary>
    public string Environment { get; set; } = "Production";
}
