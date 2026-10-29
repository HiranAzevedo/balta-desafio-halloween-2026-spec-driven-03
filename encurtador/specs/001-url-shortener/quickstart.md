# Quickstart: validar o Encurtador de links

**Feature**: [spec.md](./spec.md) | **Contrato**: [contracts/links-api.md](./contracts/links-api.md)

Guia para provar, de ponta a ponta, que a feature atende à spec. Os detalhes de campos e
status estão no [contrato](./contracts/links-api.md) e no [modelo de dados](./data-model.md).

## Pré-requisitos

- .NET SDK 10 (`dotnet --list-sdks` deve listar `10.0.x`).
- `curl` (no Windows, use `curl.exe` para não acionar o alias do PowerShell).
- Comandos executados a partir de `encurtador/`.

A configuração padrão usa `ConnectionStrings:Links = Data Source=links.db;Default Timeout=30`;
o schema é migrado na inicialização e o SQLite opera em WAL. A `ApiFactory` dos testes usa
um arquivo SQLite temporário exclusivo por teste; o cenário de reinício reabre o mesmo arquivo.

## 1. Build e testes automatizados

```powershell
dotnet tool restore
dotnet build Encurtador.slnx
dotnet test Encurtador.slnx
```

**Esperado**: build sem erros e todos os testes xUnit passando. A suíte deve cobrir:

| Área | Tipo | Requisitos |
|------|------|------------|
| Formato de código, gerador, URL, expiração, hash da chave, `LinkService` (incl. 5 tentativas) | Unitário | FR-002, FR-003, FR-006, FR-009, FR-010, FR-013 a FR-015, FR-020, FR-022 |
| Criar → redirecionar (caminho feliz) | Integração | FR-001, FR-004, SC-002 |
| Erros 400/409 em `ProblemDetails` pt-BR, incluindo código malformado nas rotas | Integração | FR-016, FR-017, SC-006, princípio V |
| 404 para código bem formado inexistente e 410 expirado, sem incremento | Integração | FR-011, FR-016 |
| Estatísticas: 200 com a chave, 400 para formato inválido e 401 uniforme nos demais casos | Integração | FR-007, FR-016, FR-023, SC-008 |
| 100 acessos simultâneos → total exatamente 100 | Integração | FR-005, SC-003, SC-007 |
| Criação em até 5 s com host iniciado | Integração de desempenho | SC-001 |
| 100 redirecionamentos sequenciais, cada um em menos de 1 s | Integração de desempenho | SC-002 |
| Reinício: novo host sobre o mesmo arquivo SQLite | Integração | FR-008, SC-005 |
| Chave ausente de logs e do banco | Integração | FR-022, SC-009 |

O caminho feliz de cada endpoint é exercitado pelo pipeline HTTP real: `POST /api/links`,
`GET /{code}` e `GET /api/links/{code}/stats`. JSON malformado também deve retornar 400 com
`ValidationProblemDetails` em pt-BR.

## 2. Executar a aplicação

```powershell
dotnet run --project src/Encurtador.Api --urls http://localhost:5080
```

**Esperado**: a aplicação sobe e cria ou atualiza o arquivo SQLite configurado em
`ConnectionStrings:Links` (migrations aplicadas na inicialização).

## 3. Cenários manuais

Execute em outro terminal, em ordem. Os corpos JSON são gravados em arquivo e enviados com
`--data-binary "@arquivo"`, o que evita diferenças de escape de aspas entre versões do
PowerShell.

### 3.1 Criar link automático (US1)

```powershell
'{"targetUrl":"https://example.com/artigo?id=123"}' | Set-Content -NoNewline auto.json
curl.exe -i -X POST http://localhost:5080/api/links -H "Content-Type: application/json" `
  --data-binary "@auto.json"
```

**Esperado**:
- Resposta `201`, com `Cache-Control: no-store`.
- O corpo traz um `code` de 7 caracteres de `A-Z1-9`, sem `I` nem `O`.
- O corpo traz um `managementKey` de 43 caracteres; guarde-o como `KEY`.
- O corpo traz `shortUrl = http://localhost:5080/{code}`.

### 3.2 Redirecionar e contar (US1, US2)

```powershell
curl.exe -i http://localhost:5080/{code}
curl.exe -i http://localhost:5080/{code}
curl.exe -i http://localhost:5080/api/links/{code}/stats -H "X-Management-Key: KEY"
```

**Esperado**:
- Os dois primeiros comandos respondem `302`, com `Location: https://example.com/artigo?id=123`
  e `Cache-Control: no-store`.
- As estatísticas respondem `200` com `{"accessCount":2}`.

### 3.3 Estatísticas sem autorização (US2)

```powershell
curl.exe -i http://localhost:5080/api/links/{code}/stats
curl.exe -i http://localhost:5080/api/links/{code}/stats -H "X-Management-Key: errada"
curl.exe -i http://localhost:5080/api/links/NAOEXISTE/stats -H "X-Management-Key: KEY"
```

**Esperado**: os três respondem `401` com o mesmo `ProblemDetails` em pt-BR e o
cabeçalho `WWW-Authenticate: ManagementKey`, sem revelar o total.

Código malformado não depende de chave e é erro de validação:

```powershell
curl.exe -i http://localhost:5080/api/links/codigo-invalido/stats
```

**Esperado**: `400` com `ValidationProblemDetails` em pt-BR no campo `code`.

### 3.4 Código personalizado (US3)

```powershell
'{"targetUrl":"https://example.com","code":"HALLOWEEN26"}' | Set-Content -NoNewline custom.json
'{"targetUrl":"https://example.com","code":"halloween"}' | Set-Content -NoNewline lower.json
curl.exe -i -X POST http://localhost:5080/api/links -H "Content-Type: application/json" `
  --data-binary "@custom.json"
# Repita o comando acima uma vez (esperado: 409) e depois envie o código em minúsculas:
curl.exe -i -X POST http://localhost:5080/api/links -H "Content-Type: application/json" `
  --data-binary "@lower.json"
```

**Esperado**:
- A 1ª criação responde `201` com `code = HALLOWEEN26`.
- A repetição responde `409` "O código informado já está em uso.".
- O código em minúsculas responde `400` com erro em `errors.code`.

### 3.5 Validações (FR-010, FR-015, FR-017)

| Request | Esperado |
|---------|----------|
| `targetUrl` = `ftp://example.com` | `400`, `errors.targetUrl` em pt-BR |
| `targetUrl` com 2049 caracteres | `400`, `errors.targetUrl` |
| `expiresAtUtc` = ontem (UTC) | `400`, `errors.expiresAtUtc` |
| `expiresAtUtc` = hoje (UTC) | `201` (vale até 23:59:59Z de hoje) |
| `targetUrl` = `http://localhost:9/fora-do-ar` | `201` (o destino não é acessado, FR-010) |
| JSON malformado | `400`, `ValidationProblemDetails` em pt-BR |

### 3.6 Inexistente (FR-016)

```powershell
curl.exe -i http://localhost:5080/NAOEXISTE
```

**Esperado**: `404` com `ProblemDetails` "Link não encontrado.".

Para um segmento de formato inválido:

```powershell
curl.exe -i http://localhost:5080/codigo-invalido
```

**Esperado**: `400` com `ValidationProblemDetails` em pt-BR no campo `code`.

As medições de SC-001 e SC-002 são automatizadas: o host e o banco temporário são
preparados antes do cronômetro; SC-002 não segue o header `Location` nem carrega o destino.

### 3.7 Persistência após reinício (FR-008)

1. Interrompa a aplicação (Ctrl+C) e execute o passo 2 de novo.
2. Repita o 3.2 com o mesmo código e a mesma chave.

**Esperado**: o redirecionamento continua funcionando e o total parte do valor anterior
(`3` após mais um acesso).

### 3.8 Expiração (US4)

A virada de dia é coberta pelos testes automatizados com relógio controlado (23:59:59Z →
`302`; 00:00:00Z do dia seguinte → `410` sem incremento). Não é necessário esperar
manualmente.

## Limpeza

Apague o arquivo SQLite local configurado em `ConnectionStrings:Links` para recomeçar do
zero, e remova os corpos de requisição temporários:

```powershell
Remove-Item auto.json, custom.json, lower.json -ErrorAction SilentlyContinue
```
