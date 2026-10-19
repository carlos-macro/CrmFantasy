using System.ComponentModel.DataAnnotations;

namespace CrmFantasy.Options;

public sealed class CrmIntegrationOptions
{
    public const string SectionName = "CrmIntegrations";

    /// <summary>Nome de API do módulo de integrações no CRM.</summary>
    public string Module { get; set; } = "Integracoes";

    /// <summary>A partir desta tentativa, uma falha marca a integração como <c>Erro</c> em vez de <c>Pendente</c>.</summary>
    [Range(1, 100)] public int MaxAttempts { get; set; } = 3;
}
