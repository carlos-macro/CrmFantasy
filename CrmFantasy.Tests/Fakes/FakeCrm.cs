using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Integrations.Zoho.Interfaces;

namespace CrmFantasy.Tests.Fakes;

/// <summary>CRM em memória: registros por (módulo, id) e escritas guardadas para conferência.</summary>
internal sealed class FakeCrm : IZohoCrmClient
{
    public Dictionary<(string Module, long Id), CrmRecord> Records { get; } = [];

    public List<IReadOnlyDictionary<string, object?>> Updates { get; } = [];

    public List<ZohoTriggers> UpdateTriggers { get; } = [];

    public List<(string Module, long ParentId, long OwnerId, string Title, string Content)> Notes { get; } = [];

    public List<ZohoTriggers> NoteTriggers { get; } = [];

    public Exception? NoteError { get; set; }

    public Exception? UpdateError { get; set; }

    public Task<CrmRecord?> GetRecordAsync(string module, long id, CancellationToken cancellationToken) =>
        Task.FromResult(Records.GetValueOrDefault((module, id)));

    public Task UpdateRecordAsync(
        string module,
        long id,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken,
        ZohoTriggers triggers = ZohoTriggers.None)
    {
        if (UpdateError is not null)
        {
            throw UpdateError;
        }

        Updates.Add(changes);
        UpdateTriggers.Add(triggers);

        return Task.CompletedTask;
    }

    public Task CreateNoteAsync(
        string parentModule,
        long parentId,
        long ownerId,
        string title,
        string content,
        CancellationToken cancellationToken,
        ZohoTriggers triggers = ZohoTriggers.None)
    {
        if (NoteError is not null)
        {
            throw NoteError;
        }

        Notes.Add((parentModule, parentId, ownerId, title, content));
        NoteTriggers.Add(triggers);

        return Task.CompletedTask;
    }
}
