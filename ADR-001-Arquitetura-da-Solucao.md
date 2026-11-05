
# ADR-001 — Arquitetura da Solução

**Status:** Aceito

## 1. Contexto

Este documento registra as principais decisões arquiteturais adotadas para a implementação da solução proposta no desafio técnico.

A solução é composta por dois serviços:

- **FluxoDeCaixa**
- **RelatorioFluxoDeCaixa (Consolidado Diário)**

O objetivo é permitir o registro de lançamentos financeiros e disponibilizar o saldo diário consolidado sem criar dependência síncrona entre os serviços.

---

# 2. Requisitos

## Funcionais

- Registrar lançamentos de débito e crédito.
- Disponibilizar consulta do saldo diário consolidado.
- Manter histórico dos lançamentos processados.

## Não Funcionais

- O serviço de lançamentos não deve ficar indisponível caso o consolidado esteja indisponível.
- Garantir consistência entre a persistência do lançamento e o registro do evento a ser publicado.
- Evitar perda de mensagens.
- Tratar possíveis duplicidades de requisições e mensagens.
- Permitir evolução independente dos serviços.
- Suportar processamento assíncrono.
- Permitir recuperação de falhas temporárias de infraestrutura.

---

# 3. Arquitetura

A solução foi dividida em dois serviços independentes.

## FluxoDeCaixa

Responsável por:

- registrar lançamentos;
- persistir os dados;
- gravar eventos na Transactional Outbox;
- publicar eventos no RabbitMQ por meio do Worker da Outbox;
- garantir idempotência no registro de lançamentos por `CorrelationId`.

## RelatorioFluxoDeCaixa

Responsável por:

- consumir eventos;
- manter histórico dos lançamentos processados;
- atualizar o saldo consolidado;
- garantir idempotência no consumo;
- disponibilizar API de consulta do consolidado diário.

A comunicação de eventos entre os serviços ocorre através do RabbitMQ, sem chamada síncrona do `FluxoDeCaixa` para o `RelatorioFluxoDeCaixa`.

A solução também utiliza Building Blocks compartilhados para capacidades técnicas transversais, como mensageria RabbitMQ e segurança.

---

# 4. Decisões Arquiteturais

## Separação em dois serviços

### Motivação

Evitar acoplamento entre registro de lançamentos e geração do consolidado diário.

### Benefícios

- implantação independente;
- evolução independente;
- maior disponibilidade;
- isolamento das responsabilidades de escrita e consolidação.

### Trade-off

Maior complexidade operacional devido à comunicação assíncrona e à consistência eventual.

---

## Comunicação assíncrona

### Motivação

O processamento do consolidado não deve impactar a disponibilidade nem o tempo de resposta da API de lançamentos.

### Benefícios

- desacoplamento;
- maior disponibilidade;
- tolerância a falhas temporárias;
- processamento independente do consolidado.

### Trade-off

Consistência eventual entre o lançamento registrado e sua disponibilização no consolidado diário.

---

## Transactional Outbox

### Motivação

Garantir consistência entre a persistência do lançamento e o registro do evento que posteriormente será publicado no RabbitMQ.

O lançamento e o registro da Outbox são persistidos na mesma transação de banco de dados.

### Funcionamento

Os eventos da Outbox passam pelo fluxo de processamento:

```text
PENDENTE
   ↓
PROCESSANDO
   ↓
PUBLICADO
```

Quando ocorre falha de publicação, o evento volta a ficar disponível para nova tentativa, não sendo descartado definitivamente por quantidade fixa de tentativas.

A reserva de lotes utiliza bloqueio concorrente com `FOR UPDATE SKIP LOCKED`, permitindo que eventos sejam reservados com segurança sem que diferentes processadores trabalhem simultaneamente sobre o mesmo registro.

Eventos que permaneçam em `PROCESSANDO` além do período esperado podem ser recuperados para novo processamento.

### Benefícios

- reduz o risco de perda de eventos entre banco e broker;
- mantém lançamento e evento atomicamente registrados;
- permite recuperação após falhas temporárias;
- suporta processamento concorrente da Outbox.

### Trade-off

A confirmação da publicação no RabbitMQ e a atualização do status da Outbox não formam uma transação distribuída. Se o processo falhar após o broker confirmar a publicação e antes de o status ser persistido como `PUBLICADO`, o evento poderá ser republicado.

Esse cenário é tratado pelo consumidor idempotente.

---

## Worker Publicador da Outbox

### Motivação

Executar a publicação dos eventos sem bloquear a requisição da API.

### Benefícios

- menor tempo de resposta da API;
- publicação assíncrona;
- processamento em lotes;
- recuperação de eventos pendentes;
- possibilidade de evolução para deployment independente.

### Trade-off

Necessidade de monitoramento do processamento da Outbox e de seus eventos pendentes.

---

## RabbitMQ e Publisher Confirm

### Motivação

Desacoplar produtor e consumidor e obter confirmação do broker para as publicações realizadas.

A publicação utiliza **Publisher Confirm**, permitindo que o publicador saiba se o RabbitMQ confirmou o recebimento da mensagem.

### Benefícios

- comunicação assíncrona;
- desacoplamento entre serviços;
- confirmação de publicação pelo broker;
- facilidade para inclusão de novos consumidores.

### Trade-off

Dependência de infraestrutura adicional e necessidade de tratamento explícito de falhas de publicação.

---

## Retry e Dead Letter Queue no consumidor

### Motivação

Falhas temporárias durante o consumo não devem causar perda imediata da mensagem.

O consumidor utiliza mecanismo de retry. Quando o limite de reprocessamentos é atingido, a mensagem segue para Dead Letter Queue (DLQ), permitindo tratamento posterior sem bloquear indefinidamente o fluxo principal.

### Benefícios

- recuperação automática de falhas transitórias;
- isolamento de mensagens que não puderam ser processadas;
- preservação do fluxo principal;
- possibilidade de análise posterior das mensagens enviadas à DLQ.

### Trade-off

A topologia de mensageria e a operação do ambiente tornam-se mais complexas, exigindo acompanhamento das filas de retry e DLQ.

---

## Idempotência

### Motivação

A arquitetura assíncrona trabalha com possibilidade de reenvio ou republicação. Por isso, duplicidades precisam ser tratadas tanto na entrada do `FluxoDeCaixa` quanto no consumidor do `RelatorioFluxoDeCaixa`.

### FluxoDeCaixa

O `CorrelationId` identifica a requisição e é protegido por restrição de unicidade no banco. O tratamento também considera concorrência entre requisições simultâneas com o mesmo identificador.

### RelatorioFluxoDeCaixa

O consumidor mantém o `CorrelationId` no histórico e utiliza persistência idempotente. O registro do evento é realizado de forma que uma duplicidade não provoque nova atualização do saldo consolidado.

### Benefícios

- processamento seguro diante de reenvios;
- proteção contra duplicidade concorrente;
- suporte ao cenário de republicação da Outbox;
- previsibilidade do resultado financeiro.

### Trade-off

Necessidade de armazenamento e controle da chave de idempotência.

---

## Histórico + Projeção

### Motivação

Separar o histórico dos eventos processados da estrutura otimizada para consulta do saldo consolidado.

### Benefícios

- histórico preservado;
- consultas rápidas;
- rastreabilidade dos lançamentos processados;
- possibilidade de reconstrução do consolidado a partir do histórico, caso necessário.

### Trade-off

Duplicação controlada dos dados entre histórico e projeção.

---

## Segurança

### Motivação

Proteger as APIs e restringir o acesso às operações expostas.

A solução implementa autenticação JWT e autorização baseada em autorização funcional no formato **Área/Processo/Operação**.

Autorizações utilizadas na solução:

```text
CONTABILIDADE/FLUXO DE CAIXA/LANCAMENTO
CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO
```

Em ambiente `Development`, cada API disponibiliza recurso de autenticação destinado ao uso local e aos testes da solução.

As capacidades técnicas compartilhadas de JWT e autorização ficam no Building Block de segurança; os endpoints HTTP permanecem nas respectivas APIs.

### Benefícios

- autenticação das chamadas;
- autorização por capacidade funcional;
- centralização da configuração técnica de segurança;
- separação entre infraestrutura de segurança e endpoints das aplicações.

### Gerenciamento de segredos no ambiente local

A solução já adota mecanismos para evitar o versionamento de credenciais e segredos:

- para execução local via Visual Studio/dotnet, utiliza **.NET User Secrets**;
- para execução via Docker Compose, recebe credenciais e segredos por variáveis de ambiente configuradas externamente;
- valores sensíveis não devem ser versionados no repositório.

Esses mecanismos de gerenciamento de credenciais não equivalem à criptografia dos dados financeiros persistidos ou transmitidos.

### Trade-off

A solução local utiliza mecanismo de autenticação de desenvolvimento. Em ambientes produtivos, emissão de identidade e gestão de credenciais e segredos devem ser integradas aos mecanismos corporativos apropriados.

A criptografia de dados sensíveis em repouso e em trânsito deverá ser avaliada conforme a classificação da informação e os requisitos do ambiente de produção. Sua implementação efetiva não foi comprovada nos fontes examinados.

---

# 5. Funcionamento

1. O cliente autentica-se e obtém um JWT válido para o ambiente.
2. O cliente registra um lançamento no `FluxoDeCaixa`, informando o `CorrelationId`.
3. O lançamento e o evento da Outbox são persistidos na mesma transação.
4. O Worker reserva eventos pendentes da Outbox.
5. O Worker publica o evento no RabbitMQ e aguarda a confirmação do broker.
6. Após confirmação, o evento da Outbox é marcado como publicado.
7. O `RelatorioFluxoDeCaixa` consome a mensagem.
8. O consumidor verifica/processa a mensagem de forma idempotente.
9. O histórico é persistido e o saldo consolidado é atualizado.
10. Em caso de sucesso, a mensagem é confirmada pelo consumidor.
11. Em falhas transitórias, aplica-se o fluxo de retry.
12. Quando o limite de reprocessamentos do consumidor é atingido, a mensagem segue para a DLQ.

---

# 6. Atendimento ao desafio

A solução atende aos principais requisitos do desafio por meio de:

- separação entre serviços;
- processamento assíncrono;
- persistência transacional;
- Transactional Outbox;
- idempotência no produtor/API e no consumidor;
- RabbitMQ com confirmação de publicação;
- retry e DLQ no consumo;
- API para registro de lançamentos;
- API para consulta do consolidado;
- autenticação e autorização;
- testes automatizados;
- documentação operacional e arquitetural.

---

# 7. Vantagens

- Baixo acoplamento.
- Alta coesão.
- Facilidade de evolução.
- Tolerância a indisponibilidade temporária.
- Consistência transacional entre lançamento e Outbox.
- Recuperação automática de eventos pendentes da Outbox.
- Tratamento de falhas transitórias no consumo.
- Proteção contra processamento duplicado.
- Facilidade para novos consumidores.
- Histórico para rastreabilidade.
- Projeção otimizada para leitura.

---

# 8. Limitações e Trade-offs

- Consistência eventual entre os serviços.
- Dependência do RabbitMQ.
- Necessidade de monitorar Worker, Outbox, filas de retry e DLQ.
- Maior complexidade operacional em relação a uma solução síncrona.
- Possibilidade de republicação quando houver falha após o Publisher Confirm e antes da persistência do status `PUBLICADO`; a duplicidade é absorvida pelo consumidor idempotente.
- O Worker da Outbox executa junto à aplicação atual, embora possa ser separado fisicamente no futuro caso a volumetria ou os requisitos operacionais justifiquem.

---

# 9. Evoluções Futuras

## Segurança

- Integrar autenticação a um provedor corporativo de identidade, quando aplicável.
- Avaliar e implementar criptografia de dados sensíveis em repouso e em trânsito, conforme a classificação da informação e os requisitos do ambiente de produção.
- Em ambientes produtivos, integrar a solução a um gerenciador de segredos, como AWS Secrets Manager, Azure Key Vault ou equivalente, mantendo a aplicação desacoplada do mecanismo de armazenamento dos segredos.

## Observabilidade

- OpenTelemetry.
- Distributed Tracing.
- Centralização de logs.
- Evolução da correlação distribuída utilizando `CorrelationId`.

## Métricas

- Taxa de publicação.
- Tempo médio de processamento.
- Eventos pendentes na Outbox.
- Falhas de publicação da Outbox.
- Tempo médio de consumo.
- Latência fim a fim.
- Throughput do consumidor.
- Quantidade de mensagens em retry e DLQ.

## Outras evoluções

- Evoluir o Worker da Outbox para processo/deployment independente, caso a volumetria ou requisitos operacionais justifiquem.
- Backoff exponencial, caso necessário para o perfil de falhas observado.
- Escalabilidade horizontal dos Workers.
- Múltiplos consumidores.
- Versionamento de eventos.

---

# 10. Recuperação operacional da mensageria (2026-11-04)

O consumidor `ConsolidadoDiario` aplica tentativas com backoff limitado (1 a 30 segundos) quando a conexão/assinatura inicial falha transitoriamente. A recuperação automática de conexões já estabelecidas continua a cargo do RabbitMQ.Client, com `AutomaticRecoveryEnabled` e `TopologyRecoveryEnabled`. O Docker Compose reinicia as duas APIs (`restart: unless-stopped`) em caso de encerramento do processo, sem reiniciar automaticamente os jobs de migrations.

Essa recuperação de infraestrutura é distinta do retry de **mensagens**, que preserva ACK, republicação para a fila de retry e DLQ. A validação de falha real do broker e de consumo após recuperação deve ser executada no ambiente Docker antes do teste de carga.

---

# 11. Precisões da implementação final (2026-11-04)

- **Dia contábil:** a data de lançamento é normalizada para UTC em `LivroRazao` e a projeção diária usa a data UTC do evento; não se aplica timezone local do servidor.
- **Falha inesperada no callback RabbitMQ:** registrar erro e tentar `NACK` com requeue, sem emitir `ACK`; com canal fechado, a mensagem não confirmada volta ao broker.
- **Falha na assinatura:** `ConsumidorConsolidadoWorker` tenta novamente com backoff de 1 a 30 segundos, inclusive para exceções não categorizadas como transitórias.
- **Sem alteração de arquitetura:** permanecem Transactional Outbox, Publisher Confirm, idempotência, retry e DLQ.

---

# 12. Evidências de validação da arquitetura (2026-11-05)

A execução do plano JMeter `Case-Arquiteto-de-Software-Fluxo-de-Caixa.jmx` registrou **29.746 amostras sem falhas HTTP**: 14.914 criações de lançamentos (201), 14.632 consultas ao consolidado (200) e 200 autenticações (200). Os grupos de carga foram **sequenciais**, cada qual configurado para 50 RPS; o ensaio não comprova 50 RPS simultâneos entre serviços.

A reconciliação quantitativa dos bancos verificou **232.901 lançamentos** no Livro Razão e **232.901 no histórico do Consolidado Diário**, com correspondência integral de quantidades e valores agrupados por data e tipo. Os quatro saldos diários persistidos reproduzem os totais e a regra `créditos - débitos`. A Outbox apresentou 232.901 eventos com data de publicação e sem erro registrado. Esses resultados sustentam a consistência **agregada**, não uma auditoria individual de `CorrelationId` ou a inexistência de duplicidades compensatórias.

A regra de dia contábil UTC está implementada em `Lancamento.NormalizarDataLancamento` (`ToUniversalTime`) e em `ProcessadorEventoLancamento` (`UtcDateTime`); as sessões PostgreSQL consultadas também estavam em UTC. Falhas transitórias de conexão RabbitMQ foram observadas nos logs, juntamente com publicação e consumo bem-sucedidos; recuperação sem perda durante indisponibilidade controlada permanece sem comprovação específica neste ciclo. Essas evidências **não alteram as decisões arquiteturais** registradas neste ADR.

---

# 13. Conclusão

A arquitetura adotada atende aos requisitos do desafio priorizando disponibilidade, desacoplamento, consistência transacional e resiliência.

O `FluxoDeCaixa` registra o lançamento e seu evento por meio do padrão Transactional Outbox, enquanto a publicação assíncrona no RabbitMQ impede que a disponibilidade do serviço de consolidação afete o registro financeiro.

O `RelatorioFluxoDeCaixa` processa os eventos de maneira idempotente, mantém histórico e atualiza a projeção do saldo consolidado. Retry e DLQ tratam falhas no consumo, enquanto Publisher Confirm aumenta a segurança da publicação no broker.

A possibilidade residual de republicação após confirmação do RabbitMQ é uma característica conhecida da estratégia adotada e é tratada pela idempotência do consumidor, evitando efeitos financeiros duplicados.

Com isso, os dois serviços permanecem desacoplados e podem evoluir de forma independente, preservando rastreabilidade, disponibilidade e consistência adequada ao modelo assíncrono adotado.
