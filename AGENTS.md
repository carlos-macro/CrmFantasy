# AGENTS.md

Diretrizes para agentes de IA (e pessoas) trabalhando neste repositório.

## Objetivo do projeto

O **CrmFantasy** é uma API intermediária entre os consumidores internos e o CRM (hoje **Zoho CRM**).
Chamadas que hoje vão direto para as APIs do CRM passam a ser roteadas por aqui para:

- **Alterar**: ajustar request/response (renomear campos, normalizar formatos, aplicar regras).
- **Agregar**: combinar várias chamadas ao CRM (ou outras fontes) em uma única resposta.
- **Substituir**: responder sem chamar o CRM quando a informação puder vir de outra fonte (cache, banco, cálculo local).

A comunicação com o Zoho deve usar o **SDK oficial do Zoho CRM para .NET** sempre que ele cobrir o caso;
HTTP direto (`HttpClient`) apenas para o que o SDK não oferece.

O consumidor da API não deve precisar saber que o Zoho existe: contratos públicos são do CrmFantasy, não do Zoho.

## Regras obrigatórias para o agente

- **Nunca faça commit, push, merge, rebase ou crie branches/tags sem autorização explícita** do usuário no momento.
  Autorização anterior não vale para mudanças novas.
- **Não adicione co-autoria** (`Co-Authored-By`, "Generated with...", ou qualquer atribuição a IA) em commits, PRs ou código.
- Não altere segredos, `appsettings.*.json` com credenciais reais ou `UserSecretsId` sem pedido explícito.
- Não execute chamadas de escrita (create/update/delete) contra o Zoho CRM real sem autorização.
- Antes de concluir uma tarefa, rode `dotnet build` (e `dotnet test` se houver testes) e reporte o resultado.

## Stack

- .NET 10, C# com `Nullable` e `ImplicitUsings` habilitados.
- ASP.NET Core com **Controllers** (`[ApiController]`). **Não usar Minimal APIs** (`app.MapGet`, etc.).
- Configuração via `IOptions<T>`; segredos via User Secrets (dev) e variáveis de ambiente (demais ambientes).
- Testes: xUnit.

## Estrutura (compacta)

Um único projeto de API, organizado por pastas. Só crie um novo projeto (`.csproj`) quando houver motivo real
(ex.: o projeto de testes).

```
CrmFantasy.sln
AGENTS.md
CrmFantasy/
  Controllers/            # Endpoints HTTP. Finos: validam entrada, chamam um serviço, devolvem resultado.
  Services/               # Regras de negócio: alteração, agregação e substituição das chamadas ao CRM.
  Integrations/
    Zoho/                 # Único lugar que conhece o SDK/API do Zoho: cliente, autenticação, mapeamentos.
  Models/                 # DTOs públicos de request/response da API (contratos do CrmFantasy).
  Options/                # Classes de configuração tipada (ex.: ZohoOptions).
  Program.cs              # Composição: DI, controllers, middlewares.
  appsettings.json
CrmFantasy.Tests/         # Criar quando houver lógica a testar.
```

Regras de dependência:

- `Controllers` → `Services` → `Integrations`. Nunca o caminho inverso.
- Controllers **não** usam o SDK do Zoho diretamente.
- Tipos do SDK do Zoho **não** saem de `Integrations/Zoho`: mapeie para `Models` (ou tipos internos) antes de retornar.
- Se a pasta crescer demais, agrupe por domínio dentro dela (ex.: `Services/Leads/`, `Services/Deals/`) em vez de criar novas camadas.

## Convenções de código

- Namespaces *file-scoped*, seguindo a pasta (`CrmFantasy.Services`, `CrmFantasy.Integrations.Zoho`).
- Construtores primários para injeção de dependência (como em `Worker.cs`).
- `async`/`await` de ponta a ponta, sempre propagando `CancellationToken`.
- DTOs como `record`; nomes em inglês no código, sufixos `Request`/`Response` para contratos HTTP.
- Interfaces apenas quando houver mais de uma implementação ou necessidade real de *mock* em teste.
  Não crie `IFooService` só por hábito.
- Sem abstrações especulativas (repositórios genéricos, *mediators*, *factories*) sem necessidade concreta.
- Logs estruturados com `ILogger<T>` (`logger.LogInformation("... {LeadId}", id)`), nunca logar tokens ou dados sensíveis.
- Rotas: `api/[controller]`, recursos no plural, verbos HTTP corretos, retornos com `ActionResult<T>`.
- Erros: retornar `ProblemDetails`; tratar falhas do Zoho (timeout, 4xx, 5xx, rate limit) na camada de integração
  e traduzi-las para erros da API, sem vazar mensagens internas do CRM.

## Integração com o Zoho

- Toda chamada ao Zoho passa por `Integrations/Zoho`.
- Credenciais OAuth (client id/secret, refresh token, data center) vêm de `ZohoOptions`, nunca *hardcoded*.
- Respeite os limites de API do Zoho: prefira chamadas em lote, selecione apenas os campos necessários e
  considere cache quando o dado não precisar ser em tempo real.
- Ao migrar uma chamada, documente no PR/descrição: endpoint original do Zoho, endpoint novo do CrmFantasy
  e o que muda (alteração, agregação ou substituição).

## Comandos

```bash
dotnet build
dotnet test
dotnet run --project CrmFantasy
```
