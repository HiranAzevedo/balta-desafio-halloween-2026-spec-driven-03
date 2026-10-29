# Data Model: Encurtador de links

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

## Entidade `Link` (agregado único, tabela `Links`)

| Campo | Tipo (C#) | Coluna SQLite | Nulo | Regras |
|-------|-----------|---------------|------|--------|
| `Id` | `long` | `INTEGER PRIMARY KEY` | não | Gerado pelo banco. Uso interno; nunca é exposto. |
| `Code` | `string` | `TEXT`, **índice único** | não | 4 a 20 caracteres de `A-Z1-9` (FR-003, FR-013); os automáticos têm 7 caracteres do alfabeto sem `I` e `O` (FR-002, R-03). Imutável e nunca reutilizado (FR-009). |
| `TargetUrl` | `string` | `TEXT` (máx. 2048) | não | Absoluta, http/https, ≤ 2048 caracteres (FR-010). Imutável (FR-018). |
| `IsCustomCode` | `bool` | `INTEGER` | não | `true` quando o criador informou o código. Atende ao atributo "indicação de código automático ou personalizado" da spec. |
| `CreatedAtUtc` | `DateTime` (UTC) | `TEXT` | não | Instante da criação, via `TimeProvider`. |
| `ExpiresAtUtc` | `DateTime?` (UTC) | `TEXT` | sim | Instante **exclusivo** em que o link deixa de valer: `data informada + 1 dia, 00:00:00Z` (FR-006, R-09). `null` = não expira. |
| `AccessCount` | `long` | `INTEGER`, default 0 | não | ≥ 0. Alterado só por incremento atômico (FR-005, FR-012, R-02). |
| `ManagementKeyHash` | `byte[]` (32) | `BLOB`, **índice único** | não | SHA-256 da chave de gerenciamento (FR-020, FR-022, R-05). Garante que chaves de links distintos não se repitam; a chave em texto nunca é persistida. |

O tipo de `AccessCount` é `long` porque contagens altas de acessos automatizados podem
ultrapassar `int` ao longo da vida do link. Datas usam `DateTime` com `Kind = Utc`
(veja R-09 sobre `DateTimeOffset` no SQLite).

## Entidades não persistidas

- **Chave de gerenciamento**: 32 bytes aleatórios em Base64Url (43 caracteres). Existe em
  texto somente na resposta de criação (FR-021); depois disso só o hash é persistido.
- **Estatística de acessos**: projeção `{ accessCount }` de um `Link`, devolvida só com a
  chave correta (FR-007, FR-023).

## Regras de validação na criação

Regras executadas pelo `LinkService`, com mensagens pt-BR em 400 `ValidationProblemDetails`
indexado pelo nome do campo:

| Campo | Regra | Mensagem (pt-BR) |
|-------|-------|------------------|
| `targetUrl` | Obrigatória | "A URL de destino é obrigatória." |
| `targetUrl` | ≤ 2048 caracteres | "A URL de destino deve ter no máximo 2048 caracteres." |
| `targetUrl` | Absoluta e bem formada, esquema http ou https | "A URL de destino deve ser um endereço http ou https válido." |
| `code` (opcional) | `^[A-Z1-9]{4,20}$` | "O código deve ter de 4 a 20 caracteres, usando apenas letras maiúsculas de A a Z e números de 1 a 9." |
| `expiresAtUtc` (opcional) | Formato `yyyy-MM-dd` | "A data de expiração deve estar no formato AAAA-MM-DD." |
| `expiresAtUtc` (opcional) | ≥ data atual em UTC | "A data de expiração não pode estar no passado." |

As recusas por estado não são erros de validação:
- 409 para código personalizado em uso: "O código informado já está em uso.";
- 503 quando as 5 tentativas de código automático colidem: "Não foi possível gerar um
  código disponível. Tente novamente."

## Estados e transições

```text
            criação válida
  (nenhum) ───────────────► ATIVO ── agora ≥ ExpiresAtUtc ──► EXPIRADO
                              │                                  │
                acesso: +1 no contador,              acesso: 410, contador inalterado
                302 para o destino                   (FR-011); stats continuam
                                                      disponíveis com a chave
```

- Sem `ExpiresAtUtc`, o link fica ATIVO para sempre.
- A passagem para EXPIRADO é calculada na leitura (`agora` vs. `ExpiresAtUtc`), sem job nem
  coluna de status.
- Não existe exclusão nem edição nesta feature. O registro permanece e mantém o código
  reservado (FR-009, FR-018).

## Operações do repositório (`Application/Abstractions/ILinkRepository`)

A interface fica em `Application/Abstractions/ILinkRepository.cs`; sua implementação
`Infrastructure/LinkRepository.cs` acessa os dados. Há um repositório por agregado, e as
regras ficam no `LinkService`.

| Operação | Comportamento |
|----------|---------------|
| `CodeExistsAsync(code)` | Verificação prévia de unicidade (R-04). |
| `TryAddAsync(link)` | Insere. Violações dos índices únicos em `Code` e `ManagementKeyHash` viram, respectivamente, `CodeConflict` e `ManagementKeyConflict`, sem exceção (R-04, R-05). |
| `TryRegisterAccessAsync(code, nowUtc)` | `UPDATE` atômico +1, filtrado por validade. Retorna o número de linhas afetadas (R-02). |
| `FindByCodeAsync(code)` | Leitura sem tracking, para obter o destino, diferenciar 404 de 410 e obter os dados das estatísticas. |

O `LinkService` trata `ManagementKeyConflict` gerando outra chave e repetindo a inserção.
Esse retry não consome o limite de cinco tentativas de códigos automáticos (FR-014).
