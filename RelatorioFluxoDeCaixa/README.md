
# RelatorioFluxoDeCaixa

Serviço responsável por consumir eventos de lançamentos, manter o histórico, consolidar os valores por data e disponibilizar a consulta do consolidado diário.

> Para subir a solução completa (`FluxoDeCaixa` + `RelatorioFluxoDeCaixa` + PostgreSQL + RabbitMQ), use o `README.md` da raiz do repositório.

## Pré-requisitos

- .NET SDK 10
- Docker Desktop
- PowerShell
- Visual Studio 2026 (opcional)

## Executar pelo Docker Compose da raiz

Na raiz do repositório:

```powershell
# Configure previamente as variáveis de ambiente exigidas pelo Compose.
docker compose up -d --build
docker compose ps
```

API:

```text
http://localhost:8081
```

Swagger:

```text
http://localhost:8081/swagger/index.html
```

Health Check:

```text
http://localhost:8081/health
```

RabbitMQ Management:

```text
http://localhost:15672
```

## Autenticação DEV

Em `Development`, obtenha o JWT pelo Swagger ou pelo endpoint:

```http
POST /api/dev/autenticacao/token
```

As credenciais são definidas por `AUTENTICACAO_DEV_USUARIO` e `AUTENTICACAO_DEV_SENHA` na configuração de ambiente. Utilize os valores efetivamente configurados no seu ambiente.

Depois, use o JWT como `Bearer` no Swagger (`Authorize`) ou nas chamadas da API.

Autorização exigida para consulta do consolidado:

```text
CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO
```

## Consultar consolidado

```http
GET /api/consolidados-diarios/{dataReferencia}
```

Exemplo:

```http
GET /api/consolidados-diarios/2026-07-28
```

## Executar localmente pelo Visual Studio

Com PostgreSQL e RabbitMQ disponíveis, configure os User Secrets do projeto `ConsolidadoDiario`:

```json
{
  "RabbitMq:Host": "localhost",
  "RabbitMq:Porta": 5672,
  "RabbitMq:Usuario": "admin",
  "RabbitMq:Senha": "admin",
  "ConnectionStrings:ConsolidadoDiario": "Host=localhost;Port=5440;Database=relatorio_fluxo_de_caixa;Username=postgres;Password=postgres",
  "Jwt:Chave": "FluxoDeCaixa-Desenvolvimento-Chave-JWT-2026-Segura",
  "AutenticacaoDesenvolvimento:Usuario": "usuario.teste@case.com.br",
  "AutenticacaoDesenvolvimento:Senha": "senhaUsuarioTeste"
}
```

Os valores apresentados são exemplos para desenvolvimento local e não devem ser utilizados em produção.

Defina `ConsolidadoDiario` como Startup Project e execute.

## Testes

Na pasta `RelatorioFluxoDeCaixa`:

```powershell
dotnet test
```

## Cobertura de testes

A cobertura é coletada com `XPlat Code Coverage` e o relatório HTML é gerado com `ReportGenerator`. O ReportGenerator não configura nem executa SonarQube/SonarCloud.

Instale o ReportGenerator uma única vez, caso ainda não esteja instalado:

```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Na pasta `RelatorioFluxoDeCaixa`, remova resultados anteriores antes de uma nova coleta:

```powershell
Remove-Item -Recurse -Force .\tests\ConsolidadoDiario.TesteDeUnidade\TestResults -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\tests\ConsolidadoDiario.TesteDeIntegracao\TestResults -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\CoverageReport -ErrorAction SilentlyContinue

dotnet test --collect "XPlat Code Coverage"

reportgenerator -reports:"tests\**\coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:Html
```

Abra o relatório gerado em:

```text
CoverageReport/index.htm
```

## Parar o ambiente

Na raiz do repositório, para parar os containers preservando-os e mantendo os volumes:

```powershell
docker compose stop
```

Para remover os containers e a rede do Compose, preservando os volumes:

```powershell
docker compose down
```

> **Operação Docker:** a API deste projeto usa `restart: unless-stopped` no Compose isolado e no Compose integrado. As migrations mantêm `restart: "no"`. Consulte o README da raiz para o procedimento de validação da recuperação RabbitMQ.

## Validação e parecer técnico — 05/11/2026

**ConsolidadoDiario / RelatorioFluxoDeCaixa: aprovado no escopo das evidências verificadas.**

### Compilação e testes automatizados

- **78 testes automatizados aprovados**, sem falhas.
- Compilação da solução concluída com sucesso.
- Health check `/health` respondendo HTTP 200.

### Teste de carga — 50 RPS

O plano `Case-Arquiteto-de-Software-Fluxo-de-Caixa.jmx` exercitou o endpoint:

```http
GET /api/consolidados-diarios/{dataReferencia}
```

Resultados registrados em `evidencia-carga-completa-50rps.jtl`:

| Indicador | Resultado |
|---|---|
| Taxa configurada | 50 requisições por segundo |
| Requisições de consulta | 14.632 |
| Respostas HTTP 200 | 14.632 |
| Falhas HTTP | 0 |
| Janela estável de execução | Aproximadamente 50 RPS |

O plano executou os grupos de carga do Livro Razão e do Consolidado Diário **sequencialmente**.

Portanto, o resultado comprova o comportamento do endpoint sob a carga individual testada, mas não demonstra execução simultânea de 50 RPS em ambos os serviços.

### Persistência e reconciliação

A verificação direta no PostgreSQL identificou **232.901 registros** em `TB_HISTORICO_LANCAMENTO`.

A reconciliação por data e tipo apresentou os seguintes resultados:

| Data | Tipo | Quantidade | Valor |
|---|---|---:|---:|
| 28/07/2026 | Crédito | 32 | R$ 1.400,00 |
| 19/08/2026 | Crédito | 3 | R$ 400,00 |
| 04/11/2026 | Débito | 188.158 | R$ 18.815.800,00 |
| 05/11/2026 | Débito | 44.708 | R$ 4.470.800,00 |

Todos os agrupamentos coincidiram integralmente com os lançamentos registrados no Livro Razão.

### Saldos consolidados

A tabela `TB_SALDO_CONSOLIDADO_DIARIO` apresentou os seguintes registros:

| Data de referência | Créditos | Débitos | Saldo diário |
|---|---:|---:|---:|
| 28/07/2026 | R$ 1.400,00 | R$ 0,00 | R$ 1.400,00 |
| 19/08/2026 | R$ 400,00 | R$ 0,00 | R$ 400,00 |
| 04/11/2026 | R$ 0,00 | R$ 18.815.800,00 | -R$ 18.815.800,00 |
| 05/11/2026 | R$ 0,00 | R$ 4.470.800,00 | -R$ 4.470.800,00 |

Os quatro registros confirmaram a fórmula:

```text
Saldo Diário Consolidado = Total de Créditos - Total de Débitos
```

Não foram identificadas divergências nos valores agregados.

A reconciliação quantitativa não substitui a verificação individual de todos os `CorrelationId` nem a execução de testes específicos de duplicidade.

### Mensageria e processamento assíncrono

Os logs registraram processamento de eventos com `CorrelationId` correspondente aos eventos publicados pelo Livro Razão.

Na verificação do RabbitMQ:

- Fila principal sem mensagens pendentes.
- Fila de retry sem mensagens pendentes.
- Dead Letter Queue sem mensagens pendentes.
- Um consumidor conectado à fila principal.

Foram observadas falhas transitórias de conexão com o RabbitMQ. A recuperação sem perda de mensagens durante uma indisponibilidade controlada não foi comprovada neste ciclo.

### Data de referência e UTC

A implementação examinada de `ProcessadorEventoLancamento` utiliza `evento.Payload.DataLancamento.UtcDateTime` para determinar a data de referência.

A consulta `SHOW TIMEZONE` retornou `UTC` no PostgreSQL do Consolidado Diário.

Os registros persistidos em `TB_SALDO_CONSOLIDADO_DIARIO` apresentaram datas e valores consistentes com os agrupamentos do Livro Razão.

### Cobertura de testes

Conforme `RelatorioFluxoDeCaixa - Summary - Coverage Report.pdf`:

| Métrica | Cobertura |
|---|---:|
| Linhas | 74,1% |
| Ramificações | 46,2% |

### Parecer técnico

**Aprovado no escopo das verificações realizadas.**

O serviço demonstrou:

- Atendimento às consultas sob carga configurada de 50 RPS, sem falhas HTTP.
- Consumo assíncrono de eventos publicados pelo Livro Razão.
- Persistência do histórico de lançamentos.
- Reconciliação quantitativa dos 232.901 lançamentos.
- Consistência dos saldos diários persistidos.
- Utilização de UTC para definição da data de referência na implementação examinada.

**Ressalvas:** permanecem sem comprovação específica a carga simultânea dos dois serviços, a reconciliação individual de todos os `CorrelationId`, os testes de duplicidade e a recuperação sem perda de eventos após indisponibilidade controlada do RabbitMQ.

Para o parecer integrado da arquitetura, consulte o `README.md` da raiz e o `ADR-001-Arquitetura-da-Solucao.md`.
