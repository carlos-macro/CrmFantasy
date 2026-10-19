# PRD 0001: Migração da função "Blip - Mensagem Ativa - Integração" para o CrmFantasy

| | |
|---|---|
| **Status** | Endpoint implementado no CrmFantasy (2026-10-16), ainda não validado contra o CRM. A função Deluge fina (seção 6) e a implantação (seção 7) estão pendentes. Ver seção 10 |
| **Data** | 2026-10-16 |
| **Origem** | `automation.EnviarMensagemAtivaBlip` em [zoho-functions/CRM/Mensagens Ativas/Blip - Mensagem Ativa - Integração.deluge](../../../zoho-functions/CRM/Mensagens%20Ativas/Blip%20-%20Mensagem%20Ativa%20-%20Integra%C3%A7%C3%A3o.deluge) |
| **Destino** | Endpoint no CrmFantasy, com acesso ao CRM pela API REST v8 do Zoho (`HttpClient` tipado) |
| **Tipo de migração** | Substituição (a regra sai do Deluge), com a função Deluge mantida como gatilho fino |
| **Decisão (2026-10-16)** | A rota v1 (AutomatusHub) foi descontinuada. O CrmFantasy envia **só pela v2** (hub.macro) e não lê a org variable `mensagem_ativa_v2_list` |

> A análise usa a versão da função que está no repositório. Antes de implementar, rode `python cli/zf.py diff "CRM/Mensagens Ativas/Blip - Mensagem Ativa - Integração.deluge"` em `zoho-functions/` para confirmar que o CRM não tem uma versão mais nova.

## 1. Contexto

A função recebe o ID de um registro do módulo `Integracoes` (`CustomModule15`), monta o pedido de mensagem ativa do Blip a partir dele, envia para o hub e registra o resultado no CRM. Hoje toda essa regra vive em Deluge, o que traz quatro problemas:

1. **Credenciais no código.** `access-token` e `x-api-key` do `hub.macro.id` e a `zapikey` do webhook do Zoho Flow estão fixos na função (ver `zoho-functions/docs/PROBLEMAS-CONHECIDOS.md`).
2. **Falhas que interrompem a função no meio.** Ver seção 4.
3. **Sem testes.** A regra de escolha entre as rotas v1 e v2 tem cinco condições combinadas e não tem cobertura. Com a descontinuação da v1, essa regra deixa de existir.
4. **Pouca observabilidade.** O diagnóstico depende de `info` no log da função e da nota gravada no registro.

### 1.1 Comportamento atual

1. Converte `IdentificadorIntegracao` para número e lê o registro em `Integracoes`.
2. Converte o campo `Request` (JSON em texto) para mapa. Esse é o corpo enviado ao hub.
3. Lê o registro de origem: módulo em `Integracoes.Modulo` e ID em `Integracoes.Identificador`. Usa `C_digo_do_Pedido`, `Ocorr_ncia`, `Stage` (só para o texto da nota) e `Whatsapp_BSUID`.
4. Se `Whatsapp_BSUID` estiver preenchido, grava esse valor em `request.BSUID`.
5. Escolhe a rota pela org variable `mensagem_ativa_v2_list` (JSON). A v2 fica ativa quando qualquer condição abaixo é verdadeira:
   - `enviarTodasAsMensagensNaRotaV2` é `true`;
   - `ChatBotMessageName` está em `mensagensMigradasV2`;
   - `ChatBotMessageName` está em `mensagensEmTesteV2` e `enviarMensagensDaListaParaQualquerTelefone` é `true`;
   - `ChatBotMessageName` está em `mensagensEmTesteV2` e `CellPhoneNumber` está em `telefones`;
   - `CellPhoneNumber` está em `telefones` e `enviarQualquerMensagemParaTelefonesDaLista` é `true`.

   Se a leitura da variável falhar, a função usa a v1.
6. Envia `POST` com o `request` como corpo JSON:
   - **v1**: `https://hub.automatuslab.com/blip/enviarabandono` (`AutomatusHub`, `BlipController.EnviarAbandono`), com `x-api-key`. Resposta: `{ "Status": bool, "Message": string }`.
   - **v2**: `https://hub.macro.id/blip/mensagem` (`hub.macro`, `BlipEndpoint.SubmitMensagemAtiva`), com `x-api-key` e `access-token`. Corpo no formato `OldMensagemAtivaBlipDto`. Resposta: `ResponseDto<SubmitCampaingFullOutDto>`, isto é, `{ "data": { "response": { "resource": { "id": ... } } }, "success": bool, "messages": [...] }`. Responde `400` quando `success` é `false`.
7. Atualiza o registro de `Integracoes`:
   - `qtdTentativa` + 1;
   - `Response` com a resposta em texto;
   - `ID_Campanha` com `data.response.resource.id`;
   - `Status` = `Concluido` se `Status` ou `success` forem `true`; senão `Pendente` se for a 1ª ou 2ª tentativa; senão `Erro`.
8. Cria uma nota no registro de origem com o título `Integração Externa - BLIP - Mensagem Ativa`, os dados do pedido, o template e a resposta (ou o erro), com dono fixo (`Owner.id` = usuário de integração).
9. Se o status ficou `Pendente`, chama o webhook do Zoho Flow com `{ id, module: "CustomModule15", property: "Status", time: "0.08 Minutes", value: "Processar" }`. Cerca de 5 segundos depois, o Flow muda o `Status` para `Processar`, o que dispara a função de novo (retentativa).

### 1.2 Projetos envolvidos

| Projeto | Papel |
|---|---|
| `zoho-functions` | Função de origem. Depois da migração, vira apenas o gatilho que chama o CrmFantasy |
| `CrmFantasy` | Passa a ter a regra: leitura e escrita no CRM pela API REST do Zoho e chamada ao hub v2 |
| `hub.macro` | Destino do envio (`POST /blip/mensagem`). Não muda |
| `AutomatusHub` | Rota v1, descontinuada. O CrmFantasy não a chama |

## 2. Objetivos

1. Levar a regra da função para o CrmFantasy, mantendo o comportamento que o CRM e o Blip enxergam (seção 1.1), com as exceções corrigidas na seção 4.
2. Ler e escrever no CRM somente pela API REST v8 do Zoho, dentro de `Integrations/Zoho`.
3. Tirar as credenciais do código: hub e OAuth do Zoho passam a vir de configuração (User Secrets em dev, variáveis de ambiente nos demais).
4. Cobrir com testes a máquina de status e o fluxo.
5. Deixar a volta para a função antiga simples (seção 7).

### Critérios de sucesso

- Para o mesmo registro de `Integracoes` enviado pela v2, o CrmFantasy envia o mesmo corpo e grava os mesmos campos e a mesma nota que a função Deluge.
- Uma falha de rede ou uma resposta sem `data.response.resource.id` não interrompe o processo: a tentativa é contada e a nota é criada.
- Nenhuma credencial aparece no código, no `appsettings.json` versionado, nos logs ou na resposta da API.
- `dotnet build` e `dotnet test` passam no CrmFantasy.

## 3. Fora de escopo

- Alterar `hub.macro` ou `AutomatusHub`.
- Apagar a org variable `mensagem_ativa_v2_list` do CRM. Ela deixa de ser lida pelo CrmFantasy, mas a remoção é decisão à parte.
- Mudar o formato do campo `Request` ou o processo que cria os registros de `Integracoes`.
- Retentativa automática. O CrmFantasy terá um mecanismo próprio, definido em outro PRD; o webhook do Zoho Flow não é usado.
- Migrar `Blip - Mensagem Ativa - Pedido.deluge` (`automation.BlipEnviaMensagemAtiva`), que usa só a v1. Pode reaproveitar este trabalho num PRD seguinte.
- Remover a função do CRM.

## 4. Problemas atuais e decisão para cada um

| # | Problema na função | Efeito | Decisão |
|---|---|---|---|
| 1 | Se o `invokeurl` lança exceção, `response` fica `null` e `response.get("data")` falha | A função para: `Integracoes` não é atualizada, a nota não é criada e não há retentativa | **Corrigir**: contar a tentativa, gravar o erro em `Response` e seguir o fluxo |
| 2 | A resposta da v1 (`{ Status, Message }`) não tem `data.response.resource.id` | Na rota v1, a leitura de `ID_Campanha` deve falhar do mesmo modo, mesmo com envio bem-sucedido | Deixa de existir: a v1 foi descontinuada |
| 3 | Na v2 com `success: false`, `resource` pode vir nulo | Mesmo efeito do item 1 | **Corrigir**, junto com o item 2 |
| 4 | `qtdTentativa` vazio no primeiro processamento | Depende de como o Deluge soma `null + 1` | Tratar vazio como 0 |
| 5 | `ID_Externo` é gravado com o próprio valor | Sem efeito | Não reproduzir |
| 6 | Credenciais fixas no código | Vazamento e troca difícil | Mover para configuração |

Fora esses itens, o comportamento deve ser reproduzido como está, inclusive o limite de 3 tentativas e o texto da nota.

## 5. Solução proposta

### 5.1 Contrato da API

`POST /api/active-messages`

```json
{ "integrationId": "4918426000123456789" }
```

Resposta `200`:

```json
{
  "integrationId": "4918426000123456789",
  "status": "Concluido",
  "attempt": 1,
  "campaignId": "…"
}
```

- `404` (`ProblemDetails`): o registro de `Integracoes` não existe ou não tem `Request`. Nesse caso, a função atual só registra a nota "Não foi possível recuperar dados da Integração." quando consegue ler o módulo de origem; o comportamento novo é retornar 404 e registrar a mesma nota quando for possível.
- `502` (`ProblemDetails`): falha ao ler ou gravar no CRM. Uma falha do **hub** não gera erro HTTP: ela vira `Pendente`/`Erro` no registro, como hoje.
- A rota é protegida por chave de API do CrmFantasy (header `x-api-key`, valor em configuração).

O consumidor não precisa conhecer o hub. O contrato é do CrmFantasy.

### 5.2 Estrutura (segue `CrmFantasy/AGENTS.md`)

```text
CrmFantasy/
  Controllers/ActiveMessagesController.cs     # valida entrada, chama o serviço
  Models/ActiveMessages/                      # SendActiveMessageRequest/Response (records)
  Services/ActiveMessages/
    ActiveMessageService.cs                   # orquestra o fluxo da seção 1.1
    BlipHubResponse.cs                        # sucesso e ID da campanha na resposta do hub
  Services/CrmIntegrations/
    CrmIntegrationService.cs                  # carrega integração (com ou sem origem), grava tentativa e status
    CrmIntegrationStatus.cs                   # Concluido / Pendente / Erro (função pura)
  Services/Notes/CrmNoteService.cs            # notas em qualquer módulo
  Integrations/
    Zoho/                                     # API REST: Integracoes, módulo de origem, Notes
    Blip/BlipHubClient.cs                     # HttpClient tipado para o hub v2
  Options/                                    # ZohoOptions, BlipHubOptions, CrmIntegrationOptions, CrmNoteOptions
```

- O corpo de `Request` é repassado ao hub como JSON, sem tipá-lo por completo (`JsonObject`), apenas acrescentando `BSUID`. Assim, campos novos no `Request` continuam chegando ao hub sem mudança no CrmFantasy.
- O JSON do Zoho não sai de `Integrations/Zoho`: os serviços recebem `CrmRecord`.
- O módulo `Integracoes` e o máximo de tentativas (3) ficam em `CrmIntegrationOptions`; o dono da nota (`Owner.id`), em `CrmNoteOptions`.

### 5.3 API REST do Zoho

- Chamar a API REST v8 do Zoho CRM com `HttpClient` tipado (`ZohoCrmClient`), sem o SDK oficial.
- Operações: ler registro por ID (`Integracoes` e módulo de origem), atualizar registro (`Integracoes`), criar nota.
- Credenciais OAuth (client id/secret, refresh token, data center) em `ZohoOptions`. O access token é renovado pelo refresh token e fica só em memória (`ZohoTokenProvider`); nada é gravado em disco.

## 6. Mudança na função Deluge

Depois que o endpoint estiver validado, `EnviarMensagemAtivaBlip` passa a só chamar o CrmFantasy:

```text
void automation.EnviarMensagemAtivaBlip(String IdentificadorIntegracao)
  → POST <crmfantasy>/api/active-messages { integrationId }
  → registra a resposta com info
```

- A chave do CrmFantasy fica numa org variable, não no código.
- O workflow do CRM que chama a função não muda.
- A mudança segue o fluxo de `zoho-functions/AGENTS.md`: `sync` antes, commit, `push` na sandbox e, só com pedido explícito, em produção.
- Atualizar `zoho-functions/docs/DEPENDENCIAS.md` (a função passa a depender do CrmFantasy e deixa de chamar o hub e o Flow) e `PROBLEMAS-CONHECIDOS.md` (credenciais removidas).

## 7. Implantação e volta

1. Implementar e testar o CrmFantasy contra a **sandbox** do CRM.
2. Publicar a função fina na sandbox e validar com registros de `Integracoes` com sucesso, falha do hub e 3ª tentativa.
3. Publicar o CrmFantasy no ambiente de produção.
4. Publicar a função fina em produção (pedido explícito do desenvolvedor).
5. Acompanhar os registros `Pendente`/`Erro` e as notas nos primeiros dias.

**Volta:** publicar de novo a versão anterior da função com `zf push`, a partir do commit anterior em `zoho-functions`. O CrmFantasy não guarda estado próprio, então não há dado a desfazer.

## 8. Testes

- `BlipHubResponse`: sucesso (`success: true`), formato antigo da v1 não conta como sucesso, ID da campanha.
- `CrmIntegrationService`: falha na 1ª, 2ª e 3ª tentativa, `qtdTentativa` vazio, `Request` inválido, integração ou origem inexistente.
- `ActiveMessageService` com o CRM e o hub simulados: `BSUID` aplicado; falha de rede no hub (problema 1); falha sem `data` não grava `ID_Campanha` (problema 3); nota criada em todos os casos com módulo de origem.
- Comparação manual na sandbox: o mesmo registro processado pela função antiga e pelo CrmFantasy gera os mesmos campos e a mesma nota.

## 9. Questões em aberto

1. Onde o CrmFantasy vai rodar em produção e qual URL a função vai chamar?
2. Qual data center e qual usuário/OAuth client o CrmFantasy usa? O webhook em `flow.zoho.com` sugere o data center US.
3. ~~Confirmar se a rota v1 falha ao gravar `ID_Campanha`.~~ Sem efeito: v1 descontinuada.
4. ~~A rota v1 ainda precisa existir?~~ Respondida em 2026-10-16: não. O envio é só pela v2.
5. Algum outro processo lê `Integracoes.Response` esperando o texto exato gerado pelo Deluge (`toString()` do mapa)? O CrmFantasy vai gravar o JSON da resposta.

## 10. Implementação

### 10.1 O que foi feito

| Parte | Onde |
|---|---|
| Endpoint `POST /api/active-messages` com `x-api-key` | `Controllers/ActiveMessagesController.cs`, `Http/RequireApiKeyAttribute.cs` |
| Falha do CRM → `502` sem mensagem interna | `Http/ZohoCrmExceptionHandler.cs` |
| Fluxo da seção 1.1 | `Services/ActiveMessages/ActiveMessageService.cs` |
| Ciclo da integração (carga, tentativa, status), reutilizável por outros fluxos | `Services/CrmIntegrations/CrmIntegrationService.cs` |
| Notas no CRM, reutilizável por outros fluxos | `Services/Notes/CrmNoteService.cs` |
| API REST do Zoho (registro e nota) e renovação do token | `Integrations/Zoho/ZohoCrmClient.cs`, `Integrations/Zoho/ZohoTokenProvider.cs` |
| Hub v2 | `Integrations/Blip/BlipHubClient.cs` |
| Testes (45) | `CrmFantasy.Tests/` |

### 10.2 Configuração

Os segredos vão em User Secrets (dev) ou variáveis de ambiente (`Secao__Chave`). A aplicação não sobe sem eles.

| Chave | Segredo | Observação |
|---|---|---|
| `ApiAccess:ApiKey` | sim | Chave que a função Deluge envia em `x-api-key`. Mínimo de 16 caracteres |
| `Zoho:ClientId`, `Zoho:ClientSecret`, `Zoho:RefreshToken` | sim | Escopos necessários: leitura e escrita de módulos, notas e leitura de variáveis |
| `Zoho:DataCenter`, `Zoho:Environment` | não | Padrão `US` / `Production`. Use `Sandbox` para a etapa 1 da seção 7 |
| `BlipHub:ApiKey`, `BlipHub:AccessToken` | sim | Mesmos valores hoje fixos na função para a v2 |
| `BlipHub:Url` | não | Padrão: `https://hub.macro.id/blip/mensagem` |
| `CrmNotes:OwnerId` | não | Dono das notas criadas pelo CrmFantasy em qualquer módulo. Padrão: usuário de integração usado pela função |
| `CrmIntegrations:*` | não | Módulo (`Module`) e limite de tentativas (`MaxAttempts`). Padrões iguais aos da função |

### 10.3 Decisões tomadas na implementação

- **API REST em vez do SDK:** a primeira versão usou o pacote `ZOHOCRMSDK-8.0` 4.0.0, que só publica binário para .NET Framework (`net461`), é síncrono e guarda a configuração em estado estático. Foi trocado pela API REST v8 com `HttpClient` tipado: assíncrono, injetado por DI e no mesmo padrão dos outros projetos da Macro que chamam o Zoho. O access token é renovado pelo refresh token (credenciais no corpo do POST, não na URL) e, se o CRM o recusar com 401, é renovado e a chamada é repetida uma vez. As operações de registro e nota **não** foram exercitadas contra um CRM real.
- **Workflows:** toda escrita no CRM aceita, opcionalmente, as automações a disparar (`ZohoTriggers`: workflow, approval, blueprint, pathfinder, orchestration); o padrão é `ZohoTriggers.None`. O cliente sempre envia a chave `trigger`, porque sem ela o Zoho dispara todas. Este fluxo usa o padrão (`trigger: []`) na atualização da integração e na nota, como o `zoho.crm.updateRecord`/`createRecord` do Deluge sem o parâmetro de trigger.
- **Picklists:** na API REST, picklists são lidos e gravados como texto, sem tratamento especial. Lookups e usuários são lidos como o ID.
- **Só v2:** a v1 foi descontinuada. O CrmFantasy não lê `mensagem_ativa_v2_list` e envia todas as mensagens ao hub.macro; o sucesso é lido só de `success`. Isso elimina uma chamada ao CRM por mensagem em relação à função (de 4 para 3).
- **Falha ao criar a nota** só gera log. **Falha ao atualizar a integração** devolve `502`, com log do resultado do hub, já que a mensagem pode ter saído.
- **Sem retentativa (decisão 2026-10-16):** o envio da mensagem ativa não chama mais o webhook do Zoho Flow. Uma falha conta a tentativa e deixa a integração `Pendente` (ou `Erro` na 3ª), mas nada a reprocessa automaticamente. O passo 9 da seção 1.1 não é reproduzido e o CrmFantasy não tem cliente do Zoho Flow; a retentativa será implementada de outra forma neste projeto.
- **Registro de origem ausente** (ou `Modulo`/`Identificador` vazios): o CrmFantasy não envia a mensagem, não altera a integração e devolve `404`. A função também parava nesse ponto, por erro.

### 10.4 Pendências

1. Configurar credenciais da sandbox e validar o endpoint contra registros reais de `Integracoes` (etapas 1 e 2 da seção 7), confirmando em especial o tipo do campo `Status` e a gravação de `qtdTentativa`.
2. Responder as questões em aberto da seção 9, em especial onde o CrmFantasy vai rodar.
3. Escrever a função Deluge fina (seção 6) em `zoho-functions`, seguindo o fluxo de `sync`/`push` daquele repositório.
