# Implementation Plan: Encurtador de links

**Branch**: `main` (sem branch dedicada; diretório da feature `001-url-shortener`) | **Date**: 2026-10-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-url-shortener/spec.md`, mais o plano técnico
fornecido pelo solicitante (decisões D-01 a D-05).

## Summary

API HTTP em .NET 10 que:
- recebe uma URL longa e devolve um link curto, com código de 7 caracteres gerado
  automaticamente ou código personalizado de 4 a 20 caracteres, mais uma chave de
  gerenciamento exibida uma única vez;
- redireciona `GET /{code}` com 302 não-cacheável, contando cada acesso;
- expõe o total de acessos só para quem apresentar a chave.

A abordagem técnica:
- **Controllers**: só traduzem HTTP.
- **`LinkService`**: concentra todas as regras de negócio (RN-01 a RN-06, expiração, chave).
- **`LinkRepository`**: acessa o SQLite via EF Core.
- **Concorrência**: o contador cresce por `UPDATE` atômico (D-02) e a unicidade do código é
  garantida por índice único (D-04). O hash da chave também tem índice único para garantir
  FR-020; colisão gera uma nova chave e retry sem consumir tentativas de código.

Os detalhes estão em [research.md](./research.md).

### Ajustes ao plano técnico fornecido

O plano fornecido contradizia a constituição ou a spec nos pontos abaixo. Pela governança
da constituição, quem muda é o plano.

| Item fornecido | Ajuste | Motivo |
|----------------|--------|--------|
| "API HTTP em Minimal API, um arquivo de endpoints por recurso" | Controllers, um por recurso (`LinksController`, `RedirectController`) | Princípio I exige Controller API, não Minimal API (R-06). |
| "LinkService, dona das regras RN-01 a RN-04" | `LinkService` é dono de RN-01 a **RN-06** e também das regras de expiração e da chave | Princípio II: nenhuma regra pode ficar no endpoint (R-07). |
| D-03 "alfabeto sem caracteres ambíguos" | Vale só para códigos **automáticos** (A-Z sem `I`/`O` + 1-9); os personalizados aceitam todo `A-Z1-9` | FR-003 permite qualquer A-Z e 1-9 no código personalizado (R-03). |
| D-05 "chave de gerenciamento da DC-03" | Ligado a FR-020 a FR-023 (DC-03 não existe na spec); hash SHA-256 + comparação em tempo constante | Rastreabilidade com a spec (R-05). |
| FR-020 exige chaves distintas | Índice único em `ManagementKeyHash`; o serviço gera uma nova chave e repete a inserção se houver colisão | Garante unicidade sob concorrência, não apenas por probabilidade (R-05). |
| Contrato `GET /api/links/{code}/stats` "exige header" | Header `X-Management-Key`; 401 único para chave ausente/incorreta e código inexistente | FR-023 (R-11). |
| Contratos sem erros de criação | Adicionados 400 (`ValidationProblemDetails`), 409 (código em uso) e 503 (5 colisões) | Princípio V, FR-014, FR-017 (R-08). |
| `expiresAtUtc` sem formato | Data `yyyy-MM-dd`; grava o limite exclusivo `data + 1 dia 00:00Z` | FR-006 (R-09). |
| Modelo de dados | Adicionado `IsCustomCode` | Atributo "automático ou personalizado" da entidade na spec. |
| Códigos inválidos nas rotas | 400 `ValidationProblemDetails` em pt-BR; código válido sem registro continua 404 no redirecionamento e 401 uniforme nas estatísticas | Princípio V e FR-016/FR-023 (R-08, R-10, R-11). |

## Technical Context

- **Language/Version**: C# 14 / .NET 10 (SDK 10.0.403 disponível na máquina).
- **Primary Dependencies**:
  - ASP.NET Core 10 (Controllers);
  - EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`
    apenas em design-time);
  - justificativas no [research.md](./research.md#r-14--dependências-nuget-princípio-iii).
- **Storage**: SQLite em arquivo, modo WAL, schema por migrations aplicadas na
  inicialização. A connection string fica em `ConnectionStrings:Links`.
- **Testing**:
  - xUnit, com testes unitários das regras e testes de integração com
    `WebApplicationFactory`;
  - os testes de integração usam um arquivo SQLite temporário por teste, para exercitar
    persistência e reinício reais.
- **Target Platform**: servidor web multiplataforma (Kestrel), Windows/Linux.
- **Project Type**: web service (API HTTP), sem frontend.
- **Performance Goals**:
  - criação respondida em até 5 s (SC-001);
  - 100 redirecionamentos sequenciais em teste local, cada um em menos de 1 s (SC-002);
  - 100 acessos simultâneos ao mesmo link sem perder contagem (SC-007).
- **Constraints**:
  - o destino nunca é acessado na criação (FR-010);
  - a chave de gerenciamento nunca é persistida em texto nem registrada em logs (FR-022);
  - redirecionamento 302 com `Cache-Control: no-store` (FR-005);
  - mensagens de erro em pt-BR e identificadores de código em inglês.
- **Scale/Scope**:
  - uma tabela (`Links`) e 3 endpoints;
  - sem limite de criação (FR-019);
  - crescimento da tabela sem política de limpeza (risco aceito, fora de escopo).

Não há itens NEEDS CLARIFICATION: todos foram resolvidos em [research.md](./research.md).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio / regra | Verificação | Pré-pesquisa | Pós-design |
|-------------------|-------------|--------------|------------|
| I. Stack Padronizada | .NET 10 + C#, **Controllers** (Minimal API trocada), SQLite via EF Core, xUnit | ✅ após ajuste R-06 | ✅ |
| II. Separação de Camadas | Controllers não usam `DbContext` nem repositório; toda regra fica no `LinkService` e em classes de domínio | ✅ após ajuste R-07 | ✅ ([data-model.md](./data-model.md#operações-do-repositório-applicationabstractionsilinkrepository)) |
| III. Dependências Justificadas | Cada pacote está justificado, com alternativa rejeitada; pacotes de template desnecessários são removidos | ✅ | ✅ (R-14) |
| IV. Testes Obrigatórios | Teste unitário para cada regra e teste de integração do caminho feliz para cada endpoint (os 3) | ✅ | ✅ ([quickstart.md](./quickstart.md#1-build-e-testes-automatizados)) |
| V. Contrato de Erros de Validação | 400 `ValidationProblemDetails` pt-BR para JSON/campos inválidos e códigos de rota malformados; testes nos três endpoints | ✅ | ✅ ([contrato](./contracts/links-api.md), [quickstart](./quickstart.md#1-build-e-testes-automatizados)) |
| Convenções | Classes e arquivos em inglês; mensagens em pt-BR; commits `Tipo(escopo): descrição FR-xxx` | ✅ | ✅ |
| Quality gates | Constitution Check presente; build e testes antes do merge | ✅ | ✅ |

**Resultado**: aprovado. Não há violações a justificar.

## Project Structure

### Documentation (this feature)

```text
specs/001-url-shortener/
├── spec.md
├── plan.md              # Este arquivo
├── research.md          # Fase 0: decisões R-01 a R-14
├── data-model.md        # Fase 1: entidade Link, validações, estados, repositório
├── quickstart.md        # Fase 1: guia de validação ponta a ponta
├── contracts/
│   └── links-api.md     # Fase 1: contrato HTTP dos 3 endpoints
├── checklists/
│   └── requirements.md
└── tasks.md             # Fase 2 (/speckit-tasks; não criado aqui)
```

### Source Code (diretório `encurtador/`)

```text
Encurtador.slnx
.config/
└── dotnet-tools.json            # dotnet-ef fixado (R-14)
src/
└── Encurtador.Api/
    ├── Program.cs               # DI, ProblemDetails, factory de 400 pt-BR, migrations na inicialização
    ├── appsettings.json         # ConnectionStrings:Links, ShortLinks:PublicBaseUrl
    ├── Controllers/
    │   ├── LinksController.cs   # POST /api/links, GET /api/links/{code}/stats
    │   └── RedirectController.cs# GET /{code}
    ├── Contracts/               # CreateLinkRequest, CreateLinkResponse, LinkStatsResponse
    ├── Application/
    │   ├── ILinkService.cs
    │   ├── LinkService.cs       # Regras RN-01 a RN-06, expiração, chave
    │   ├── Abstractions/
    │   │   └── ILinkRepository.cs
    │   └── Results/             # Resultados de criação, acesso e estatísticas
    ├── Domain/
    │   ├── Link.cs
    │   ├── LinkCodeRules.cs     # Formato e alfabetos (FR-002, FR-003, FR-013)
    │   ├── CodeGenerator.cs     # 7 caracteres via RandomNumberGenerator (R-03)
    │   ├── TargetUrlRules.cs    # FR-010
    │   ├── ExpirationRules.cs   # FR-006, FR-015 (R-09)
    │   └── ManagementKeyHasher.cs # Geração, SHA-256, FixedTimeEquals (R-05)
    └── Infrastructure/
        ├── LinksDbContext.cs    # Índices únicos em Code e ManagementKeyHash
        ├── LinkRepository.cs    # UPDATE atômico (D-02), conflitos de código/hash (R-04, R-05)
        └── Migrations/
tests/
└── Encurtador.Tests/
    ├── Unit/                    # Regras de domínio e LinkService (repositório e relógio falsos)
    └── Integration/             # WebApplicationFactory + SQLite temporário
```

**Structure Decision**: usamos um projeto de API, com as camadas separadas por pasta, e um
único projeto de testes, com as pastas `Unit/` e `Integration/`. A separação de camadas
exigida pelo princípio II é garantida pelas dependências entre pastas:
- controllers usam só `Application/`;
- `Application/` usa `Domain/` e declara `ILinkRepository` em `Application/Abstractions/`;
- `Infrastructure/` implementa o repositório.

Projetos separados por camada foram descartados porque não trazem benefício a uma feature
deste tamanho. Ver [research.md](./research.md#r-07--onde-ficam-as-regras-de-negócio).

## Riscos

| Risco | Mitigação |
|-------|-----------|
| Dois criadores pedindo o mesmo código personalizado ao mesmo tempo | Índice único (D-04); o segundo recebe 409. |
| Escritas concorrentes no SQLite (100 acessos simultâneos) | WAL + `UPDATE` atômico + timeout de espera do SQLite (R-02); coberto por teste de integração. |
| Vazamento da chave de gerenciamento | Só o hash é persistido, o HTTP logging não é habilitado, respostas usam `no-store` e um teste verifica os logs (R-12). |
| Crescimento da tabela sem política de limpeza | Aceito e fora de escopo. Não existe exclusão, porque o código nunca pode ser reutilizado (FR-009). |
| Links para destinos internos (`localhost`, IPs privados) | Aceito por decisão do solicitante (FR-010 só valida a sintaxe). |

## Complexity Tracking

Nenhuma violação da constituição; nada a justificar.
