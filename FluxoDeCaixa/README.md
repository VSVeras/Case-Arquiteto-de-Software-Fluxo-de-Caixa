
# FluxoDeCaixa

Serviço responsável pelo registro de lançamentos financeiros e pela publicação assíncrona dos eventos correspondentes.

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
http://localhost:8080
```

Swagger:

```text
http://localhost:8080/swagger/index.html
```

Health Check:

```text
http://localhost:8080/health
```

## Autenticação DEV

Em `Development`, obtenha o JWT pelo Swagger ou pelo endpoint:

```http
POST /api/dev/autenticacao/token
```

As credenciais são definidas por `AUTENTICACAO_DEV_USUARIO` e `AUTENTICACAO_DEV_SENHA` na configuração de ambiente. Utilize os valores efetivamente configurados no seu ambiente.

Depois, use o JWT como `Bearer` no Swagger (`Authorize`) ou nas chamadas da API.

Autorização exigida para lançamentos:

```text
CONTABILIDADE/FLUXO DE CAIXA/LANCAMENTO
```

## Executar localmente pelo Visual Studio

Com PostgreSQL e RabbitMQ disponíveis, configure os User Secrets do projeto `LivroRazao`:

```json
{
  "RabbitMq:Host": "localhost",
  "RabbitMq:Porta": 5672,
  "RabbitMq:Usuario": "admin",
  "RabbitMq:Senha": "admin",
  "ConnectionStrings:LivroRazao": "Host=localhost;Port=5432;Database=fluxo_de_caixa;Username=postgres;Password=postgres",
  "Jwt:Chave": "FluxoDeCaixa-Desenvolvimento-Chave-JWT-2026-Segura",
  "AutenticacaoDesenvolvimento:Usuario": "usuario.teste@case.com.br",
  "AutenticacaoDesenvolvimento:Senha": "senhaUsuarioTeste"
}
```

Os valores acima são exemplos exclusivamente para desenvolvimento local. Não devem ser utilizados em produção.

Defina `LivroRazao` como Startup Project e execute.

## Testes

Na pasta `FluxoDeCaixa`:

```powershell
dotnet test
```

## Cobertura de testes

A cobertura é coletada com `XPlat Code Coverage` e o relatório HTML é gerado com `ReportGenerator`. O ReportGenerator não configura nem executa SonarQube/SonarCloud.

Instale o ReportGenerator uma única vez, caso ainda não esteja instalado:

```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Na pasta `FluxoDeCaixa`, remova resultados anteriores antes de uma nova coleta:

```powershell
Remove-Item -Recurse -Force .\tests\LivroRazao.TesteDeUnidade\TestResults -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\tests\LivroRazao.TesteDeIntegracao\TestResults -ErrorAction SilentlyContinue
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

**LivroRazao / FluxoDeCaixa: aprovado no escopo das evidências verificadas.**

### Compilação e testes automatizados

- **129 testes automatizados aprovados**, sem falhas.
- Compilação da solução concluída com sucesso.
- Health check `/health` respondendo HTTP 200.

### Teste de carga — 50 RPS

O plano `Case-Arquiteto-de-Software-Fluxo-de-Caixa.jmx` exercitou o endpoint:

```http
POST /api/lancamentos
```

Resultados registrados em `evidencia-carga-completa-50rps.jtl`:

| Indicador | Resultado |
|---|---|
| Taxa configurada | 50 requisições por segundo |
| Requisições de lançamento | 14.914 |
| Respostas HTTP 201 | 14.914 |
| Falhas HTTP | 0 |
| Janela estável de execução | Aproximadamente 50 RPS |

O plano executou os grupos de carga do Livro Razão e do Consolidado Diário **sequencialmente**.

Portanto, o resultado comprova o comportamento do endpoint sob a carga individual testada, mas não demonstra execução simultânea de 50 RPS em ambos os serviços.

### Persistência e reconciliação

A verificação direta no PostgreSQL identificou:

| Indicador | Resultado |
|---|---:|
| Lançamentos persistidos | 232.901 |
| Registros correspondentes no histórico do Consolidado Diário | 232.901 |
| Eventos registrados na Outbox | 232.901 |
| Eventos sem data de publicação | 0 |
| Eventos com erro registrado | 0 |

Todos os eventos da Outbox estavam com `STATUS = 3`.

A reconciliação por data e tipo apresentou os seguintes resultados:

| Data | Tipo | Quantidade | Valor |
|---|---|---:|---:|
| 28/07/2026 | Crédito | 32 | R$ 1.400,00 |
| 19/08/2026 | Crédito | 3 | R$ 400,00 |
| 04/11/2026 | Débito | 188.158 | R$ 18.815.800,00 |
| 05/11/2026 | Débito | 44.708 | R$ 4.470.800,00 |

Os totais coincidiram integralmente com os registros do serviço de Consolidado Diário.

A verificação realizada comprova **consistência quantitativa agregada**. Não substitui uma comparação individual por `CorrelationId` nem uma verificação específica de duplicidades.

### Mensageria e resiliência

Os logs registraram publicação de eventos pelo worker da Outbox e processamento correspondente pelo consumidor, com `CorrelationId` coincidente.

Na verificação do RabbitMQ:

- Fila principal sem mensagens pendentes.
- Fila de retry sem mensagens pendentes.
- Dead Letter Queue sem mensagens pendentes.
- Um consumidor conectado à fila principal.

Também foram observadas falhas transitórias de conexão com o RabbitMQ. Embora existam evidências de publicação e consumo, não foi demonstrada recuperação sem perda em um cenário de indisponibilidade controlada.

### Data de lançamento e UTC

A normalização de `DataLancamento` para UTC está implementada em `Lancamento.NormalizarDataLancamento`.

A consulta `SHOW TIMEZONE` retornou `UTC` no PostgreSQL do Livro Razão.

A reconciliação das datas foi realizada em sessões PostgreSQL configuradas para UTC, apresentando resultados consistentes com os registros do Consolidado Diário.

### Cobertura de testes

Conforme `FluxoDeCaixa - Summary - Coverage Report.pdf`:

| Métrica | Cobertura |
|---|---:|
| Linhas | 87,0% |
| Ramificações | 57,4% |

### Parecer técnico

**Aprovado no escopo das verificações realizadas.**

O serviço demonstrou:

- Registro de lançamentos sob carga configurada de 50 RPS, sem falhas HTTP.
- Persistência de lançamentos e eventos na Outbox.
- Evidências de publicação assíncrona no RabbitMQ.
- Reconciliação quantitativa com o histórico do Consolidado Diário.
- Normalização UTC na implementação examinada.

**Ressalvas:** permanecem sem comprovação específica a carga simultânea dos dois serviços, a reconciliação individual de todos os `CorrelationId` e a recuperação sem perda de eventos após indisponibilidade controlada do broker.

Para o parecer integrado da arquitetura, consulte o `README.md` da raiz e o `ADR-001-Arquitetura-da-Solucao.md`.
