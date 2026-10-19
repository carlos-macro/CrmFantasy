using System.ComponentModel.DataAnnotations;

namespace CrmFantasy.Options;

/// <summary>Hub que envia a mensagem ativa ao Blip (hub.macro, rota v2).</summary>
public sealed class BlipHubOptions
{
    public const string SectionName = "BlipHub";

    [Required, Url] public string Url { get; set; } = "https://hub.macro.id/blip/mensagem";

    /// <summary>Header <c>x-api-key</c>.</summary>
    [Required] public string ApiKey { get; set; } = string.Empty;

    /// <summary>Header <c>access-token</c>.</summary>
    [Required] public string AccessToken { get; set; } = string.Empty;
}
