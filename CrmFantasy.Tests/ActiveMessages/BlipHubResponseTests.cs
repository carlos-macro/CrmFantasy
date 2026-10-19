using System.Text.Json.Nodes;
using CrmFantasy.Services.ActiveMessages;

namespace CrmFantasy.Tests.ActiveMessages;

public class BlipHubResponseTests
{
    [Theory]
    [InlineData("""{ "Status": true, "Message": "formato antigo da v1" }""", false)]
    [InlineData("""{ "success": true, "data": {} }""", true)]
    [InlineData("""{ "success": false, "messages": ["erro"] }""", false)]
    [InlineData("""{ "success": "true" }""", false)]
    [InlineData("""[]""", false)]
    public void Reconhece_sucesso_pelo_campo_success(string body, bool expected) =>
        Assert.Equal(expected, BlipHubResponse.IsSuccess(JsonNode.Parse(body)));

    [Fact]
    public void Sem_resposta_nao_e_sucesso() => Assert.False(BlipHubResponse.IsSuccess(null));

    [Fact]
    public void Le_id_da_campanha() =>
        Assert.Equal("abc-123", BlipHubResponse.CampaignId(JsonNode.Parse("""
            { "data": { "response": { "resource": { "id": "abc-123" } } }, "success": true }
            """)));

    [Theory]
    [InlineData("""{ "data": { "response": { "resource": null } }, "success": false }""")]
    [InlineData("""{ "data": null }""")]
    public void Sem_caminho_da_campanha_devolve_null(string body) =>
        Assert.Null(BlipHubResponse.CampaignId(JsonNode.Parse(body)));
}
