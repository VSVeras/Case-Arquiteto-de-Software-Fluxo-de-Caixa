# Relatório de Fluxo de Caixa — Consolidado Diário

Parte II do case de Fluxo de Caixa.

## Escopo

- Consumir eventos de lançamentos publicados no RabbitMQ.
- Usar `CorrelationId` como chave de idempotência.
- Manter `TB_HISTORICO_LANCAMENTO` como fonte da verdade.
- Atualizar `TB_SALDO_CONSOLIDADO_DIARIO` como projeção otimizada para consulta.
- Disponibilizar API de consulta do saldo diário consolidado.
- Usar Dapper e PostgreSQL na persistência.

## Estrutura

O projeto segue a mesma estrutura da Parte I: um único projeto principal com as pastas `Aplicacao`, `Dominio`, `Infraestrutura`, `IoC` e `Rest`, além dos projetos separados de testes.

## Estado desta entrega

- Estrutura base revisada e limpa.
- Dependências iniciais de Dapper, PostgreSQL, RabbitMQ, Swagger e Health Checks.
- Contrato do evento recebido da Parte I.
- Modelo de domínio inicial.
- Script SQL inicial das tabelas do consolidado.
- Docker Compose com PostgreSQL, RabbitMQ, migração SQL e aplicação.

A implementação do Consumer, dos repositórios Dapper e dos endpoints será adicionada nas próximas entregas.
