# CrmFantasy

API intermediária entre os consumidores internos e o **Zoho CRM**. Chamadas que iam direto ao CRM (ou a funções
Deluge) passam por aqui para serem alteradas, agregadas ou substituídas. O acesso ao CRM é feito pela
**API REST v8 do Zoho**.

Diretrizes de código e arquitetura: [AGENTS.md](AGENTS.md). Migrações documentadas: [docs/prds](docs/prds).

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Acesso de administrador (ou permissão de API) ao Zoho CRM para gerar as credenciais OAuth (ver
  [Gerando o token do Zoho](#gerando-o-token-do-zoho))
- Chaves do hub.macro para o envio de mensagens ativas do Blip

## Configuração

A aplicação **não sobe** sem as configurações obrigatórias: elas são validadas na inicialização.

Segredos nunca vão para o repositório nem para os `appsettings*.json` versionados:

- **Desenvolvimento:** [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (o projeto já tem `UserSecretsId`).
- **Demais ambientes:** variáveis de ambiente, no formato `Secao__Chave` (ex.: `Zoho__ClientId`).

Os arquivos `CrmFantasy/appsettings.Development.example.json` e `CrmFantasy/appsettings.Production.example.json`
mostram a estrutura completa e servem só como referência (não são carregados pela aplicação).

### Chaves

| Chave | Obrigatória | Segredo | Descrição |
|---|---|---|---|
| `ApiAccess:ApiKey` | sim | sim | Chave que os consumidores enviam no header `x-api-key`. Mínimo de 16 caracteres. |
| `Zoho:ClientId` | sim | sim | Client ID do Self Client do Zoho. |
| `Zoho:ClientSecret` | sim | sim | Client Secret do Self Client do Zoho. |
| `Zoho:RefreshToken` | sim | sim | Refresh token gerado a partir do grant token. |
| `Zoho:DataCenter` | não | não | `US` (padrão), `EU`, `IN`, `AU`, `JP` ou `CA`. |
| `Zoho:Environment` | não | não | `Production` (padrão), `Sandbox` ou `Developer`. Precisa ser o mesmo ambiente em que o token foi gerado. |
| `BlipHub:ApiKey` | sim | sim | Header `x-api-key` do hub.macro. |
| `BlipHub:AccessToken` | sim | sim | Header `access-token` do hub.macro. |
| `BlipHub:Url` | não | não | Padrão: `https://hub.macro.id/blip/mensagem`. |
| `CrmIntegrations:Module` | não | não | Módulo de integrações no CRM. Padrão: `Integracoes`. |
| `CrmIntegrations:MaxAttempts` | não | não | Tentativas antes de marcar a integração como `Erro`. Padrão: `3`. |
| `CrmNotes:OwnerId` | não | não | ID do usuário dono das notas criadas pelo CrmFantasy. |

### Desenvolvimento (User Secrets)

Na raiz do repositório:

```bash
dotnet user-secrets set "ApiAccess:ApiKey" "<chave-com-16-ou-mais-caracteres>" --project CrmFantasy

dotnet user-secrets set "Zoho:ClientId" "<client-id>" --project CrmFantasy
dotnet user-secrets set "Zoho:ClientSecret" "<client-secret>" --project CrmFantasy
dotnet user-secrets set "Zoho:RefreshToken" "<refresh-token>" --project CrmFantasy
dotnet user-secrets set "Zoho:Environment" "Sandbox" --project CrmFantasy

dotnet user-secrets set "BlipHub:ApiKey" "<x-api-key-do-hub>" --project CrmFantasy
dotnet user-secrets set "BlipHub:AccessToken" "<access-token-do-hub>" --project CrmFantasy
```

Para conferir o que está configurado: `dotnet user-secrets list --project CrmFantasy`.

### Produção (variáveis de ambiente)

```bash
ApiAccess__ApiKey=<chave>
Zoho__ClientId=<client-id>
Zoho__ClientSecret=<client-secret>
Zoho__RefreshToken=<refresh-token>
Zoho__DataCenter=US
Zoho__Environment=Production
BlipHub__ApiKey=<x-api-key-do-hub>
BlipHub__AccessToken=<access-token-do-hub>
ASPNETCORE_ENVIRONMENT=Production
```

## Gerando o token do Zoho

O CrmFantasy usa um **Self Client** do Zoho: um client OAuth sem tela de login, próprio para aplicações de servidor.
Você gera um **grant token** (válido por poucos minutos), troca-o uma única vez por um **refresh token** e configura
o refresh token na aplicação. A partir daí, o CrmFantasy gera sozinho os access tokens (válidos por 1 hora) e os
renova quando expiram.

> Gere o token com um **usuário de integração** (não com um usuário pessoal): as ações feitas pela API ficam
> registradas no CRM em nome dele e herdam as permissões do perfil dele.

### 1. Criar o Self Client

1. Acesse o **Zoho API Console** do data center da conta (US: <https://api-console.zoho.com>; EU:
   `api-console.zoho.eu`; IN: `api-console.zoho.in`; AU: `api-console.zoho.com.au`; JP: `api-console.zoho.jp`;
   CA: `api-console.zohocloud.ca`) e entre com o usuário de integração.
2. Clique em **Add Client** (ou **Get Started**), escolha **Self Client** e clique em **Create Now** → **OK**.
3. Na aba **Client Secret**, copie o **Client ID** e o **Client Secret**. Eles vão em `Zoho:ClientId` e `Zoho:ClientSecret`.

O mesmo Self Client serve para todos os ambientes; o que muda por ambiente é o refresh token.

### 2. Gerar o grant token

1. No Self Client, abra a aba **Generate Code**.
2. Em **Scope**, informe os escopos separados por vírgula. Para o que o CrmFantasy faz hoje (ler e atualizar
   registros, criar notas):

   ```
   ZohoCRM.modules.READ,ZohoCRM.modules.CREATE,ZohoCRM.modules.UPDATE
   ```

   Inclua outros escopos só quando uma funcionalidade nova precisar deles. Evite `ZohoCRM.modules.ALL`, que também
   permite excluir registros.
3. Em **Time Duration**, escolha o tempo de validade do grant token (o padrão é 3 minutos; escolha 10 para ter folga).
4. Preencha a descrição (ex.: `CrmFantasy - Sandbox`) e clique em **Create**.
5. Escolha o **portal**, o **ambiente** (Production, Sandbox ou Developer) e a **organização**, e confirme. O token só
   funciona no ambiente escolhido aqui, que precisa ser o mesmo de `Zoho:Environment`.
6. Copie o **grant token** exibido. Ele só pode ser usado uma vez e expira no tempo escolhido.

### 3. Trocar o grant token pelo refresh token

Faça a troca antes de o grant token expirar. Use a URL de Accounts do data center da conta (US:
`https://accounts.zoho.com`; EU: `https://accounts.zoho.eu`; IN: `https://accounts.zoho.in`; AU:
`https://accounts.zoho.com.au`; JP: `https://accounts.zoho.jp`; CA: `https://accounts.zohocloud.ca`).

```bash
curl -X POST "https://accounts.zoho.com/oauth/v2/token" -d "grant_type=authorization_code" -d "client_id=CLIENT_ID" -d "client_secret=CLIENT_SECRET" -d "code=GRANT_TOKEN"
```

Antes de executar, substitua:

| Campo | O que colocar | Onde encontrar |
|---|---|---|
| `https://accounts.zoho.com` | URL de Accounts do data center da conta. Só muda se a conta não for do US. | Lista de URLs no início deste passo. É o mesmo data center configurado em `Zoho:DataCenter`. |
| `CLIENT_ID` | Identificador público do Self Client. Começa com `1000.`. | API Console → Self Client → aba **Client Secret** (passo 1). Mesmo valor de `Zoho:ClientId`. |
| `CLIENT_SECRET` | Senha do Self Client, que prova que a requisição vem do dono do client. | API Console → Self Client → aba **Client Secret** (passo 1). Mesmo valor de `Zoho:ClientSecret`. |
| `GRANT_TOKEN` | Código de autorização de uso único, que define escopos e ambiente do refresh token. | Gerado na aba **Generate Code** (passo 2). Expira no tempo escolhido em **Time Duration**. |

Não altere `grant_type=authorization_code`: ele indica ao Zoho que a requisição troca um grant token por tokens.

No PowerShell, use `curl.exe` no lugar de `curl`.

A resposta traz o `refresh_token`:

```json
{
  "access_token": "1000.xxxx",
  "refresh_token": "1000.yyyy",
  "api_domain": "https://www.zohoapis.com",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

Guarde o `refresh_token` em `Zoho:RefreshToken` (User Secrets ou variável de ambiente). O `access_token` pode ser
descartado: o CrmFantasy gera os seus.

Se a resposta vier com `"error": "invalid_code"`, o grant token expirou ou já foi usado: gere outro (passo 2).

### Cuidados com o refresh token

- O refresh token **não expira**, mas pode ser revogado (no API Console ou na conta do usuário). Trate-o como senha.
- Não compartilhe o mesmo refresh token entre ambientes: Production, Sandbox e Developer exigem tokens próprios.
- O Zoho limita a quantidade de refresh tokens por usuário; revogue os que não estão mais em uso.
- Se o CrmFantasy registrar `Zoho recusou a renovação do token`, o refresh token foi revogado ou não corresponde ao
  Client ID/Secret ou ao data center configurados: gere um novo.

## Executando

```bash
dotnet run --project CrmFantasy
```

- HTTP: <http://localhost:5080>, HTTPS: <https://localhost:7080> (perfis em `CrmFantasy/Properties/launchSettings.json`).
- Em `Development`, a documentação interativa (Scalar) fica em <http://localhost:5080/scalar> e o documento OpenAPI
  em `/openapi/v1.json`. Em produção, nenhum dos dois é exposto.

Exemplo de chamada:

```bash
curl -X POST "http://localhost:5080/api/active-messages" \
  -H "Content-Type: application/json" \
  -H "x-api-key: <ApiAccess:ApiKey>" \
  -d '{ "integrationId": "4918426000000000001" }'
```

> Atenção: com `Zoho:Environment=Production`, as chamadas **gravam no CRM real** e enviam mensagens de verdade.
> Para testar, use um token gerado para a Sandbox.

## Testes

```bash
dotnet test
```

Os testes não acessam o Zoho nem o hub: o CRM e as respostas HTTP são simulados.

## Estrutura

```
CrmFantasy/
  Controllers/        # Endpoints HTTP
  Services/           # Regras de negócio (alteração, agregação, substituição)
  Integrations/Zoho/  # Cliente REST do Zoho CRM, token OAuth e mapeamentos
  Integrations/Blip/  # Cliente do hub.macro
  Models/             # Contratos públicos da API
  Options/            # Configuração tipada
  Http/               # Autenticação por API key e tratamento de erros
CrmFantasy.Tests/     # Testes (xUnit)
docs/prds/            # Documentação das migrações
```
