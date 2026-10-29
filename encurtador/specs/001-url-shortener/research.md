# Research: Encurtador de links

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-29

Cada item registra a decisão, o motivo e as alternativas avaliadas. Os itens R-01 a R-05
correspondem às decisões D-01 a D-05 do plano técnico fornecido; os demais resolvem pontos
que o plano não definia ou que conflitavam com a constituição ou com a spec.

## R-01 — Armazenamento (D-01)

- **Decision**: SQLite em arquivo via EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`),
  com o modo de journal WAL e o schema criado por migrations aplicadas na inicialização.
- **Rationale**: FR-008/SC-005 exigem persistência entre reinícios, e a constituição
  (princípio I) fixa SQLite via EF Core. Com WAL, as leituras (redirecionamentos e
  estatísticas) não bloqueiam a escrita do contador. Com migrations, o schema evolui sem
  apagar o banco.
- **Alternatives considered**: SQLite em memória (perde dados no reinício e viola FR-008);
  `EnsureCreated` (não permite evoluir o schema depois); PostgreSQL (fora da stack da
  constituição).

## R-02 — Contador concorrente (D-02)

- **Decision**: cada acesso faz um único `UPDATE ... SET AccessCount = AccessCount + 1`
  (`ExecuteUpdateAsync`), filtrado por código e por validade
  (`ExpiresAtUtc IS NULL OR ExpiresAtUtc > now`). Se nenhuma linha for afetada, uma leitura
  separada distingue "não existe" (404) de "expirado" (410). O destino é lido do mesmo link;
  como ele é imutável (FR-018), ler antes ou depois do incremento não abre condição de
  corrida.
- **Rationale**: o incremento atômico no banco não perde atualizações sob concorrência
  (FR-005, FR-012, SC-003, SC-007) e nunca diminui o valor. Colocar a validade no `WHERE`
  impede contar um acesso no instante em que o link expira (FR-011).
- **Alternatives considered**: ler o valor, somar na aplicação e regravar (perde incrementos
  concorrentes); concorrência otimista com retry (mais complexa e falha sob 100 acessos
  simultâneos); uma tabela de eventos de acesso agregada depois (a spec não pede histórico
  e o total precisa ser exato e imediato).
- **Note**: o SQLite serializa as escritas. O `Microsoft.Data.Sqlite` espera até 30 s quando
  o banco está ocupado, o que basta para 100 incrementos simultâneos (SC-007).

## R-03 — Alfabeto do código automático (D-03)

- **Decision**: códigos automáticos têm 7 caracteres sorteados com
  `RandomNumberGenerator.GetItems` do alfabeto `ABCDEFGHJKLMNPQRSTUVWXYZ123456789`
  (A-Z sem `I` e `O`, mais 1-9: 33 símbolos, ≈ 4,26 × 10¹⁰ combinações). Códigos
  personalizados continuam aceitando todo o conjunto `A-Z` e `1-9` (`^[A-Z1-9]{4,20}$`).
- **Rationale**: `0` já está proibido pela spec; tirar `I` e `O` elimina as confusões com `1`
  e `0` ao ler o link em voz alta. Um subconjunto de A-Z/1-9 continua cumprindo FR-002/FR-013.
  Restringir os códigos personalizados contrariaria FR-003, então o D-03 vale só para os
  automáticos.
- **Alternatives considered**: remover também `S/5`, `Z/2` e `B/8` (o ganho é pequeno e o
  espaço de códigos diminui); `Random` comum (previsível, e o gerador criptográfico não
  traz custo relevante).

## R-04 — Unicidade do código (D-04)

- **Decision**: índice único em `Links.Code`, além da verificação prévia no serviço. A
  inserção que viola o índice (SQLite `SQLITE_CONSTRAINT_UNIQUE`, código estendido 2067) é
  traduzida pelo repositório em um resultado "código em uso". O serviço reage conforme o
  tipo de código:
  - automático: conta como colisão e tenta outro código, até 5 tentativas no total
    (FR-014);
  - personalizado: recusa imediatamente com 409.
- **Rationale**: a verificação prévia sozinha tem condição de corrida, e o índice é a
  garantia final (FR-009, SC-004). Links nunca são apagados (não existe exclusão nesta
  feature), então o índice também impede reutilizar o código de um link expirado.
- **Alternatives considered**: apenas a verificação no serviço (sujeita a corrida); um lock
  na aplicação (não protege múltiplas instâncias e serializa toda criação).

## R-05 — Armazenamento da chave de gerenciamento (D-05)

- **Decision**:
  - **Geração**: 32 bytes de `RandomNumberGenerator` (256 bits ≥ os 128 exigidos por
    FR-020), codificados em Base64Url (43 caracteres).
  - **Armazenamento**: somente o SHA-256 da chave, em `ManagementKeyHash` (FR-022).
  - **Unicidade**: índice único em `Links.ManagementKeyHash`. Se a inserção encontrar uma
    colisão de hash, o serviço gera outra chave e tenta novamente; a colisão não consome as
    tentativas reservadas a códigos automáticos.
  - **Verificação**: calcula o SHA-256 da chave recebida e compara com
    `CryptographicOperations.FixedTimeEquals`. Quando o código não existe, a comparação é
    feita contra um hash fictício, para responder 401 com o mesmo custo (FR-023).
- **Rationale**: o plano citava "DC-03", que não existe na spec; os requisitos
  correspondentes são FR-020 a FR-023. O índice único torna a distinção exigida por FR-020
  verificável também sob criações concorrentes, em vez de depender somente da probabilidade
  de colisão. Com 256 bits aleatórios, um hash rápido não é vulnerável a força bruta.
  Algoritmos lentos (PBKDF2, bcrypt, Argon2) servem para senhas escolhidas por humanos e
  aqui só acrescentariam latência. Todas as APIs usadas estão em
  `System.Security.Cryptography`, sem dependência nova.
- **Alternatives considered**: PBKDF2/bcrypt/Argon2 (latência desnecessária; bcrypt e Argon2
  exigiriam pacote externo); guardar a chave cifrada (permitiria recuperá-la, o que FR-022
  proíbe); confiar apenas na probabilidade de não haver colisão (não garante FR-020 sob
  concorrência); HMAC com segredo do servidor (exige gerenciar mais um segredo e não traz
  ganho com chaves de 256 bits).

## R-06 — Estilo de API: controllers em vez de Minimal API

- **Decision**: ASP.NET Core com controllers (`[ApiController]`), um controller por recurso:
  `LinksController` (criação e estatísticas, em `/api/links`) e `RedirectController`
  (`GET /{code}`).
- **Rationale**: a constituição (princípio I) exige controllers e não Minimal APIs, e a
  governança define que o plano muda quando contradiz a constituição. "Um arquivo de
  endpoints por recurso" é preservado como "um controller por recurso". O redirecionamento
  fica em controller próprio porque sua rota está na raiz e não sob `/api`.
- **Alternatives considered**: Minimal API, como no plano original (viola o princípio I).

## R-07 — Onde ficam as regras de negócio

- **Decision**: o `LinkService` (camada de aplicação) é dono de **todas** as regras: RN-01 a
  RN-06, expiração (FR-006, FR-011, FR-015), formato e tamanho da URL (FR-010), formato do
  código (FR-003, FR-013) e regras da chave (FR-020 a FR-023). As regras puras ficam em
  classes de domínio sem HTTP nem EF, chamadas pelo serviço:
  - `LinkCodeRules`;
  - `TargetUrlRules`;
  - `ExpirationRules`;
  - `CodeGenerator`;
  - `ManagementKeyHasher`.

  Os controllers só fazem binding, chamam o serviço e traduzem o resultado em status HTTP.
- **Rationale**: o plano original atribuía ao serviço só RN-01 a RN-04, o que deixaria RN-05
  (formato do código) e RN-06 (tentativas de geração) sem dono ou no endpoint. O princípio
  II proíbe regra de negócio em endpoint, e o princípio IV exige teste unitário de cada
  regra, o que fica mais fácil sem HTTP.
- **Alternatives considered**: validação por DataAnnotations/FluentValidation no DTO
  (colocaria regra de negócio na borda HTTP; FluentValidation seria dependência nova sem
  necessidade).

## R-08 — Contrato de erros e mensagens pt-BR

- **Decision**:
  - `AddProblemDetails()` é usado para todas as respostas de erro.
  - Erros de validação do serviço viram **400** com `ValidationProblemDetails`, com
    mensagens pt-BR por campo (FR-017, princípio V).
  - Códigos de rota com formato inválido são erros de validação e respondem 400 com
    `ValidationProblemDetails` e erro no campo `code`, tanto no redirecionamento quanto nas
    estatísticas.
  - O `InvalidModelStateResponseFactory` do `[ApiController]` é substituído para que JSON
    malformado ou tipo incompatível também devolva 400 com mensagem pt-BR (o padrão do
    framework é em inglês).
  - Os campos do request são strings; o serviço converte e valida (a data, por exemplo),
    para que toda mensagem venha do domínio, em pt-BR.
  - Demais erros:
    - 404 para código de formato válido, mas sem link correspondente;
    - 410 para link expirado;
    - 401 para estatísticas sem autorização;
    - 409 para código personalizado em uso;
    - 503 quando as 5 tentativas de código automático colidem.
- **Rationale**: o princípio V exige 400 com `ProblemDetails` e mensagens pt-BR; os status
  diferentes de 400 distinguem situações que não são entrada inválida. O 503 indica falha
  temporária do serviço: uma nova tentativa provavelmente terá sucesso.
- **Alternatives considered**: 400 também para código em uso (mistura "entrada inválida"
  com "conflito de estado", o que dificulta o tratamento no cliente); 409 para a exaustão de
  tentativas (o cliente não causou o conflito e não consegue resolvê-lo).

## R-09 — Expiração como data de calendário

- **Decision**: o campo `expiresAtUtc` da criação recebe uma data `yyyy-MM-dd` (sem hora).
  Datas anteriores a hoje (UTC) são recusadas (FR-015), e hoje é aceito. O valor gravado em
  `ExpiresAtUtc` é o instante em que o link deixa de valer: `data + 1 dia, 00:00:00Z`. O link
  é válido enquanto `agora < ExpiresAtUtc`. O relógio vem de `TimeProvider` (BCL), substituído
  nos testes por um relógio fixo escrito à mão.
- **Rationale**: FR-006 define que o link vale até o fim do dia informado, em UTC. Gravar o
  limite exclusivo deixa a comparação simples e sem ambiguidade de milissegundos. O nome
  `expiresAtUtc` do contrato original foi mantido. `TimeProvider` permite testar 23:59:59 e
  00:00:00 sem esperar o tempo real e sem pacote extra.
- **Alternatives considered**: aceitar data e hora (contraria FR-006);
  `Microsoft.Extensions.TimeProvider.Testing` (dependência nova; um relógio falso tem poucas
  linhas); `DateTimeOffset` no SQLite (o provider do SQLite não traduz comparações e ordenação
  de `DateTimeOffset`, por isso o modelo usa `DateTime` em UTC).

## R-10 — Redirecionamento e cache

- **Decision**: `GET /{code}` responde `302 Found` com `Location` = destino e
  `Cache-Control: no-store`. A rota aceita qualquer segmento; um código com formato inválido
  responde 400 com `ValidationProblemDetails` em pt-BR, sem consultar o banco. Um código
  válido em formato, mas inexistente, responde 404.
- **Rationale**: FR-005 exige redirecionamento não-permanente e não-cacheável para que todo
  acesso seja contado. Manter a validação no serviço permite que códigos malformados sigam
  o contrato constitucional de 400 com `ProblemDetails`; códigos bem formados sem registro
  continuam sendo 404. A rota de um segmento não conflita com `/api/links/...`.
- **Alternatives considered**: responder 404 para código malformado (viola o princípio V);
  301/308 (o navegador guarda em cache e para de contar acessos); 307 (também válido, mas o
  302 foi o que o plano pediu).

## R-11 — Estatísticas e 401

- **Decision**: `GET /api/links/{code}/stats` lê `X-Management-Key`. Com a chave correta,
  responde 200 `{ "accessCount": n }`. Um código com formato inválido responde 400 com
  `ValidationProblemDetails` antes da verificação da chave. Para códigos de formato válido,
  os demais casos recebem a mesma resposta 401 com `ProblemDetails`
  "Chave de gerenciamento ausente ou inválida.":
  - sem a chave;
  - chave vazia;
  - chave incorreta;
  - código inexistente.

  As respostas 401 também incluem `WWW-Authenticate: ManagementKey` (exigido pelo HTTP para
  401) e todas as respostas usam `Cache-Control: no-store`.
- **Rationale**: atende FR-007, FR-023 e SC-008 sem revelar se o código existe nem qual foi
  a falha.
- **Alternatives considered**: 403 para chave incorreta e 404 para código inexistente
  (contraria FR-023); 401 para formato inválido (contraria o princípio V); chave em query
  string (vazaria em logs e no histórico, contrariando FR-022).

## R-12 — Chave fora de logs (FR-022, SC-009)

- **Decision**:
  - O `HttpLoggingMiddleware` não é habilitado e nenhum código registra headers ou o corpo
    da resposta de criação.
  - A resposta de criação recebe `Cache-Control: no-store`.
  - Um teste de integração captura todos os logs, com um `ILoggerProvider` próprio de
    teste, e verifica que a chave não aparece.
- **Rationale**: a garantia precisa ser verificável (SC-009) e não depender de convenção.
- **Alternatives considered**: redação de logs com `Microsoft.Extensions.Compliance.Redaction`
  (dependência nova sem necessidade, já que nada registra a chave).

## R-13 — Link curto devolvido na criação

- **Decision**: `shortUrl = {PublicBaseUrl}/{code}`. `PublicBaseUrl` vem da configuração
  `ShortLinks:PublicBaseUrl`; quando ausente, usa scheme e host da requisição.
- **Rationale**: FR-001 exige que o link curto use o endereço base do serviço. A
  configuração cobre a publicação atrás de proxy, e o fallback evita configuração
  obrigatória em desenvolvimento e testes.
- **Alternatives considered**: devolver só o código (contraria FR-001).

## R-14 — Dependências NuGet (princípio III)

| Pacote | Projeto | Justificativa | Alternativa rejeitada |
|--------|---------|---------------|-----------------------|
| `Microsoft.EntityFrameworkCore.Sqlite` 10.0.x | Api | Exigido pelo princípio I (SQLite via EF Core). | Nenhuma: é a stack obrigatória. |
| `Microsoft.EntityFrameworkCore.Design` 10.0.x (`PrivateAssets=all`) | Api | Necessário para gerar migrations (R-01); não entra no runtime. | `EnsureCreated` não permite evoluir o schema. |
| `dotnet-ef` 10.0.x (ferramenta local, `.config/dotnet-tools.json`) | — | CLI para criar migrations, com a versão fixada no repositório. | Ferramenta global (sem versão fixada, varia por máquina). |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | Tests | Exigidos pelo princípio I (testes com xUnit) e para executar com `dotnet test`. | Nenhuma. |
| `Microsoft.AspNetCore.Mvc.Testing` 10.0.x | Tests | `WebApplicationFactory`, citado no princípio IV para testes de integração pelo pipeline HTTP real. | Subir o servidor em porta real (lento e instável). |

**Pacotes que não serão adicionados**:
- `coverlet.collector`, que vem no template `xunit`, será removido por não haver requisito
  de cobertura.
- `Microsoft.AspNetCore.OpenApi`, que vem no template `webapi`, será removido porque a spec
  não pede documentação OpenAPI em runtime; o contrato está em `contracts/`.
- Também não entram Moq, FluentAssertions, FluentValidation e o pacote de teste do
  `TimeProvider`: os fakes são escritos à mão.
