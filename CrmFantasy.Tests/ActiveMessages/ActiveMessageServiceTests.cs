using System.Text.Json.Nodes;
using CrmFantasy.Integrations.Blip;
using CrmFantasy.Integrations.Blip.Interfaces;
using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Options;
using CrmFantasy.Services.ActiveMessages;
using CrmFantasy.Services.CrmIntegrations;
using CrmFantasy.Services.Notes;
using CrmFantasy.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrmFantasy.Tests.ActiveMessages;

public class ActiveMessageServiceTests
{
    private const long IntegrationId = 100;
    private const long DealId = 200;

    private const string V2Success = """
        { "data": { "response": { "resource": { "id": "camp-1" } } }, "success": true, "messages": [] }
        """;

    private readonly FakeCrm _crm = new();
    private readonly FakeHub _hub = new();

    private ActiveMessageService CreateService() => new(
        new CrmIntegrationService(
            _crm,
            Microsoft.Extensions.Options.Options.Create(new CrmIntegrationOptions()),
            NullLogger<CrmIntegrationService>.Instance),
        _hub,
        new CrmNoteService(
            _crm,
            Microsoft.Extensions.Options.Options.Create(new CrmNoteOptions()),
            NullLogger<CrmNoteService>.Instance),
        NullLogger<ActiveMessageService>.Instance);

    private void GivenIntegration(int? attempts = 0, string request = """{ "ChatBotMessageName": "template_x", "CellPhoneNumber": "5511999990000" }""")
    {
        _crm.Records[("Integracoes", IntegrationId)] = new CrmRecord(IntegrationId, new Dictionary<string, object?>
        {
            ["Request"] = request,
            ["Modulo"] = "Deals",
            ["Identificador"] = DealId.ToString(),
            ["qtdTentativa"] = attempts,
            ["Status"] = "Processar",
        });

        _crm.Records[("Deals", DealId)] = new CrmRecord(DealId, new Dictionary<string, object?>
        {
            ["C_digo_do_Pedido"] = "PED-1",
            ["Stage"] = "Aguardando",
            ["Ocorr_ncia"] = "Nova",
            ["Whatsapp_BSUID"] = "bsuid-1",
        });
    }

    [Fact]
    public async Task Sucesso_conclui_integracao_e_registra_nota()
    {
        GivenIntegration();
        _hub.Result = new BlipHubResult(JsonNode.Parse(V2Success), V2Success, null);

        var result = await CreateService().SendAsync(IntegrationId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(("Concluido", 1, "camp-1"), (result.Status, result.Attempt, result.CampaignId));
        Assert.Equal("bsuid-1", _hub.Request!["BSUID"]!.GetValue<string>());

        var update = Assert.Single(_crm.Updates);
        Assert.Equal(1, update["qtdTentativa"]);
        Assert.Equal("Concluido", update["Status"]);
        Assert.Equal("camp-1", update["ID_Campanha"]);
        Assert.Equal(V2Success, update["Response"]);
        Assert.Equal(ZohoTriggers.None, _crm.UpdateTriggers.Single());

        var note = Assert.Single(_crm.Notes);
        Assert.Equal(ZohoTriggers.None, _crm.NoteTriggers.Single());
        Assert.Equal(("Deals", DealId, ActiveMessageService.NoteTitle), (note.Module, note.ParentId, note.Title));
        Assert.Equal(
            "Resultado envio mensagem ativa: \nID Externo: PED-1\nEstágio: Aguardando\nOcorrência: Nova\nTemplate: template_x",
            note.Content);
    }

    [Fact]
    public async Task Falha_de_rede_conta_tentativa_e_registra_nota()
    {
        GivenIntegration(attempts: null);
        _hub.Result = new BlipHubResult(null, string.Empty, "Falha ao chamar o hub: conexão recusada");

        var result = await CreateService().SendAsync(IntegrationId, CancellationToken.None);

        Assert.Equal(("Pendente", 1), (result!.Status, result.Attempt));
        var update = _crm.Updates.Single();
        Assert.Equal("Falha ao chamar o hub: conexão recusada", update["Response"]);
        Assert.DoesNotContain("conexão recusada", _crm.Notes.Single().Content);
    }

    [Fact]
    public async Task Terceira_falha_marca_erro()
    {
        GivenIntegration(attempts: 2);
        const string body = """{ "success": false, "messages": ["template inválido"] }""";
        _hub.Result = new BlipHubResult(JsonNode.Parse(body), body, null);

        var result = await CreateService().SendAsync(IntegrationId, CancellationToken.None);

        Assert.Equal(("Erro", 3), (result!.Status, result.Attempt));
        Assert.DoesNotContain("ID_Campanha", _crm.Updates.Single().Keys);
    }

    [Fact]
    public async Task Integracao_inexistente_devolve_null_sem_chamar_hub()
    {
        var result = await CreateService().SendAsync(IntegrationId, CancellationToken.None);

        Assert.Null(result);
        Assert.Null(_hub.Request);
        Assert.Empty(_crm.Notes);
    }

    [Fact]
    public async Task Registro_de_origem_inexistente_devolve_null_sem_enviar_nem_alterar_integracao()
    {
        GivenIntegration();
        _crm.Records.Remove(("Deals", DealId));

        var result = await CreateService().SendAsync(IntegrationId, CancellationToken.None);

        Assert.Null(result);
        Assert.Null(_hub.Request);
        Assert.Empty(_crm.Updates);
        Assert.Empty(_crm.Notes);
    }

    [Fact]
    public async Task Request_invalido_registra_nota_e_devolve_null()
    {
        GivenIntegration(request: "não é json");

        var result = await CreateService().SendAsync(IntegrationId, CancellationToken.None);

        Assert.Null(result);
        Assert.Null(_hub.Request);
        Assert.Empty(_crm.Updates);
        Assert.Equal(ActiveMessageService.MissingIntegrationNote, _crm.Notes.Single().Content);
    }

    [Fact]
    public async Task Falha_ao_criar_nota_nao_interrompe_o_fluxo()
    {
        GivenIntegration();
        _crm.NoteError = new ZohoCrmException("falhou");
        _hub.Result = new BlipHubResult(null, string.Empty, "Tempo esgotado ao chamar o hub.");

        var result = await CreateService().SendAsync(IntegrationId, CancellationToken.None);

        Assert.Equal("Pendente", result!.Status);
        Assert.Single(_crm.Updates);
    }

    [Fact]
    public async Task Falha_ao_atualizar_integracao_propaga_erro_do_crm()
    {
        GivenIntegration();
        _crm.UpdateError = new ZohoCrmException("falhou");
        _hub.Result = new BlipHubResult(JsonNode.Parse(V2Success), V2Success, null);

        await Assert.ThrowsAsync<ZohoCrmException>(() => CreateService().SendAsync(IntegrationId, CancellationToken.None));
    }

    private sealed class FakeHub : IBlipHubClient
    {
        public BlipHubResult Result { get; set; } = new(null, string.Empty, "não configurado");
        public JsonObject? Request { get; private set; }

        public Task<BlipHubResult> SendAsync(JsonObject request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Result);
        }
    }
}
