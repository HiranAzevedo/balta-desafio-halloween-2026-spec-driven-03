# Feature Specification: Encurtador de links

**Feature Branch**: `main` (sem branch dedicada)

**Created**: 2026-10-29

**Status**: Draft

**Input**: User description: "Encurtador de links — permitir que uma pessoa transforme uma URL
longa em um link curto, compartilhe esse link e acompanhe quantos acessos ele recebeu."
(descrição completa com RF-01..RF-08, RN-01..RN-06, casos de borda, fora de escopo e
critérios de aceite fornecida pelo usuário; os identificadores originais são mantidos entre
parênteses para rastreabilidade)

## Clarifications

### Session 2026-10-29

- Q: O serviço deve limitar quantos links uma pessoa pode criar sem autenticação? → A: Sem limite de criações.
- Q: Quantos acessos a links válidos o serviço deve conseguir processar ao mesmo tempo sem perder contagens? → A: 100 acessos simultâneos.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Encurtar uma URL e ser redirecionado (Priority: P1)

O criador informa uma URL de destino longa e recebe um link curto com um código de 7
caracteres gerado automaticamente. Qualquer visitante que acessar esse link curto é levado
diretamente ao destino, sem tela intermediária.

**Why this priority**: É o núcleo do produto. Sem criar e redirecionar, nenhuma outra
história tem valor.

**Independent Test**: Criar um link a partir de uma URL válida, acessar o link curto
retornado e verificar que o visitante chega à URL de destino informada.

**Acceptance Scenarios**:

1. **Given** uma URL de destino http(s) válida, **When** o criador solicita o
   encurtamento sem informar código, **Then** o sistema devolve um link curto cujo código tem
   exatamente 7 caracteres, todos em A-Z ou 1-9, junto com a chave de gerenciamento do link.
2. **Given** um link curto existente e válido, **When** um visitante o acessa, **Then** é
   redirecionado diretamente à URL de destino, sem página intermediária.
3. **Given** um código inexistente com formato válido, **When** um visitante o acessa,
   **Then** o sistema responde HTTP 404 que o link não existe e não redireciona.
4. **Given** um código com formato inválido, **When** um visitante o acessa, **Then** o
   sistema responde HTTP 400 com `ValidationProblemDetails` em pt-BR indicando o formato
   inválido no campo `code`.
5. **Given** uma URL malformada, com esquema diferente de http/https ou com mais de 2048
   caracteres, **When** o criador solicita o encurtamento, **Then** a criação é recusada com
   uma mensagem explicando o motivo.
6. **Given** links já criados, **When** a aplicação é reiniciada, **Then** os links continuam
   redirecionando para seus destinos e mantêm seus contadores.

---

### User Story 2 - Acompanhar o total de acessos (Priority: P2)

O criador consulta quantos acessos um link recebeu, apresentando o código e a chave de
gerenciamento secreta recebida na criação. Conhecer somente o código não basta. Cada acesso a
um link válido conta, inclusive repetições e acessos automatizados.

**Why this priority**: Medir cliques é o segundo problema declarado; depende apenas da
existência de links (US1).

**Independent Test**: Criar um link guardando a chave de gerenciamento, acessá-lo N vezes
(incluindo acessos repetidos da mesma origem) e consultar o total com a chave, que deve ser
exatamente N; repetir a consulta sem a chave e com uma chave errada e verificar a recusa.

**Acceptance Scenarios**:

1. **Given** um link válido acessado 3 vezes pela mesma origem, **When** o criador consulta o
   total informando o código e a chave correta, **Then** o sistema informa 3.
2. **Given** um link recém-criado, **When** o criador consulta o total com a chave correta,
   **Then** o sistema informa 0.
3. **Given** um código existente e a chave correta, **When** o criador consulta as
   estatísticas, **Then** a resposta contém somente o total de acessos (nenhum outro dado,
   como destino, chave ou origem dos acessos).
4. **Given** um código existente, **When** alguém consulta as estatísticas sem informar a
   chave, **Then** a consulta é recusada como não autorizada, com mensagem em pt-BR, e
   nenhum total é revelado.
5. **Given** um código existente, **When** alguém consulta as estatísticas com uma chave
   incorreta (inclusive a chave de outro link), **Then** a consulta é recusada como não
   autorizada, com mensagem em pt-BR, e nenhum total é revelado.
6. **Given** um código inexistente com formato válido, **When** alguém consulta o total sem
   chave ou com qualquer chave, **Then** a consulta é recusada como não autorizada, com a
   mesma mensagem em pt-BR dos casos 4 e 5, sem indicar que o link não existe.
7. **Given** um código com formato inválido, **When** alguém consulta estatísticas com ou
   sem chave, **Then** a consulta responde HTTP 400 com `ValidationProblemDetails` em pt-BR
   indicando formato inválido no campo `code`.
8. **Given** um link já criado, **When** qualquer pessoa consulta o link ou suas estatísticas
   depois da criação, **Then** a chave de gerenciamento nunca é devolvida novamente.

---

### User Story 3 - Escolher um código personalizado (Priority: P3)

O criador informa um código próprio e legível (ex.: `HALLOWEEN26`) em vez de receber um
código automático.

**Why this priority**: Melhora a legibilidade do link, mas o produto já entrega valor sem
isso.

**Independent Test**: Criar um link informando um código válido e verificar que o link curto
usa exatamente esse código e redireciona; tentar reutilizá-lo e verificar a recusa.

**Acceptance Scenarios**:

1. **Given** um código de 4 a 20 caracteres usando apenas A-Z e 1-9 e ainda nunca usado,
   **When** o criador cria o link com esse código, **Then** o link curto usa exatamente esse
   código.
2. **Given** um código já usado por outro link (ativo ou expirado), **When** o criador tenta
   criá-lo novamente, **Then** a criação é recusada informando que o código já está em uso.
3. **Given** um código com menos de 4 ou mais de 20 caracteres, ou contendo `0`, letras
   minúsculas, espaços ou caracteres especiais, **When** o criador tenta usá-lo, **Then** a
   criação é recusada explicando a regra de formato.

---

### User Story 4 - Definir data de expiração (Priority: P4)

O criador define uma data de expiração; o link funciona até o fim desse dia em UTC e, depois
disso, deixa de redirecionar e de contar acessos, sem que o código volte a ficar disponível.

**Why this priority**: Recurso complementar de controle de validade; depende de US1.

**Independent Test**: Criar links com expiração hoje e em data futura, acessá-los antes e
depois do limite simulado e verificar redirecionamento, recusa e contador.

**Acceptance Scenarios**:

1. **Given** um link com expiração na data D, **When** é acessado em D às 23:59:59 UTC,
   **Then** redireciona e o contador é incrementado.
2. **Given** um link com expiração na data D, **When** é acessado em D+1 às 00:00:00 UTC ou
   depois, **Then** o sistema responde que o link expirou, não redireciona e o contador não
   muda.
3. **Given** uma data de expiração anterior à data atual em UTC, **When** o criador tenta
   criar o link, **Then** a criação é recusada.
4. **Given** um link expirado, **When** alguém tenta criar um novo link com o mesmo código,
   **Then** a criação é recusada porque o código nunca pode ser reutilizado.

---

### Edge Cases

- Código personalizado já em uso (por link ativo ou expirado): recusar e explicar o motivo.
- Código automático gerado já em uso: gerar outro e tentar novamente, até 5 tentativas no
  total; se todas colidirem, recusar informando que não foi possível gerar um código
  disponível.
- Acesso a código inexistente com formato válido: responder HTTP 404 que o link não existe.
- Acesso a código com formato inválido, tanto no redirecionamento quanto nas estatísticas:
  responder HTTP 400 com `ValidationProblemDetails` em pt-BR no campo `code`.
- Acesso a link expirado: responder que o link expirou, sem redirecionar e sem contar acesso.
- URL de destino com mais de 2048 caracteres: recusar.
- URL de destino sem esquema http/https (ex.: `ftp:`, `javascript:`, `mailto:`) ou malformada:
  recusar.
- URL de destino fora do ar, privada ou que exige autenticação: aceita na criação, pois o
  sistema não acessa o destino; o visitante é redirecionado normalmente.
- Data de expiração no passado: recusar; data de expiração igual à data atual em UTC é
  aceita e o link vale até o fim do dia.
- Código informado em minúsculas: recusar (não há conversão automática para maiúsculas).
- Acessos simultâneos ao mesmo link: todos devem ser contabilizados, sem perda.
- Consulta de total de acessos de link expirado: permitida com a chave correta, retorna o
  total acumulado.
- Consulta de estatísticas sem chave, com chave vazia ou com chave incorreta: recusar como
  não autorizada, com a mesma mensagem em pt-BR para os três casos.
- Chave de outro link: tratada como chave incorreta.
- Chave perdida pelo criador: não há recuperação nem reemissão; as estatísticas daquele link
  deixam de ser consultáveis, mas o link continua redirecionando e contando acessos.
- Consulta de estatísticas para código inexistente com formato válido: sem chave ou com
  qualquer chave, recusar como não autorizada, com a mesma mensagem em pt-BR das chaves
  ausentes ou incorretas.
- Consulta de estatísticas com código de formato inválido: responder HTTP 400 com
  `ValidationProblemDetails` em pt-BR no campo `code`, independentemente da chave.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** (RF-01): O sistema MUST receber uma URL de destino e devolver um link curto
  composto pelo endereço base do serviço seguido do código do link.
- **FR-002** (RF-02): Quando o criador não informar código, o sistema MUST gerar
  automaticamente um código de exatamente 7 caracteres, usando apenas A-Z maiúsculo e os
  dígitos 1-9.
- **FR-003** (RF-03): O criador MUST poder informar um código próprio com 4 a 20 caracteres,
  usando apenas A-Z maiúsculo e os dígitos 1-9, sem espaços.
- **FR-004** (RF-04): Acessar um link curto existente e não expirado MUST redirecionar
  diretamente para a URL de destino, sem página intermediária.
- **FR-005** (RF-05): Cada acesso a um link válido MUST incrementar em 1 o contador daquele
  link, incluindo acessos repetidos e automatizados, sem deduplicação. O redirecionamento
  MUST ser não-permanente/não-cacheável para que todo acesso chegue ao sistema e seja
  contado.
- **FR-006** (RF-06): O criador MUST poder definir, opcionalmente, uma data de expiração
  (dia, sem hora). Data e limite são interpretados em UTC; o link é válido até 23:59:59.999
  UTC da data informada, inclusive. Sem data, o link não expira.
- **FR-007** (RF-07, alterado): O sistema MUST devolver somente o total de acessos de um link
  a quem informar o código e a chave de gerenciamento correta daquele link (ver FR-020 a
  FR-024). Conhecer somente o código MUST NOT ser suficiente.
- **FR-008** (RF-08): Links, códigos, destinos, datas de expiração e contadores MUST ser
  persistidos de forma durável e permanecer disponíveis após reinício da aplicação.
- **FR-009** (RN-01): O código MUST ser único em todo o sistema e MUST NOT ser reutilizado,
  mesmo após a expiração do link.
- **FR-010** (RN-02): A URL de destino MUST ser absoluta, bem formada, usar esquema http ou
  https e ter no máximo 2048 caracteres; caso contrário, a criação MUST ser recusada. O
  sistema MUST NOT acessar o destino para verificar disponibilidade ou acessibilidade antes
  de criar o link.
- **FR-011** (RN-03): Link expirado MUST NOT redirecionar e MUST NOT incrementar o contador;
  o sistema MUST informar que o link expirou.
- **FR-012** (RN-04): O contador de acessos MUST NOT diminuir em nenhuma circunstância.
- **FR-013** (RN-05): Códigos automáticos e personalizados MUST conter somente A-Z maiúsculo e
  1-9; o dígito 0, minúsculas, espaços e caracteres especiais MUST ser rejeitados.
- **FR-014** (RN-06): Se um código automático colidir com um existente, o sistema MUST gerar
  outro, totalizando no máximo 5 tentativas; se todas colidirem, a criação MUST ser recusada
  informando que não foi possível gerar um código disponível.
- **FR-015**: Data de expiração anterior à data atual (UTC) MUST ser recusada na criação.
- **FR-016**: Acesso com código de formato inválido MUST responder HTTP 400 com
  `ValidationProblemDetails` em pt-BR, indicando erro no campo `code`. Acesso com código de
  formato válido, mas inexistente, MUST responder HTTP 404 informando que o link não existe.
  Consulta de estatísticas com código de formato inválido MUST responder HTTP 400 com
  `ValidationProblemDetails` em pt-BR, independentemente da chave; código válido, mas
  inexistente, segue FR-023.
- **FR-017**: Toda recusa por dado inválido MUST informar ao solicitante o motivo, em
  português (pt-BR), indicando qual campo/regra foi violado.
- **FR-018**: O destino de um link MUST NOT ser alterável após a criação.
- **FR-019**: O sistema MUST NOT impor um limite à quantidade de links que uma pessoa pode
  criar.
- **FR-020**: Para cada link criado, o sistema MUST gerar uma chave de gerenciamento secreta e
  imprevisível, com pelo menos 128 bits aleatórios, que MUST NOT ser derivável do código, do
  destino ou da data de criação. Links distintos MUST ter chaves distintas.
- **FR-021**: A resposta de criação MUST devolver a chave de gerenciamento junto com o link
  curto. Essa é a única vez em que a chave é exibida em texto legível: nenhuma outra resposta
  MUST devolvê-la, e não há recuperação nem reemissão da chave nesta feature.
- **FR-022**: A chave de gerenciamento MUST NOT ser armazenada em texto legível nem aparecer
  em logs; o sistema guarda somente uma forma que permita verificá-la, mas não recuperá-la.
- **FR-023**: A consulta de estatísticas MUST exigir a chave no cabeçalho
  `X-Management-Key`. Sem a chave, com chave vazia ou com chave incorreta, o sistema MUST
  responder HTTP 401 com `ProblemDetails` e mensagem em pt-BR, sem revelar o total nem
  indicar qual dos três casos ocorreu. Para código de formato válido, mas inexistente, nenhuma
  chave é correta: a consulta MUST receber a mesma resposta 401, sem indicar que o link não
  existe. Códigos de formato inválido seguem FR-016 e MUST responder 400.
- **FR-024**: A chave de gerenciamento protege somente a consulta de estatísticas. Criar um
  link e acessá-lo (redirecionamento e contagem) MUST NOT exigir a chave.

### Key Entities

- **Link curto**: representa um encurtamento. Atributos: código (único, imutável,
  nunca reutilizado), URL de destino (imutável), indicação de código automático ou
  personalizado, data de criação (UTC), data de expiração opcional (UTC), total de acessos
  (inteiro ≥ 0, monotonicamente crescente), forma verificável e não reversível da chave de
  gerenciamento (nunca a chave em texto legível).
- **Chave de gerenciamento**: segredo gerado na criação, vinculado a exatamente um link,
  exibido uma única vez ao criador e exigido para consultar as estatísticas daquele link.
- **Estatística de acessos**: visão protegida pela chave de gerenciamento contendo somente o
  total de acessos de um link.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Um criador obtém um link curto a partir de uma URL válida em uma única
  solicitação, recebendo a resposta em até 5 segundos. A medição é feita em teste de
  integração local com a aplicação já iniciada e banco temporário, sem incluir o tempo de
  inicialização.
- **SC-002**: Em teste de integração local com a aplicação já iniciada, 100% de 100
  solicitações sequenciais a links válidos recebem a resposta de redirecionamento em menos
  de 1 segundo cada, sem tela intermediária e sem incluir o carregamento do destino.
- **SC-003**: Em um teste com N acessos (inclusive repetidos e simultâneos) a um link válido,
  o total consultado é exatamente N; acessos a links expirados acrescentam 0.
- **SC-004**: 0 ocorrências de dois links com o mesmo código, inclusive entre links
  expirados e novos.
- **SC-005**: Após reinício da aplicação, 100% dos links previamente criados continuam
  redirecionando e preservam seus totais.
- **SC-006**: 100% das solicitações inválidas (URL, código, data) são recusadas com uma
  mensagem em pt-BR que identifica o motivo.
- **SC-007**: Em teste com 100 solicitações simultâneas ao mesmo link válido, o total de
  acessos aumenta exatamente em 100, sem perda de contagens.
- **SC-008**: 100% das consultas de estatísticas sem chave ou com chave incorreta são
  recusadas sem revelar o total; 100% das consultas com a chave correta devolvem o total.
- **SC-009**: A chave de gerenciamento aparece em texto legível em exatamente uma resposta (a
  de criação) e em 0 registros armazenados ou linhas de log.

## Assumptions

- Não há usuários autenticados: qualquer pessoa pode criar e acessar links (cadastro/login
  fora de escopo). A chave de gerenciamento, devolvida uma única vez na criação, é a única
  credencial para consultar o total de acessos; a regra original de RF-07 ("qualquer pessoa
  que conheça o código") foi substituída por decisão do solicitante.
- Guardar a chave é responsabilidade do criador. Perdida a chave, as estatísticas do link não
  podem mais ser consultadas.
- Não há limite de tentativas na consulta de estatísticas: com pelo menos 128 bits aleatórios
  na chave, adivinhá-la por tentativa e erro é inviável.
- A consulta de estatísticas não distingue código inexistente de chave ausente ou
  incorreta: em todos esses casos responde 401, conforme exigido para chaves ausentes ou
  incorretas.
- O nome do cabeçalho (`X-Management-Key`), o status HTTP 401 e o formato `ProblemDetails`
  fazem parte do contrato externo definido pelo solicitante e pela constituição.
- A exigência original de RN-02 de que o destino seja "acessível publicamente" foi removida
  por decisão do solicitante: a validação do destino é apenas sintática (formato, esquema
  http/https e tamanho). Disponibilidade e acessibilidade do destino são responsabilidade do
  criador.
- A data de expiração é informada como data de calendário (sem horário); o link vale até o
  fim desse dia em UTC.
- Códigos informados em minúsculas são recusados, não convertidos.
- A colisão de códigos personalizados não tem novas tentativas: é recusada imediatamente.
- Expiração é tratada como resposta distinta de "link não existe", para o visitante entender
  por que o link não funciona.
- O endereço base do link curto é o endereço onde o serviço está publicado (domínio
  personalizado fora de escopo).
- Fora de escopo: cadastro e login, domínio personalizado, relatório de origem geográfica,
  edição do destino, exclusão de links.
