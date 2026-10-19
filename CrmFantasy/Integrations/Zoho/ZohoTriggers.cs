namespace CrmFantasy.Integrations.Zoho;

/// <summary>
/// Automações do CRM que uma escrita pode disparar (chave <c>trigger</c> da API). Combine com <c>|</c>.
/// <see cref="None"/> não dispara nenhuma, como o <c>zoho.crm.updateRecord</c> do Deluge sem trigger.
/// </summary>
[Flags]
public enum ZohoTriggers
{
    None = 0,
    Workflow = 1,
    Approval = 2,
    Blueprint = 4,
    Pathfinder = 8,
    Orchestration = 16,
    All = Workflow | Approval | Blueprint | Pathfinder | Orchestration,
}
