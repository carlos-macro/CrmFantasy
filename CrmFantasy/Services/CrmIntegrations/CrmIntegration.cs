using System.Text.Json.Nodes;
using CrmFantasy.Integrations.Zoho;

namespace CrmFantasy.Services.CrmIntegrations;

/// <summary>Registro do módulo de integrações.</summary>
/// <param name="SourceModule">Campo <c>Modulo</c>: módulo do registro de origem.</param>
/// <param name="SourceId">Campo <c>Identificador</c>: ID do registro de origem.</param>
/// <param name="Source">Registro de origem; <c>null</c> quando carregado sem ele.</param>
/// <param name="Request">Campo <c>Request</c> como objeto JSON; <c>null</c> quando vazio ou inválido.</param>
public sealed record CrmIntegration(
    CrmRecord Record,
    string? SourceModule,
    long? SourceId,
    CrmRecord? Source,
    JsonObject? Request)
{
    public long Id => Record.Id;
}
