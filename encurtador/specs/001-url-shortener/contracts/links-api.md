# Contrato HTTP: Encurtador de links

**Feature**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

Regras gerais:

- JSON em `camelCase`, `Content-Type: application/json`.
- Toda resposta de erro usa `application/problem+json` (`ProblemDetails`, RFC 9457) com
  `title`/`detail` em pt-BR. Erros de validação usam `ValidationProblemDetails`, com o
  dicionário `errors` indexado pelo nome do campo.
- Nenhum endpoint exige login. Só as estatísticas exigem a chave de gerenciamento.

---

## 1. Criar link — `POST /api/links`

Atende FR-001 a FR-003, FR-006, FR-009, FR-010, FR-013 a FR-015, FR-019 a FR-022 e FR-024.

### Request

```json
{
  "targetUrl": "https://example.com/um/caminho/bem/longo?com=parametros",
  "code": "HALLOWEEN26",
  "expiresAtUtc": "2026-10-31"
}
```

| Campo | Tipo | Obrigatório | Observações |
|-------|------|-------------|-------------|
| `targetUrl` | string | sim | http/https absoluta, ≤ 2048 caracteres. O destino nunca é acessado (FR-010). |
| `code` | string | não | `^[A-Z1-9]{4,20}$`. Sem conversão de maiúsculas e minúsculas. Omitido ou `null` = código automático de 7 caracteres. |
| `expiresAtUtc` | string (`yyyy-MM-dd`) | não | Data de calendário em UTC; o link vale até 23:59:59.999Z desse dia. Omitido ou `null` = não expira. |

### Responses

| Status | Quando | Corpo |
|--------|--------|-------|
| `201 Created` | Link criado | `CreateLinkResponse` (abaixo). Headers: `Location: /api/links/{code}/stats`, `Cache-Control: no-store`. |
| `400 Bad Request` | URL, código ou data inválidos, ou JSON malformado | `ValidationProblemDetails`, com mensagens pt-BR por campo (veja [data-model.md](../data-model.md#regras-de-validação-na-criação)). |
| `409 Conflict` | Código personalizado já usado por qualquer link, ativo ou expirado | `ProblemDetails`: "O código informado já está em uso." |
| `503 Service Unavailable` | As 5 tentativas de código automático colidiram (FR-014) | `ProblemDetails`: "Não foi possível gerar um código disponível. Tente novamente." |

```json
{
  "shortUrl": "https://encurta.example/HALLOWEEN26",
  "code": "HALLOWEEN26",
  "managementKey": "q3Vt9l2Q7xg4bJm3o2c8Q5yJr5ZxkC1oV4F2m8Q5b1E",
  "expiresAtUtc": "2026-10-31"
}
```

- `managementKey` só aparece nesta resposta e nunca é devolvido em outro lugar (FR-021).
- `expiresAtUtc` repete a data informada, ou `null`.

---

## 2. Acessar link — `GET /{code}`

Atende FR-004, FR-005, FR-011, FR-012, FR-016 e FR-024. Não exige chave.

| Status | Quando | Headers / corpo |
|--------|--------|-----------------|
| `400 Bad Request` | O código tem formato inválido | `ValidationProblemDetails` em pt-BR, com erro no campo `code`. |
| `302 Found` | O link existe e não expirou. O contador é incrementado em 1. | `Location: {targetUrl}`, `Cache-Control: no-store`. Sem corpo. |
| `404 Not Found` | O código tem formato válido, mas não existe | `ProblemDetails`: "Link não encontrado." Contador inalterado. |
| `410 Gone` | O link existe, mas `agora ≥ ExpiresAtUtc` | `ProblemDetails`: "Este link expirou." Contador inalterado. |

---

## 3. Consultar estatísticas — `GET /api/links/{code}/stats`

Atende FR-007, FR-023 e FR-024.

### Request headers

| Header | Obrigatório | Valor |
|--------|-------------|-------|
| `X-Management-Key` | sim | A chave devolvida na criação. |

### Responses

| Status | Quando | Corpo |
|--------|--------|-------|
| `400 Bad Request` | O código tem formato inválido | `ValidationProblemDetails` em pt-BR, com erro no campo `code`; independe da chave. |
| `200 OK` | O código existe e a chave confere. Link expirado também responde. | `{ "accessCount": 42 }`, somente esse campo. Header `Cache-Control: no-store`. |
| `401 Unauthorized` | Para código de formato válido: chave ausente, vazia ou incorreta, ou código inexistente | `ProblemDetails`: "Chave de gerenciamento ausente ou inválida." Headers `WWW-Authenticate: ManagementKey` e `Cache-Control: no-store`. A resposta é a mesma em todos esses casos. |

Esta consulta nunca incrementa o contador.
