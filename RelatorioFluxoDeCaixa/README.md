# Relatório de Fluxo de Caixa — Consolidado Diário

Segunda etapa do case de Fluxo de Caixa.

## Escopo

- Consumir eventos de lançamentos publicados no RabbitMQ pela primeira etapa.
- Usar `CorrelationId` como chave de idempotência.
- Manter `TB_HISTORICO_LANCAMENTO` como fonte da verdade.
- Atualizar `TB_SALDO_CONSOLIDADO_DIARIO` como projeção otimizada para consulta.
- Disponibilizar API de consulta do saldo diário consolidado.
- Usar Dapper e PostgreSQL na persistência.

## Fluxo do consumidor

```text
RabbitMQ
    ↓
ConsumidorConsolidadoWorker
    ↓
ProcessadorEventoLancamento
    ↓
ConsolidadoDiarioDao
    ↓
INSERT idempotente em TB_HISTORICO_LANCAMENTO
    ↓
UPSERT em TB_SALDO_CONSOLIDADO_DIARIO
    ↓
COMMIT
    ↓
ACK no RabbitMQ
```

O histórico e a atualização do saldo são executados na mesma transação. Quando o `CorrelationId` já existe, o histórico e o saldo não são alterados e a mensagem é confirmada com `ACK`.

## RabbitMQ

O projeto usa o RabbitMQ disponibilizado pelo ambiente da primeira etapa.

Configuração compartilhada:

```text
Exchange: fluxocaixa.exchange
Routing key: fluxocaixa.lancamento.criado
Fila: fluxocaixa.lancamento.criado
```

Quando o consolidado é executado em Docker e o RabbitMQ da primeira etapa está publicado na porta `5672` da máquina, o host configurado é `host.docker.internal`.

## Persistência

- `TB_HISTORICO_LANCAMENTO`: histórico, auditoria e idempotência.
- `TB_SALDO_CONSOLIDADO_DIARIO`: projeção diária consolidada.
- `ConsolidadoDiarioDao`: acesso transacional com Dapper.

## Estado desta entrega

- Consumer RabbitMQ com `ACK` manual.
- Reprocessamento de falhas técnicas com `NACK` e `requeue`.
- Descarte de mensagens inválidas com `reject` sem requeue.
- Processamento idempotente por `CorrelationId`.
- Atualização atômica do histórico e do consolidado diário.
- Health checks do PostgreSQL e RabbitMQ.

## API REST

Consulta do consolidado diário por data de referência:

```http
GET /api/consolidados-diarios/{dataReferencia}
```

Exemplo:

```http
GET /api/consolidados-diarios/2026-07-22
```

Resposta `200 OK`:

```json
{
  "dataReferencia": "2026-07-22",
  "totalCreditos": 300.00,
  "totalDebitos": 50.00,
  "saldoDiarioConsolidado": 250.00,
  "dataAtualizacao": "2026-07-22T20:17:38.73815+00:00"
}
```

Quando não houver consolidado para a data, a API retorna `404 Not Found`.

## Pré-requisitos

- .NET SDK 8.
- Docker Desktop em execução.
- RabbitMQ da primeira etapa disponível na porta `5672` para execução completa do consumidor.

## Executar com Docker

Na raiz da solução:

```bash
docker compose up -d --build
```

Serviços disponibilizados:

```text
API: http://localhost:8081
Swagger: http://localhost:8081/swagger
Health check: http://localhost:8081/health
PostgreSQL: localhost:5433
```

Acompanhar os logs do consumidor:

```bash
docker logs -f consolidado-diario
```

Encerrar os serviços:

```bash
docker compose down
```

Encerrar os serviços e remover os dados persistidos:

```bash
docker compose down -v
```

## Executar localmente

Iniciar apenas o PostgreSQL e aplicar a migração:

```bash
docker compose up -d postgres migrations
```

Executar a aplicação:

```bash
dotnet run --project src/ConsolidadoDiario/ConsolidadoDiario.csproj
```

A aplicação local utiliza as configurações de `appsettings.json`:

```text
PostgreSQL: localhost:5433
RabbitMQ: localhost:5672
```

## Testes

A solução possui dois projetos de teste:

```text
ConsolidadoDiario.TesteDeUnidade
ConsolidadoDiario.TesteDeIntegracao
```

Os testes de unidade validam:

- Criação e invariantes de `HistoricoLancamento`.
- Conversão do evento para o histórico do lançamento.
- Validação dos tipos de evento e lançamento.
- Consulta e conversão de `SaldoConsolidado` para `ConsolidadoDiarioResposta`.
- Retorno nulo quando não houver consolidado para a data.

Os testes de integração validam com PostgreSQL real em Testcontainers:

- Persistência de crédito e débito.
- Cálculo do saldo diário consolidado.
- Consulta do consolidado por data.
- Idempotência por `CorrelationId`.
- Retorno nulo para uma data sem consolidado.

Executar todos os testes:

```bash
dotnet test RelatorioFluxoDeCaixa.sln
```

Executar somente os testes de unidade:

```bash
dotnet test tests/ConsolidadoDiario.TesteDeUnidade/ConsolidadoDiario.TesteDeUnidade.csproj
```

Executar somente os testes de integração:

```bash
dotnet test tests/ConsolidadoDiario.TesteDeIntegracao/ConsolidadoDiario.TesteDeIntegracao.csproj
```

Os testes de integração exigem que o Docker esteja em execução. O Testcontainers cria e remove automaticamente uma instância isolada do PostgreSQL.

## Cobertura de testes

Gerar cobertura com Coverlet:

```bash
dotnet test RelatorioFluxoDeCaixa.sln --collect:"XPlat Code Coverage"
```

O arquivo de cobertura é gerado dentro de uma pasta `TestResults` em cada projeto de teste executado.

## Estrutura da solução

```text
RelatorioFluxoDeCaixa
├── src
│   └── ConsolidadoDiario
│       ├── Aplicacao
│       │   ├── Abstracao
│       │   ├── Dto
│       │   ├── EventosDeIntegracao
│       │   └── Servico
│       ├── Infraestrutura
│       │   ├── Dao
│       │   ├── Mensageria
│       │   ├── Migracao
│       │   ├── Persistencia
│       │   ├── RegistroDeEvento
│       │   └── Worker
│       ├── IoC
│       └── Rest
└── tests
    ├── ConsolidadoDiario.TesteDeIntegracao
    └── ConsolidadoDiario.TesteDeUnidade
```
