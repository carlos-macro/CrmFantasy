namespace CrmFantasy.Services.CrmIntegrations;

/// <summary>Valores do campo <c>Status</c> do módulo de integrações.</summary>
public static class CrmIntegrationStatus
{
    public const string Completed = "Concluido";
    public const string Pending = "Pendente";
    public const string Failed = "Erro";

    /// <summary>Concluído no sucesso; na falha, pendente até atingir <paramref name="maxAttempts"/>, depois erro.</summary>
    public static string Decide(bool success, int attempt, int maxAttempts)
    {
        if (success) return Completed;

        return attempt < maxAttempts ? Pending : Failed;
    }
}
