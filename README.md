# Case — Arquiteto de Software — Fluxo de Caixa

Solução desenvolvida em **C# / .NET 10** para registro de lançamentos financeiros (débito e crédito) e consulta do saldo consolidado diário, utilizando comunicação assíncrona, persistência transacional e independência operacional entre os serviços.

Este é o README principal da solução.

Documentação complementar:

- [FluxoDeCaixa — Livro Razão](FluxoDeCaixa/README.md)
- [RelatorioFluxoDeCaixa — Consolidado Diário](RelatorioFluxoDeCaixa/README.md)
- [ADR-001 — Arquitetura da Solução](ADR-001-Arquitetura-da-Solucao.md)

## 1. Visão geral da arquitetura

A solução está organizada em dois serviços independentes, apoiados por Building Blocks compartilhados.

| Componente | Responsabilidade |
|---|---|
| `FluxoDeCaixa` / `LivroRazao` | Registrar lançamentos, persistir dados e eventos na Transactional Outbox e publicar eventos de forma assíncrona |
| `RelatorioFluxoDeCaixa` / `ConsolidadoDiario` | Consumir eventos, manter histórico, garantir idempotência e disponibilizar o consolidado diário |
| `BuildingBlocks` | Compartilhar recursos técnicos de mensageria RabbitMQ, autenticação JWT e autorização |
| PostgreSQL | Persistir os dados dos serviços em bancos independentes |
| RabbitMQ | Transportar eventos entre os serviços |

### Fluxo de processamento

```text
Cliente
   |
   v
LivroRazao API
POST /api/lancamentos
   |
   v
PostgreSQL - Fluxo de Caixa
   |-- TB_LANCAMENTO
   |-- TB_LANCAMENTO_OUTBOX
   |
   v
Worker Publicador da Outbox
   |
   v
RabbitMQ
   |
   v
Consumidor do Consolidado Diário
   |
   v
PostgreSQL - Relatório
   |-- TB_HISTORICO_LANCAMENTO
   |-- TB_SALDO_CONSOLIDADO_DIARIO
   |
   v
ConsolidadoDiario API
GET /api/consolidados-diarios/{dataReferencia}
```

A comunicação entre os serviços é assíncrona, com consistência eventual.

O serviço de lançamentos não depende de uma chamada HTTP síncrona ao serviço de consolidação. A indisponibilidade temporária do consumidor não impede o registro de novos lançamentos, desde que as dependências necessárias ao Livro Razão estejam disponíveis.

### Decisões arquiteturais

A solução utiliza:

- Separação entre serviços.
- Transactional Outbox.
- Worker de publicação assíncrona.
- RabbitMQ com Publisher Confirm.
- Entrega de mensagens ao menos uma vez.
- Idempotência por `CorrelationId`.
- Retry e Dead Letter Queue (DLQ).
- Histórico de lançamentos e projeção do saldo diário.
- Autenticação JWT e autorização funcional.
- Normalização de datas para UTC.

As motivações, limitações e trade-offs estão documentados no ADR-001.

---

## 2. Pré-requisitos

Para executar o ambiente integrado:

- Windows com PowerShell 5.1 ou superior.
- Docker Desktop instalado e iniciado.
- Docker Compose disponível.
- Portas necessárias disponíveis.

Para desenvolvimento e testes fora dos containers:

- .NET SDK 10.
- Visual Studio ou Visual Studio Code, opcional.

### Portas utilizadas

| Componente | Porta no host |
|---|---:|
| Livro Razão | 8080 |
| Consolidado Diário | 8081 |
| PostgreSQL Livro Razão | 5432 |
| PostgreSQL Consolidado Diário | 5440 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ Management | 15672 |

Todos os comandos de inicialização devem ser executados na raiz do repositório, onde está localizado o `docker-compose.yml` integrado.

---

## 3. Configuração do ambiente local

### 3.1. Variáveis obrigatórias

O Docker Compose e a bateria integrada de testes utilizam variáveis de ambiente para configurar as credenciais e os parâmetros de segurança.

| Variável | Finalidade |
|---|---|
| `POSTGRES_USER` | Usuário PostgreSQL |
| `POSTGRES_PASSWORD` | Senha PostgreSQL |
| `RABBITMQ_USER` | Usuário RabbitMQ |
| `RABBITMQ_PASSWORD` | Senha RabbitMQ |
| `RABBITMQ_ERLANG_COOKIE` | Cookie interno RabbitMQ |
| `JWT_CHAVE` | Chave de assinatura JWT |
| `AUTENTICACAO_DEV_USUARIO` | Usuário de autenticação para Development |
| `AUTENTICACAO_DEV_SENHA` | Senha de autenticação para Development |

O Compose integrado utiliza:

```yaml
ASPNETCORE_ENVIRONMENT: Development
```

Essa configuração define o perfil de execução do ASP.NET Core. Ela não substitui nem impede o uso de variáveis de ambiente.

O ASP.NET Core recebe configurações como `Jwt__Chave`, `RabbitMq__Usuario` e `ConnectionStrings__LivroRazao` a partir das variáveis fornecidas ao Docker Compose.

### 3.2. Arquivo `.env`

Para o ambiente local, mantenha um arquivo `.env` na raiz do repositório.

Exemplo de estrutura:

```dotenv
POSTGRES_USER=postgres
POSTGRES_PASSWORD=SUBSTITUIR_PELA_SENHA_LOCAL

RABBITMQ_USER=admin
RABBITMQ_PASSWORD=SUBSTITUIR_PELA_SENHA_LOCAL
RABBITMQ_ERLANG_COOKIE=SUBSTITUIR_POR_COOKIE_LOCAL

JWT_CHAVE=SUBSTITUIR_POR_CHAVE_FORTE

AUTENTICACAO_DEV_USUARIO=SUBSTITUIR_PELO_USUARIO_LOCAL
AUTENTICACAO_DEV_SENHA=SUBSTITUIR_PELA_SENHA_LOCAL
```

Os valores acima são marcadores de configuração, não credenciais prontas para utilização.

O arquivo `.env` contém informações sensíveis e **não deve ser versionado no Git**.

Utilize valores adequados para o ambiente local. A chave JWT deve ter comprimento e entropia compatíveis com o algoritmo configurado.

Se os volumes PostgreSQL ou RabbitMQ já tiverem sido inicializados, mantenha as credenciais correspondentes. Alterar variáveis não altera automaticamente credenciais persistidas anteriormente.

### 3.3. Carregamento automático das configurações

O Docker Compose utiliza automaticamente o arquivo `.env` da raiz do repositório para resolver as variáveis de configuração.

O script `Bateria de testes.ps1` também carrega automaticamente esse arquivo por meio da função `Carregar-EnvLocal`.

O carregamento ocorre antes da utilização das credenciais, utilizando o diretório do próprio script como referência.

O script valida a presença das oito variáveis obrigatórias e disponibiliza seus valores no processo PowerShell durante a execução.

**Não é necessário carregar manualmente o `.env` nem configurar variáveis adicionais antes de executar a bateria.**

O carregador foi preparado para valores literais simples no formato `NOME=VALOR`, incluindo valores delimitados por aspas simples ou duplas. Não interpreta expansão `${VAR}`, sequências de escape ou comentários ao final dos valores.

O carregamento não imprime os valores das credenciais.

---

## 4. Inicialização completa pelo Docker Compose

### 4.1. Verificar o Docker

Com o Docker Desktop iniciado:

```powershell
docker info
docker compose version
docker context show
```

Confirme que o contexto Docker corresponde ao ambiente local esperado.

### 4.2. Validar o Compose

Na raiz do repositório:

```powershell
docker compose config --quiet
```

Esse comando verifica a configuração sem imprimir a configuração expandida com credenciais.

### 4.3. Subir o ambiente

```powershell
docker compose up -d --build
```

A solução integrada contém os seguintes serviços:

| Serviço Compose | Função |
|---|---|
| `rabbitmq` | Broker de mensagens |
| `postgres-fluxo` | Banco PostgreSQL do Livro Razão |
| `migrations-fluxo` | Aplicação das migrations do Livro Razão |
| `livro-razao` | API e Worker da Outbox |
| `postgres-relatorio` | Banco PostgreSQL do Consolidado Diário |
| `migrations-relatorio` | Aplicação da estrutura do banco do relatório |
| `consolidado-diario` | API e consumidor de eventos |

O Docker Compose controla as dependências de inicialização.

As migrations são executadas após a disponibilidade dos bancos e antes da inicialização das respectivas APIs.

### 4.4. Verificar os containers

```powershell
docker compose ps
docker compose ps -a
```

Os serviços de migrations são processos de execução pontual e podem apresentar estado `Exited (0)` após conclusão bem-sucedida.

### 4.5. Consultar os logs

Infraestrutura:

```powershell
docker compose logs --tail=100 rabbitmq postgres-fluxo postgres-relatorio
```

Migrations:

```powershell
docker compose logs --tail=100 migrations-fluxo migrations-relatorio
```

APIs:

```powershell
docker compose logs --tail=100 livro-razao consolidado-diario
```

Em caso de falha, examine os logs antes de remover ou recriar containers.

---

## 5. Validação de disponibilidade

### 5.1. Health Checks

Execute:

```powershell
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
Invoke-WebRequest http://localhost:8081/health -UseBasicParsing
```

O resultado esperado é HTTP 200 para ambas as APIs.

### 5.2. Endereços

| Componente | Endereço |
|---|---|
| Livro Razão — API | http://localhost:8080 |
| Livro Razão — Swagger | http://localhost:8080/swagger/index.html |
| Livro Razão — Health | http://localhost:8080/health |
| Consolidado Diário — API | http://localhost:8081 |
| Consolidado Diário — Swagger | http://localhost:8081/swagger/index.html |
| Consolidado Diário — Health | http://localhost:8081/health |
| RabbitMQ Management | http://localhost:15672 |

Para acessar o RabbitMQ Management, utilize as credenciais definidas nas variáveis `RABBITMQ_USER` e `RABBITMQ_PASSWORD`.

---

## 6. Autenticação e endpoints

### 6.1. Autenticação de desenvolvimento

Em ambiente `Development`, obtenha um JWT utilizando:

```http
POST /api/dev/autenticacao/token
```

Exemplo de corpo:

```json
{
  "usuario": "<AUTENTICACAO_DEV_USUARIO>",
  "senha": "<AUTENTICACAO_DEV_SENHA>"
}
```

Substitua os marcadores pelos valores configurados no ambiente.

Utilize o `accessToken` retornado como:

```http
Authorization: Bearer <token>
```

O token também pode ser informado pelo botão `Authorize` do Swagger.

A autenticação DEV destina-se exclusivamente ao ambiente de desenvolvimento e aos testes da solução.

### 6.2. Livro Razão

Endereço base:

```text
http://localhost:8080
```

Endpoints principais:

```http
POST /api/lancamentos
GET /api/lancamentos/{id}
```

Autorização funcional:

```text
CONTABILIDADE/FLUXO DE CAIXA/LANCAMENTO
```

### 6.3. Consolidado Diário

Endereço base:

```text
http://localhost:8081
```

Endpoint:

```http
GET /api/consolidados-diarios/{dataReferencia}
```

Exemplo:

```http
GET /api/consolidados-diarios/2026-07-28
```

Autorização funcional:

```text
CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO
```

Para os contratos completos de entrada e saída, utilize o Swagger de cada serviço.

---

## 7. Testes automatizados

Na raiz do repositório:

```powershell
dotnet test .\BuildingBlocks\BuildingBlocks.sln
dotnet test .\FluxoDeCaixa\FluxoDeCaixa.sln
dotnet test .\RelatorioFluxoDeCaixa\RelatorioFluxoDeCaixa.sln
```

Os resultados registrados na validação anterior foram:

| Solução | Testes aprovados |
|---|---:|
| BuildingBlocks | 44 |
| FluxoDeCaixa | 129 |
| RelatorioFluxoDeCaixa | 78 |
| **Total** | **251** |

**Resultado registrado: 251 testes aprovados, 0 falhas.**

Esses números correspondem às evidências anteriores. Uma nova execução deve ser utilizada para confirmar o estado atual da entrega.

Os procedimentos de cobertura estão descritos nos READMEs específicos de cada serviço.

---

## 8. Bateria integrada de testes e exceções

### 8.1. Restrição obrigatória

**ATENÇÃO — SCRIPT DESTRUTIVO.**

O arquivo `Bateria de testes.ps1` foi desenvolvido para execução exclusivamente em ambiente local de testes, isolado e descartável.

**NÃO EXECUTAR EM PRODUÇÃO.**

**NÃO EXECUTAR EM AMBIENTES COMPARTILHADOS OU COM DADOS QUE PRECISAM SER PRESERVADOS.**

O script executa intencionalmente operações destrutivas para validar o comportamento da solução diante de falhas.

Entre as operações estão:

- Limpeza da fila principal do RabbitMQ.
- Limpeza da fila de retry.
- Limpeza da Dead Letter Queue (DLQ).
- Publicação de mensagens para testes.
- Interrupção e reinicialização do RabbitMQ.
- Interrupção e reinicialização do PostgreSQL do Consolidado Diário.
- Testes de indisponibilidade e recuperação.
- Testes de processamento assíncrono.
- Testes de idempotência e concorrência.

Essas operações são necessárias para exercitar os cenários de exceção e resiliência e devem ser preservadas.

A limpeza de filas remove mensagens pendentes. A interrupção dos serviços afeta temporariamente sua disponibilidade.

### 8.2. Credenciais utilizadas

A bateria utiliza as credenciais configuradas no arquivo `.env` da raiz do repositório.

As variáveis são:

```text
POSTGRES_USER
POSTGRES_PASSWORD
RABBITMQ_USER
RABBITMQ_PASSWORD
RABBITMQ_ERLANG_COOKIE
JWT_CHAVE
AUTENTICACAO_DEV_USUARIO
AUTENTICACAO_DEV_SENHA
```

O script carrega automaticamente essas variáveis ao iniciar, utilizando a função `Carregar-EnvLocal`.

Não é necessário executar comandos adicionais para carregar as credenciais.

A bateria não utiliza credenciais DEV fixas no código.

### 8.3. Executar a bateria

Antes da execução, certifique-se de que o Docker Desktop está conectado ao ambiente local de testes.

**A execução deve ocorrer somente em ambiente isolado, pois a bateria realiza operações destrutivas.**

Na raiz do repositório, execute:

```powershell
& ".\Bateria de testes.ps1"
```

O script realiza automaticamente o carregamento do `.env` e inicia os testes integrados.

Não é necessário configurar variáveis de autorização adicionais nem realizar confirmação interativa.

### 8.4. Verificação após os testes

Após a bateria, verifique os serviços:

```powershell
docker compose ps
```

Consulte os logs:

```powershell
docker compose logs --tail=100 rabbitmq livro-razao consolidado-diario
```

Confira os Health Checks:

```powershell
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
Invoke-WebRequest http://localhost:8081/health -UseBasicParsing
```

Caso a bateria tenha interrompido serviços e não tenha concluído sua recuperação, investigue os logs e o estado dos containers antes de prosseguir.

---

## 9. Testes de carga com JMeter

O repositório contém:

```text
Case-Arquiteto-de-Software-Fluxo-de-Caixa.jmx
evidencia-carga-completa-50rps.jtl
```

O plano exercita os endpoints:

```http
POST /api/lancamentos
GET /api/consolidados-diarios/{dataReferencia}
```

Os grupos de carga são executados **sequencialmente**, cada um configurado para uma taxa-alvo de 50 requisições por segundo.

### Configuração manual da autenticação DEV no JMeter

Antes de executar os testes de carga de 50 RPS, abra o arquivo `Case-Arquiteto-de-Software-Fluxo-de-Caixa.jmx` no Apache JMeter.

Configure a aba **Body Data** das duas requisições `POST - Autenticacao DEV`:

1. `Carga - LivroRazao - 50 RPS` → `Autenticacao - Uma vez por usuario` → `POST - Autenticacao DEV`.
2. `Carga - ConsolidadoDiario - 50 RPS` → `Autenticacao - Uma vez por usuario` → `POST - Autenticacao DEV`.

Utilize os valores de `AUTENTICACAO_DEV_USUARIO` e `AUTENTICACAO_DEV_SENHA` definidos no arquivo `.env` da raiz do projeto.

O conteúdo da aba **Body Data** deve seguir este formato:

```json
{
  "usuario": "SEU_USUARIO_DEV",
  "senha": "SUA_SENHA_DEV"
}
```

Substitua `SEU_USUARIO_DEV` e `SUA_SENHA_DEV` pelos valores reais configurados no `.env` antes de executar o teste.

**Importante:** o JMeter não lê automaticamente o arquivo `.env`. A configuração manual deve ser realizada nas duas requisições de autenticação.

**Segurança:** não publique credenciais reais no GitHub. Após a execução dos testes, restaure os marcadores `SEU_USUARIO_DEV` e `SUA_SENHA_DEV` nas duas requisições e salve o arquivo `.jmx` sem as credenciais reais.

### Resultados registrados

| Indicador | Resultado |
|---|---:|
| Total de amostras | 29.746 |
| Lançamentos HTTP 201 | 14.914 |
| Consultas HTTP 200 | 14.632 |
| Autenticações HTTP 200 | 200 |
| Falhas HTTP | 0 |

As janelas estáveis de execução de aproximadamente 210 segundos atingiram cerca de 50 RPS para cada serviço, individualmente.

**Limitação:** o plano não demonstra 50 RPS simultâneos nos dois serviços.

O arquivo `.jtl` contém a evidência registrada da execução.

---

## 10. Persistência e reconciliação

### 10.1. Bancos de dados

A solução utiliza dois bancos PostgreSQL independentes.

**Livro Razão:**

```text
Banco: fluxo_de_caixa

TB_LANCAMENTO
TB_LANCAMENTO_OUTBOX
```

**Consolidado Diário:**

```text
Banco: relatorio_fluxo_de_caixa

TB_HISTORICO_LANCAMENTO
TB_SALDO_CONSOLIDADO_DIARIO
```

### 10.2. Resultado da reconciliação

A validação quantitativa identificou:

| Indicador | Resultado |
|---|---:|
| Lançamentos no Livro Razão | 232.901 |
| Registros no histórico do Consolidado Diário | 232.901 |
| Eventos na Outbox | 232.901 |
| Eventos sem data de publicação | 0 |
| Eventos com erro registrado | 0 |

Os eventos da Outbox estavam com `STATUS = 3`.

### 10.3. Reconciliação por data e tipo

| Data | Tipo | Quantidade | Valor |
|---|---|---:|---:|
| 28/07/2026 | Crédito | 32 | R$ 1.400,00 |
| 19/08/2026 | Crédito | 3 | R$ 400,00 |
| 04/11/2026 | Débito | 188.158 | R$ 18.815.800,00 |
| 05/11/2026 | Débito | 44.708 | R$ 4.470.800,00 |

Os agrupamentos por data e tipo coincidiram entre o Livro Razão e o histórico do Consolidado Diário.

### 10.4. Saldos diários

| Data | Créditos | Débitos | Saldo diário |
|---|---:|---:|---:|
| 28/07/2026 | R$ 1.400,00 | R$ 0,00 | R$ 1.400,00 |
| 19/08/2026 | R$ 400,00 | R$ 0,00 | R$ 400,00 |
| 04/11/2026 | R$ 0,00 | R$ 18.815.800,00 | -R$ 18.815.800,00 |
| 05/11/2026 | R$ 0,00 | R$ 4.470.800,00 | -R$ 4.470.800,00 |

A regra aplicada é:

```text
Saldo Diário = Total de Créditos - Total de Débitos
```

A reconciliação demonstrou consistência quantitativa agregada.

Não substitui uma auditoria individual de todos os `CorrelationId` nem uma prova específica de ausência de duplicidades compensatórias.

---

## 11. Mensageria e resiliência

O Livro Razão utiliza Transactional Outbox para registrar lançamento e evento na mesma transação.

O Worker publica os eventos no RabbitMQ utilizando Publisher Confirm.

O Consolidado Diário consome os eventos de maneira idempotente e atualiza o histórico e a projeção do saldo.

### Tratamento de falhas

A solução contempla:

- Reprocessamento de eventos pendentes na Outbox.
- Recuperação de eventos reservados que excederam o tempo esperado.
- Recuperação automática de conexão RabbitMQ.
- Tentativas de restabelecimento da assinatura com backoff.
- Retry de mensagens.
- Dead Letter Queue.
- Proteção contra efeitos duplicados.

Os logs da validação registraram publicação e consumo de eventos com `CorrelationId` correspondente.

Na verificação do RabbitMQ:

- A fila principal estava sem mensagens pendentes.
- A fila de retry estava sem mensagens pendentes.
- A DLQ estava sem mensagens pendentes.
- Havia um consumidor conectado à fila principal.

### Validação de indisponibilidade e recuperação

Em 05/11/2026, a bateria integrada executou cenários controlados de falha e recuperação, incluindo:

- Interrupção temporária do RabbitMQ.
- Falha transitória na publicação de eventos.
- Reinicialização do broker e recuperação do processamento assíncrono.
- Interrupção temporária do PostgreSQL do Consolidado Diário.
- Recuperação do processamento após o restabelecimento do banco.
- Tratamento de mensagem inválida.
- Verificação de idempotência e concorrência.

Durante a indisponibilidade do RabbitMQ, os logs registraram falha de publicação com `BrokerUnreachableException`, evidenciando que a interrupção foi efetivamente exercitada.

A bateria concluiu com o resultado:

```text
RESULTADO FINAL: BATERIA APROVADA
Total de erros: 0
```

Os containers envolvidos estavam novamente em execução ao término da bateria.

**Conclusão:** os cenários controlados de indisponibilidade e recuperação foram aprovados na execução registrada em 05/11/2026.

Essa evidência comprova o comportamento esperado nos cenários exercitados pela bateria, sem representar garantia absoluta de ausência de perda de eventos em qualquer condição de falha ou operação produtiva.

---

## 12. Datas e fuso horário

O Livro Razão normaliza a data do lançamento para UTC em:

```text
Lancamento.NormalizarDataLancamento
```

O Consolidado Diário determina a data de referência utilizando:

```text
evento.Payload.DataLancamento.UtcDateTime
```

As consultas `SHOW TIMEZONE` realizadas nos dois bancos retornaram `UTC`.

Os saldos consolidados foram reconciliados por data UTC.

A data contábil não deve depender do fuso horário local do servidor.

---

## 13. Cobertura de testes

Os relatórios de cobertura estão disponíveis na raiz do repositório:

- `BuildingBlocks - Summary - Coverage Report.pdf`
- `FluxoDeCaixa - Summary - Coverage Report.pdf`
- `RelatorioFluxoDeCaixa - Summary - Coverage Report.pdf`

Resultados registrados:

| Solução | Cobertura de linhas | Cobertura de ramificações |
|---|---:|---:|
| BuildingBlocks | 34,2% | 44,8% |
| FluxoDeCaixa | 87,0% | 57,4% |
| RelatorioFluxoDeCaixa | 74,1% | 46,2% |

A cobertura de BuildingBlocks apresenta oportunidade de ampliação.

---

## 14. Segurança

A solução implementa:

- Autenticação JWT.
- Autorização funcional.
- Separação das responsabilidades de segurança nos Building Blocks.
- Configuração local de segredos por variáveis de ambiente e .NET User Secrets.

O Docker Compose integrado executa as APIs com perfil `Development` e endpoints HTTP, adequado ao ambiente local do case.

Essa configuração não deve ser considerada pronta para produção.

O gerenciamento de credenciais por variáveis de ambiente ou User Secrets não equivale à criptografia dos dados financeiros em repouso ou em trânsito.

A aplicação efetiva de criptografia desses dados não foi comprovada na inspeção dos fontes.

Para ambientes produtivos, devem ser avaliados os requisitos de TLS, criptografia em repouso, gerenciamento corporativo de segredos e integração com provedor de identidade.

Consulte o ADR-001 para as decisões e evoluções de segurança.

---

## 15. Encerramento do ambiente

### Parar containers preservando os volumes

```powershell
docker compose stop
```

### Remover containers e rede preservando os volumes

```powershell
docker compose down
```

**Não execute `docker compose down -v` se precisar preservar os dados dos bancos e do RabbitMQ.**

---

## 16. Evidências de validação e parecer técnico — 05/11/2026

As evidências registradas na validação do case incluem:

| Verificação | Resultado |
|---|---|
| Compilação | Três soluções compiladas com sucesso |
| Testes automatizados | 251 aprovados, 0 falhas |
| Health Checks | HTTP 200 nas duas APIs |
| Teste de carga | 29.746 amostras, 0 falhas HTTP |
| Livro Razão | 14.914 lançamentos HTTP 201 |
| Consolidado Diário | 14.632 consultas HTTP 200 |
| Autenticação | 200 respostas HTTP 200 |
| Reconciliação | 232.901 lançamentos em cada serviço |
| Outbox | 232.901 eventos publicados |
| Saldos diários | Quatro datas reconciliadas |
| UTC | Normalização implementada e bancos consultados em UTC |
| Bateria integrada | Aprovada, 0 erros |
| Indisponibilidade do RabbitMQ | Cenário controlado executado e aprovado |
| Indisponibilidade do PostgreSQL do relatório | Cenário controlado executado e aprovado |
| Recuperação dos serviços | Containers operacionais ao término da bateria |

### Parecer técnico

**Aprovado no escopo das evidências verificadas.**

A arquitetura demonstra separação de responsabilidades, comunicação assíncrona, persistência transacional, mecanismos de idempotência e tratamento de falhas.

Os testes de carga registrados apresentaram comportamento satisfatório nas taxas individuais exercitadas, sem falhas HTTP.

A reconciliação quantitativa demonstrou correspondência entre os lançamentos registrados e o histórico do Consolidado Diário, além da consistência dos saldos diários.

A bateria integrada executada em 05/11/2026 complementou as evidências anteriores, validando cenários controlados de indisponibilidade e recuperação do RabbitMQ e do PostgreSQL do Consolidado Diário.

O resultado final da bateria foi **APROVADA, com zero erros**, incluindo a recuperação dos serviços envolvidos.

### Ressalvas

As evidências disponíveis não comprovam:

- Execução simultânea de 50 RPS nas duas APIs.
- Reconciliação individual de todos os `CorrelationId`.
- Ausência de duplicidades compensatórias em todos os registros.
- Garantia universal de ausência de perda de eventos sob condições de falha não contempladas pela bateria integrada.

Os cenários controlados de indisponibilidade e recuperação do RabbitMQ foram efetivamente exercitados e aprovados. Portanto, não permanecem como pendência de validação no escopo da bateria executada.

Essas ressalvas delimitam o alcance dos testes registrados e não representam, por si só, falhas funcionais comprovadas.

A atualização documental incorpora os resultados da bateria integrada de 05/11/2026 e não representa uma nova execução dos testes automatizados ou das APIs.

---

## 17. Artefatos do repositório

| Artefato | Finalidade |
|---|---|
| `README.md` | Documentação principal e inicialização integrada |
| `FluxoDeCaixa/README.md` | Documentação do Livro Razão |
| `RelatorioFluxoDeCaixa/README.md` | Documentação do Consolidado Diário |
| `ADR-001-Arquitetura-da-Solucao.md` | Decisões arquiteturais |
| `Case - Arquiteto de Software.drawio` | Desenho da arquitetura |
| `Case - Arquiteto de Software.pdf` | Enunciado do desafio |
| `Bateria de testes.ps1` | Bateria integrada e cenários de exceção |
| `Case-Arquiteto-de-Software-Fluxo-de-Caixa.jmx` | Plano de carga JMeter |
| `evidencia-carga-completa-50rps.jtl` | Evidência de carga |
| Relatórios PDF de cobertura | Evidências de cobertura dos testes |

## 18. Conclusão

A solução implementa o registro de lançamentos financeiros e a consulta do consolidado diário com serviços desacoplados, consistência eventual e persistência transacional.

A combinação de Transactional Outbox, RabbitMQ, Publisher Confirm, idempotência, retry e DLQ atende às necessidades arquiteturais do desafio dentro do escopo implementado.

Os resultados registrados de testes automatizados, carga e reconciliação sustentam a avaliação técnica positiva da solução, observadas as ressalvas documentadas.