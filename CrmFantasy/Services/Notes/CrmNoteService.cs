using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Integrations.Zoho.Interfaces;
using CrmFantasy.Options;
using Microsoft.Extensions.Options;

namespace CrmFantasy.Services.Notes;

/// <summary>
/// Registro de notas em qualquer módulo do CRM, com o usuário de integração como dono.
/// A nota é um registro auxiliar: uma falha ao criá-la gera log e não interrompe o fluxo de quem chama.
/// </summary>
public sealed class CrmNoteService(
    IZohoCrmClient crm,
    IOptions<CrmNoteOptions> options,
    ILogger<CrmNoteService> logger)
{
    /// <summary>
    /// Cria a nota em <paramref name="module"/>/<paramref name="parentId"/>, disparando só as automações de
    /// <paramref name="triggers"/> (padrão: nenhuma). Devolve <c>false</c> se falhar.
    /// </summary>
    public async Task<bool> TryCreateAsync(
        string module,
        long parentId,
        string title,
        string content,
        CancellationToken cancellationToken,
        ZohoTriggers triggers = ZohoTriggers.None)
    {
        try
        {
            await crm.CreateNoteAsync(
                module,
                parentId,
                options.Value.OwnerId,
                title,
                content,
                cancellationToken,
                triggers);

            return true;
        }
        catch (ZohoCrmException ex)
        {
            logger.LogWarning(ex, "Falha ao criar a nota em {Module}/{ParentId}", module, parentId);

            return false;
        }
    }
}
