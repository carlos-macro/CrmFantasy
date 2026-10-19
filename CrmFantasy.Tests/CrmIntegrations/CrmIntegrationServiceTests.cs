using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Options;
using CrmFantasy.Services.CrmIntegrations;
using CrmFantasy.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrmFantasy.Tests.CrmIntegrations;

public class CrmIntegrationServiceTests
{
    private const long IntegrationId = 100;
    private const long DealId = 200;

    private readonly FakeCrm _crm = new();

    private CrmIntegrationService CreateService() => new(
        _crm,
        Microsoft.Extensions.Options.Options.Create(new CrmIntegrationOptions()),
        NullLogger<CrmIntegrationService>.Instance);

    private void GivenIntegration(int? attempts = 0, string? request = """{ "a": 1 }""")
    {
        _crm.Records[("Integracoes", IntegrationId)] = new CrmRecord(
            IntegrationId,
            new Dictionary<string, object?>
            {
                ["Request"] = request,
                ["Modulo"] = "Deals",
                ["Identificador"] = DealId.ToString(),
                ["qtdTentativa"] = attempts,
            });

        _crm.Records[("Deals", DealId)] = new CrmRecord(DealId, new Dictionary<string, object?>());
    }

    private async Task<CrmIntegration> LoadAsync(CrmIntegrationService service) =>
        (await service.LoadAsync(IntegrationId, includeSource: true, CancellationToken.None))!;

    [Theory]
    [InlineData(true, 1, "Concluido")]
    [InlineData(true, 3, "Concluido")]
    [InlineData(false, 1, "Pendente")]
    [InlineData(false, 2, "Pendente")]
    [InlineData(false, 3, "Erro")]
    [InlineData(false, 4, "Erro")]
    public void Decide_status_pela_tentativa(bool success, int attempt, string expected) =>
        Assert.Equal(expected, CrmIntegrationStatus.Decide(success, attempt, maxAttempts: 3));

    [Fact]
    public async Task Carrega_integracao_origem_e_request()
    {
        GivenIntegration();

        var integration = await CreateService().LoadAsync(IntegrationId, includeSource: true, CancellationToken.None);

        Assert.NotNull(integration);
        Assert.Equal(("Deals", DealId, DealId), (integration.SourceModule, integration.SourceId, integration.Source!.Id));
        Assert.Equal(1, integration.Request!["a"]!.GetValue<int>());
    }

    [Fact]
    public async Task Sem_origem_carrega_so_a_integracao()
    {
        GivenIntegration();
        _crm.Records.Remove(("Deals", DealId));

        var integration = await CreateService().LoadAsync(IntegrationId, includeSource: false, CancellationToken.None);

        Assert.NotNull(integration);
        Assert.Null(integration.Source);
        Assert.Equal(("Deals", DealId), (integration.SourceModule, integration.SourceId));
        Assert.NotNull(integration.Request);
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("[1, 2]")]
    [InlineData(null)]
    public async Task Request_invalido_carrega_com_request_null(string? request)
    {
        GivenIntegration(request: request);

        var integration = await LoadAsync(CreateService());

        Assert.Null(integration.Request);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Integracao_inexistente_devolve_null(bool includeSource)
    {
        var integration = await CreateService().LoadAsync(IntegrationId, includeSource, CancellationToken.None);

        Assert.Null(integration);
    }

    [Fact]
    public async Task Origem_inexistente_devolve_null_quando_pedida()
    {
        GivenIntegration();
        _crm.Records.Remove(("Deals", DealId));

        var integration = await CreateService().LoadAsync(IntegrationId, includeSource: true, CancellationToken.None);

        Assert.Null(integration);
    }

    [Fact]
    public async Task Falha_grava_tentativa_e_campos_extras()
    {
        GivenIntegration(attempts: null);
        var service = CreateService();
        var integration = await LoadAsync(service);

        var attempt = await service.RecordAttemptAsync(
            integration,
            success: false,
            "falhou",
            new Dictionary<string, object?> { ["Extra"] = "x" },
            CancellationToken.None,
            ZohoTriggers.Workflow);

        Assert.Equal(new CrmIntegrationAttempt(1, "Pendente"), attempt);

        var update = _crm.Updates.Single();
        Assert.Equal((1, "falhou", "Pendente", "x"), (update["qtdTentativa"], update["Response"], update["Status"], update["Extra"]));
        Assert.Equal(ZohoTriggers.Workflow, _crm.UpdateTriggers.Single());
    }

    [Fact]
    public async Task Sucesso_conclui()
    {
        GivenIntegration(attempts: 1);
        var service = CreateService();
        var integration = await LoadAsync(service);

        var attempt = await service.RecordAttemptAsync(integration, success: true, "ok", null, CancellationToken.None);

        Assert.Equal(new CrmIntegrationAttempt(2, "Concluido"), attempt);
    }

    [Fact]
    public async Task Falha_ao_gravar_propaga_erro()
    {
        GivenIntegration();
        _crm.UpdateError = new ZohoCrmException("falhou");
        var service = CreateService();
        var integration = await LoadAsync(service);

        await Assert.ThrowsAsync<ZohoCrmException>(
            () => service.RecordAttemptAsync(integration, success: false, "x", null, CancellationToken.None));
    }
}
