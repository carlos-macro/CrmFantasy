using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CrmFantasy.Integrations.Zoho;
using CrmFantasy.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrmFantasy.Tests.Zoho;

public class ZohoCrmClientTests
{
    private const string TokenJson = """{ "access_token": "tok-1", "expires_in": 3600 }""";

    private readonly StubHandler _accounts = new();
    private readonly StubHandler _crm = new();

    private ZohoCrmClient CreateClient()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ZohoOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            RefreshToken = "refresh",
        });

        var tokens = new ZohoTokenProvider(
            new StubFactory(_accounts),
            options,
            NullLogger<ZohoTokenProvider>.Instance);

        var http = new HttpClient(_crm) { BaseAddress = ZohoEndpoints.From(options.Value).CrmApi };

        return new ZohoCrmClient(http, tokens);
    }

    [Fact]
    public async Task Le_registro_normalizando_lookup_usuario_e_numeros()
    {
        _accounts.Enqueue(HttpStatusCode.OK, TokenJson);
        _crm.Enqueue(HttpStatusCode.OK, """
            { "data": [ {
                "id": "4918426000000001",
                "Status": "Processar",
                "Owner": { "name": "Fulano", "id": "4918426000001382001", "email": "f@x.com" },
                "Deal": { "name": "Pedido", "id": "4918426000000002" },
                "qtdTentativa": 2,
                "Valor": 10.5,
                "Vazio": null
            } ] }
            """);

        var record = await CreateClient().GetRecordAsync("Integracoes", 4918426000000001, CancellationToken.None);

        Assert.NotNull(record);
        Assert.Equal(4918426000000001, record.Id);
        Assert.Equal("Processar", record.GetString("Status"));
        Assert.Equal(4918426000001382001, record.Fields["Owner"]);
        Assert.Equal(4918426000000002, record.GetLong("Deal"));
        Assert.Equal(2, record.GetInt("qtdTentativa"));
        Assert.Equal(10.5m, record.Fields["Valor"]);
        Assert.Null(record.Fields["Vazio"]);

        var request = Assert.Single(_crm.Requests);
        Assert.Equal("https://www.zohoapis.com/crm/v8/Integracoes/4918426000000001", request.Uri);
        Assert.Equal("Zoho-oauthtoken tok-1", request.Authorization);

        var tokenRequest = Assert.Single(_accounts.Requests);
        Assert.Equal("https://accounts.zoho.com/oauth/v2/token", tokenRequest.Uri);
        Assert.Contains("grant_type=refresh_token", tokenRequest.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, "")]
    [InlineData(HttpStatusCode.BadRequest, """{ "code": "INVALID_DATA", "message": "the related id given seems to be invalid", "status": "error" }""")]
    public async Task Registro_inexistente_devolve_null(HttpStatusCode status, string body)
    {
        _accounts.Enqueue(HttpStatusCode.OK, TokenJson);
        _crm.Enqueue(status, body);

        var record = await CreateClient().GetRecordAsync("Deals", 1, CancellationToken.None);

        Assert.Null(record);
    }

    [Fact]
    public async Task Atualiza_sem_disparar_workflows()
    {
        _accounts.Enqueue(HttpStatusCode.OK, TokenJson);
        _crm.Enqueue(HttpStatusCode.OK, """{ "data": [ { "code": "SUCCESS", "status": "success" } ] }""");

        var changes = new Dictionary<string, object?> { ["qtdTentativa"] = 1, ["Status"] = "Concluido", ["Response"] = null };

        await CreateClient().UpdateRecordAsync("Integracoes", 7, changes, CancellationToken.None);

        var request = Assert.Single(_crm.Requests);
        var body = JsonNode.Parse(request.Body)!;

        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith("/crm/v8/Integracoes/7", request.Uri);
        Assert.Empty(body["trigger"]!.AsArray());
        Assert.Equal(1, (int)body["data"]![0]!["qtdTentativa"]!);
        Assert.Equal("Concluido", (string?)body["data"]![0]!["Status"]);
        Assert.True(body["data"]![0]!.AsObject().ContainsKey("Response"));
    }

    [Theory]
    [InlineData(ZohoTriggers.Workflow, new[] { "workflow" })]
    [InlineData(ZohoTriggers.Workflow | ZohoTriggers.Blueprint, new[] { "workflow", "blueprint" })]
    [InlineData(ZohoTriggers.All, new[] { "workflow", "approval", "blueprint", "pathfinder", "orchestration" })]
    public async Task Envia_somente_os_triggers_pedidos(ZohoTriggers triggers, string[] expected)
    {
        _accounts.Enqueue(HttpStatusCode.OK, TokenJson);
        _crm.Enqueue(HttpStatusCode.OK, """{ "data": [ { "code": "SUCCESS", "status": "success" } ] }""");

        await CreateClient().UpdateRecordAsync("Deals", 7, new Dictionary<string, object?>(), CancellationToken.None, triggers);

        var sent = JsonNode.Parse(Assert.Single(_crm.Requests).Body)!["trigger"]!.AsArray();

        Assert.Equal(expected, sent.Select(item => (string?)item));
    }

    [Fact]
    public async Task Escrita_recusada_lanca_ZohoCrmException()
    {
        _accounts.Enqueue(HttpStatusCode.OK, TokenJson);
        _crm.Enqueue(HttpStatusCode.BadRequest, """{ "data": [ { "code": "INVALID_DATA", "message": "invalid data", "status": "error" } ] }""");

        var error = await Assert.ThrowsAsync<ZohoCrmException>(() =>
            CreateClient().CreateNoteAsync("Deals", 2, 3, "Titulo", "Conteudo", CancellationToken.None));

        Assert.Contains("INVALID_DATA", error.Message);
    }

    [Fact]
    public async Task Cria_nota_vinculada_ao_registro()
    {
        _accounts.Enqueue(HttpStatusCode.OK, TokenJson);
        _crm.Enqueue(HttpStatusCode.Created, """{ "data": [ { "code": "SUCCESS", "status": "success" } ] }""");

        await CreateClient().CreateNoteAsync("Deals", 2, 3, "Titulo", "Conteudo", CancellationToken.None);

        var request = Assert.Single(_crm.Requests);
        var note = JsonNode.Parse(request.Body)!["data"]![0]!;

        Assert.EndsWith("/crm/v8/Notes", request.Uri);
        Assert.Equal("Deals", (string?)note["Parent_Id"]!["module"]!["api_name"]);
        Assert.Equal("2", (string?)note["Parent_Id"]!["id"]);
        Assert.Equal("3", (string?)note["Owner"]!["id"]);
        Assert.Equal("Titulo", (string?)note["Note_Title"]);
    }

    [Fact]
    public async Task Token_recusado_renova_e_repete_uma_vez()
    {
        _accounts.Enqueue(HttpStatusCode.OK, TokenJson);
        _accounts.Enqueue(HttpStatusCode.OK, """{ "access_token": "tok-2", "expires_in": 3600 }""");
        _crm.Enqueue(HttpStatusCode.Unauthorized, """{ "code": "INVALID_TOKEN", "status": "error" }""");
        _crm.Enqueue(HttpStatusCode.NoContent, "");

        await CreateClient().GetRecordAsync("Deals", 1, CancellationToken.None);

        Assert.Equal(2, _accounts.Requests.Count);
        Assert.Equal(["Zoho-oauthtoken tok-1", "Zoho-oauthtoken tok-2"], _crm.Requests.Select(r => r.Authorization));
    }

    [Fact]
    public async Task Token_valido_e_reaproveitado()
    {
        _accounts.Enqueue(HttpStatusCode.OK, TokenJson);
        _crm.Enqueue(HttpStatusCode.NoContent, "");
        _crm.Enqueue(HttpStatusCode.NoContent, "");

        var client = CreateClient();

        await client.GetRecordAsync("Deals", 1, CancellationToken.None);
        await client.GetRecordAsync("Deals", 2, CancellationToken.None);

        Assert.Single(_accounts.Requests);
    }

    [Fact]
    public async Task Refresh_token_recusado_lanca_ZohoCrmException()
    {
        _accounts.Enqueue(HttpStatusCode.OK, """{ "error": "invalid_code" }""");

        var error = await Assert.ThrowsAsync<ZohoCrmException>(() =>
            CreateClient().GetRecordAsync("Deals", 1, CancellationToken.None));

        Assert.Contains("invalid_code", error.Message);
        Assert.Empty(_crm.Requests);
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? Authorization, string Body);

    /// <summary>Devolve respostas enfileiradas, na ordem, e guarda as requisições recebidas.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();

        public List<RecordedRequest> Requests { get; } = [];

        public void Enqueue(HttpStatusCode status, string body) => _responses.Enqueue((status, body));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                body));

            var (status, responseBody) = _responses.Dequeue();

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
