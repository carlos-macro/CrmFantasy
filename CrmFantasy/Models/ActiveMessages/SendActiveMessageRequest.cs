using System.ComponentModel.DataAnnotations;

namespace CrmFantasy.Models.ActiveMessages;

/// <param name="IntegrationId">ID do registro no módulo Integracoes do CRM.</param>
public sealed record SendActiveMessageRequest(
    [Required]
    [RegularExpression(@"^\d{1,19}$", ErrorMessage = "integrationId deve ser o ID numérico do registro.")]
    string IntegrationId);
