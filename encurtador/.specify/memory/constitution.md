<!--
Sync Impact Report
- Version change: (template, sem versão) → 1.0.0
- Princípios definidos (novos):
  - [PRINCIPLE_1_NAME] → I. Stack Padronizada
  - [PRINCIPLE_2_NAME] → II. Separação de Camadas
  - [PRINCIPLE_3_NAME] → III. Dependências Justificadas
  - [PRINCIPLE_4_NAME] → IV. Testes Obrigatórios
  - [PRINCIPLE_5_NAME] → V. Contrato de Erros de Validação
- Seções adicionadas: Convenções; Fluxo de Desenvolvimento e Quality Gates; Governance
- Seções removidas: nenhuma
- Templates: nenhum template versionado foi alterado (lidos em tempo de execução)
- TODOs pendentes: nenhum
-->
# Encurtador Constitution

## Core Principles

### I. Stack Padronizada

- A aplicação MUST ser construída em .NET 10 com C#, expondo HTTP via ASP.NET Core
  Controller API (controllers; não Minimal APIs).
- A persistência MUST usar SQLite via Entity Framework Core.
- Os testes MUST ser escritos com xUnit.

Racional: uma stack única e conhecida reduz decisões repetidas e mantém specs, planos e
tarefas comparáveis entre features.

### II. Separação de Camadas

- Controllers/endpoints MUST NOT acessar o banco de dados (DbContext, repositórios ou SQL)
  diretamente; todo acesso a dados MUST passar por um serviço de aplicação.
- Regras de negócio MUST NOT residir em controllers, endpoints ou handlers HTTP; essas camadas
  apenas traduzem HTTP ↔ chamadas ao serviço de aplicação (binding, status code, resposta).
- Regras de negócio MUST viver em serviços de aplicação ou no domínio, testáveis sem HTTP.

Racional: isolar a regra de negócio do transporte HTTP permite testá-la unitariamente e
evolui-la sem acoplamento à infraestrutura.

### III. Dependências Justificadas

- Pacotes NuGet ou dependências externas novas MUST NOT ser adicionados sem justificativa
  escrita no `plan.md` da feature (motivo, alternativas consideradas e por que a stack atual
  não basta).
- Dependências sem justificativa registrada MUST ser removidas ou justificadas antes do merge.

Racional: cada dependência adiciona superfície de manutenção, segurança e acoplamento.

### IV. Testes Obrigatórios

- Toda regra de negócio MUST ter ao menos um teste unitário (xUnit) cobrindo seu
  comportamento.
- Todo endpoint MUST ter ao menos um teste de integração do caminho feliz, exercitando o
  pipeline HTTP real (ex.: `WebApplicationFactory`) até a persistência.
- Uma tarefa que introduz regra de negócio ou endpoint MUST NOT ser considerada concluída
  sem os testes correspondentes passando.

Racional: testes são a evidência verificável de que a spec foi atendida.

### V. Contrato de Erros de Validação

- Todo erro de validação de entrada MUST retornar HTTP 400 com corpo `ProblemDetails`
  (RFC 9457 / `ValidationProblemDetails` quando houver erros por campo).
- Mensagens de erro retornadas ao cliente MUST estar em português do Brasil (pt-BR).
- Cada endpoint com validação MUST ter teste cobrindo a resposta 400 em formato
  `ProblemDetails`.

Racional: um contrato de erro único e previsível simplifica consumidores e testes.

## Convenções

- Nomes de classes, arquivos, namespaces, métodos e demais identificadores de código MUST
  estar em inglês.
- Mensagens de erro voltadas ao usuário/cliente MUST estar em pt-BR.
- Mensagens de commit MUST seguir o formato `Tipo(escopo): descrição`, incluindo o
  identificador do requisito atendido (ex.: `feat(links): cria URL encurtada FR-001`).
  Tipos aceitos: `feat`, `fix`, `test`, `refactor`, `docs`, `chore`.

## Fluxo de Desenvolvimento e Quality Gates

- Todo `plan.md` MUST conter uma verificação explícita de conformidade com esta
  constituição (Constitution Check) antes da geração de tarefas.
- Antes do merge, MUST ser verificado que: (a) a solução compila; (b) todos os testes xUnit
  passam; (c) os princípios II, IV e V foram respeitados; (d) toda dependência nova possui
  justificativa no plano.
- Revisões de código MUST rejeitar acesso a dados ou regra de negócio em controllers.

## Governance

- Esta constituição prevalece sobre specs, planos, tarefas e demais práticas do projeto.
- Se um plano contradiz a constituição, o plano MUST ser alterado; a constituição não é
  ajustada para acomodar um plano.
- Alterações nesta constituição MUST ser feitas em commit separado, contendo apenas a
  emenda (sem código de feature), com o tipo `docs(constitution)` e, como identificador
  do requisito, `CONST-<versão>` (ex.: `docs(constitution): ratifica constituição
  CONST-1.0.0`).
- Versionamento semântico: MAJOR para remoção ou redefinição incompatível de princípios;
  MINOR para novo princípio/seção ou expansão material; PATCH para esclarecimentos e
  correções de redação.
- Conformidade MUST ser verificada no Constitution Check de cada plano e em toda revisão
  de código.

**Version**: 1.0.0 | **Ratified**: 2026-10-29 | **Last Amended**: 2026-10-29
