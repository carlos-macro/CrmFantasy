namespace CrmFantasy.Integrations.Zoho;

/// <summary>Falha ao ler ou gravar no Zoho CRM. A mensagem é interna e não deve ser devolvida ao consumidor.</summary>
public sealed class ZohoCrmException(string message, Exception? innerException = null)
    : Exception(message, innerException);
