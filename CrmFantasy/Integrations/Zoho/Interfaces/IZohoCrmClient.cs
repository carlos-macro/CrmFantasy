namespace CrmFantasy.Integrations.Zoho.Interfaces;

public interface IZohoCrmClient
{
    /// <summary>Lê um registro. Devolve <c>null</c> quando ele não existe.</summary>
    Task<CrmRecord?> GetRecordAsync(string module, long id, CancellationToken cancellationToken);

    /// <summary>Atualiza campos do registro <paramref name="id"/>, disparando só as automações de <paramref name="triggers"/> (padrão: nenhuma).</summary>
    Task UpdateRecordAsync(
        string module,
        long id,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken,
        ZohoTriggers triggers = ZohoTriggers.None);

    /// <summary>Cria uma nota no registro, disparando só as automações de <paramref name="triggers"/> (padrão: nenhuma).</summary>
    Task CreateNoteAsync(
        string parentModule,
        long parentId,
        long ownerId,
        string title,
        string content,
        CancellationToken cancellationToken,
        ZohoTriggers triggers = ZohoTriggers.None);
}
