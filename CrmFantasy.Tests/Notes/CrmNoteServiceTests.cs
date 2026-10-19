using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Options;
using CrmFantasy.Services.Notes;
using CrmFantasy.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrmFantasy.Tests.Notes;

public class CrmNoteServiceTests
{
    private readonly FakeCrm _crm = new();

    private CrmNoteService CreateService(long ownerId = 123) => new(
        _crm,
        Microsoft.Extensions.Options.Options.Create(new CrmNoteOptions { OwnerId = ownerId }),
        NullLogger<CrmNoteService>.Instance);

    [Fact]
    public async Task Cria_nota_com_o_dono_configurado()
    {
        var created = await CreateService(ownerId: 999).TryCreateAsync("Deals", 10, "Título", "Conteúdo", CancellationToken.None, ZohoTriggers.Workflow);

        Assert.True(created);
        Assert.Equal(("Deals", 10L, 999L, "Título", "Conteúdo"), _crm.Notes.Single());
        Assert.Equal(ZohoTriggers.Workflow, _crm.NoteTriggers.Single());
    }

    [Fact]
    public async Task Falha_do_crm_devolve_false_sem_lancar()
    {
        _crm.NoteError = new ZohoCrmException("falhou");

        var created = await CreateService().TryCreateAsync("Leads", 20, "Título", "Conteúdo", CancellationToken.None);

        Assert.False(created);
    }
}
