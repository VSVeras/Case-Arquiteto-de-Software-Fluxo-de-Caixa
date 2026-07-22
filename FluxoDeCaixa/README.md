# Fluxo de Caixa

## Visão geral

Este projeto implementa a primeira etapa do desafio de Fluxo de Caixa: o serviço responsável pelo registro de lançamentos financeiros de débito e crédito.

O serviço recebe a solicitação do cliente, valida as regras de negócio, registra o lançamento no PostgreSQL e grava o evento correspondente na Transactional Outbox. Um Worker executado em segundo plano processa os eventos pendentes e realiza sua publicação no RabbitMQ de forma assíncrona.

O objetivo desta etapa é garantir que o registro dos lançamentos permaneça disponível mesmo diante da indisponibilidade temporária da infraestrutura de mensageria ou de serviços consumidores.

O serviço de consolidação diária citado no desafio não faz parte desta implementação.

\---

## Escopo implementado

Esta entrega contempla exclusivamente o serviço de registro de lançamentos.

Foram implementadas as seguintes funcionalidades:

* Registro de lançamentos de débito e crédito.
* Consulta de lançamentos por identificador.
* Validação das regras de domínio.
* Idempotência utilizando `CorrelationId`.
* Persistência em PostgreSQL.
* Transactional Outbox.
* Publicação assíncrona de eventos no RabbitMQ.
* Worker para processamento da Outbox.
* Retry automático da publicação de eventos.
* Tratamento centralizado de exceções.
* Testes unitários.
* Testes de integração.
* Testes da API.
* Execução completa utilizando Docker Compose.

\---

## Motivação das decisões arquiteturais

As decisões arquiteturais adotadas neste projeto foram tomadas considerando os requisitos funcionais e não funcionais apresentados no desafio.

Os principais objetivos foram:

* manter o serviço de lançamentos disponível mesmo diante da indisponibilidade de componentes externos;
* evitar perda de eventos;
* garantir consistência entre persistência e publicação de mensagens;
* reduzir o acoplamento entre os serviços;
* facilitar manutenção e evolução da solução;
* permitir testes automatizados em diferentes níveis;
* documentar claramente toda a execução local da aplicação.

O projeto busca demonstrar decisões arquiteturais compatíveis com um ambiente distribuído sem adicionar padrões desnecessários ao escopo desta primeira etapa.

Todas as soluções adotadas possuem relação direta com os requisitos apresentados pelo case.

\---

## Independência entre lançamentos e consolidado diário

O desafio estabelece que o serviço responsável pelo registro dos lançamentos não deve ficar indisponível caso o serviço responsável pelo consolidado diário esteja indisponível.

Por esse motivo, o registro do lançamento não depende de chamadas síncronas para outros serviços.

Após a validação das regras de domínio:

1. O lançamento é persistido no PostgreSQL.
2. O evento correspondente é gravado na Transactional Outbox.
3. Ambos são persistidos na mesma transação.
4. O processamento da publicação ocorre posteriormente de forma assíncrona.

Essa estratégia produz diversos benefícios.

* A indisponibilidade do RabbitMQ não impede o registro do lançamento.
* Uma futura indisponibilidade do serviço de consolidado não afeta a API de lançamentos.
* Os eventos permanecem persistidos até serem publicados com sucesso.
* O tempo de resposta da API permanece reduzido.
* Os componentes permanecem desacoplados.

Essa abordagem permite que cada serviço evolua e escale de forma independente.

\---

## Arquitetura em camadas

A solução foi organizada em camadas para separar claramente as responsabilidades da aplicação.

## Camada de Apresentação

Responsável pela exposição da API HTTP através do ASP.NET Core Minimal API.

Suas responsabilidades incluem:

* recebimento das requisições;
* serialização dos objetos;
* exposição dos endpoints;
* retorno dos códigos HTTP apropriados.

As regras de negócio não são implementadas nesta camada.

\---

## Camada de Aplicação

Responsável por coordenar os casos de uso da aplicação.

Entre suas responsabilidades estão:

* validação das entradas;
* orquestração do fluxo da aplicação;
* coordenação da persistência;
* criação dos eventos da Outbox;
* controle da idempotência.

A camada de aplicação não contém detalhes de infraestrutura.

\---

## Camada de Domínio

Representa o núcleo da aplicação.

Nesta camada encontram-se:

* entidades;
* objetos de valor;
* contratos;
* regras de negócio;
* invariantes do domínio.

O domínio permanece isolado das tecnologias utilizadas pela aplicação.

Essa separação facilita testes, manutenção e evolução do sistema.

\---

## Camada de Infraestrutura

Responsável pelos detalhes técnicos da aplicação.

Entre eles:

* Entity Framework Core;
* PostgreSQL;
* RabbitMQ;
* Transactional Outbox;
* Worker da Outbox;
* Repositórios;
* Persistência;
* Logging;
* Configurações externas.

Alterações na infraestrutura não afetam diretamente o domínio.

\---

## Domain-Driven Design

Os princípios de Domain-Driven Design foram utilizados de forma objetiva, mantendo o foco no problema de negócio apresentado pelo desafio.

O agregado `Lancamento` concentra as regras relacionadas ao registro financeiro e protege suas próprias invariantes.

Quando uma regra de negócio é violada, o domínio lança uma exceção específica, impedindo que objetos inválidos sejam persistidos.

Dessa forma:

* regras permanecem no domínio;
* endpoints não implementam regras de negócio;
* repositórios não validam o domínio;
* detalhes de infraestrutura permanecem desacoplados das regras do negócio.

Essa organização reduz o acoplamento entre as camadas e facilita tanto a manutenção quanto a evolução futura da aplicação.

\---

## Transactional Outbox

## Motivação

Em arquiteturas distribuídas é comum que um mesmo fluxo envolva persistência de dados e publicação de eventos.

Executar essas duas operações de forma independente pode gerar inconsistências.

Por exemplo:

* o lançamento pode ser salvo com sucesso e a publicação da mensagem falhar;
* a mensagem pode ser publicada antes da confirmação da transação;
* uma indisponibilidade temporária do broker pode provocar perda de eventos.

Para eliminar esse problema foi adotado o padrão **Transactional Outbox**.

\---

## Funcionamento

Durante o processamento da requisição são gravados dois registros utilizando a mesma transação do banco de dados:

* o lançamento financeiro;
* o evento da Outbox.

Caso qualquer operação falhe, toda a transação é revertida.

Somente após a confirmação da transação um Worker independente passa a processar os eventos pendentes.

Esse padrão garante consistência entre a base de dados e a mensageria.

\---

## Fluxo da Outbox

```text
Cliente
    |
    v
POST /api/lancamentos
    |
    v
Validação do domínio
    |
    v
BEGIN TRANSACTION
    |
    +------------------------------+
    |                              |
    | Grava TB\_LANCAMENTO          |
    |                              |
    | Grava TB\_LANCAMENTO\_OUTBOX   |
    |                              |
    +------------------------------+
    |
COMMIT
    |
    v
PublicadorOutboxWorker
    |
    v
RabbitMQ
```

\---

## Benefícios

A utilização da Transactional Outbox oferece diversas vantagens.

* Reduz o risco de perda de eventos entre a persistência do lançamento e a publicação no RabbitMQ, pois o evento permanece armazenado na Outbox até ser publicado ou atingir o limite de tentativas.
* Não existe dependência síncrona do RabbitMQ.
* A API permanece disponível mesmo com o broker indisponível.
* A publicação pode ser repetida até obter sucesso.
* A consistência entre banco e mensageria é preservada.
* O acoplamento entre os componentes é reduzido.

\---

## Publicador da Outbox

A publicação dos eventos é realizada por um `BackgroundService` chamado `PublicadorOutboxWorker`.

Sua responsabilidade é localizar eventos pendentes, publicá-los no RabbitMQ e atualizar seu estado.

O Worker executa continuamente enquanto a aplicação estiver em execução.

Embora esteja hospedado no mesmo processo da API, sua responsabilidade permanece completamente separada.

Essa decisão reduz a complexidade do case sem impedir uma futura separação física do componente.

\---

## Fluxo do Worker

```text
Loop

↓

Buscar eventos pendentes

↓

Marcar como PROCESSANDO

↓

Publicar no RabbitMQ

↓

Atualizar para PUBLICADO

↓

Aguardar próximo ciclo
```

\---

## Estados da Outbox

Cada evento percorre um ciclo de vida durante seu processamento.

|Estado|Descrição|
|-|-|
|PENDENTE|Evento aguardando processamento.|
|PROCESSANDO|Evento atualmente sendo publicado.|
|PUBLICADO|Evento publicado com sucesso.|
|ERRO|Número máximo de tentativas atingido.|

A atualização desses estados ocorre de forma transacional, evitando que múltiplas instâncias processem simultaneamente o mesmo evento.

\---

## Concorrência da Outbox

O processamento concorrente utiliza o recurso `FOR UPDATE SKIP LOCKED` do PostgreSQL.

Esse mecanismo permite que múltiplos Workers possam executar simultaneamente sem disputar os mesmos registros.

Quando um Worker seleciona determinado evento, esse registro permanece bloqueado para os demais até o término da transação.

Com isso obtém-se:

* prevenção do processamento concorrente do mesmo evento por múltiplos Workers;
* melhor escalabilidade horizontal;
* menor contenção entre Workers;
* maior eficiência no processamento concorrente.

Essa estratégia já faz parte da implementação atual.

\---

## RabbitMQ

O RabbitMQ é utilizado como broker de mensagens responsável pela distribuição dos eventos produzidos pela aplicação.

Após a publicação bem-sucedida da Outbox, os consumidores podem processar os eventos de forma totalmente desacoplada da API.

Essa abordagem permite que novos serviços sejam adicionados futuramente sem necessidade de alteração no serviço responsável pelos lançamentos.

\---

## Idempotência

A API implementa idempotência utilizando o campo `CorrelationId`.

Cada requisição deve possuir um identificador único informado pelo cliente.

Durante o processamento são possíveis três cenários.

## Primeira solicitação

O lançamento é criado normalmente.

Resposta:

```text
HTTP 201 Created
```

\---

## Reenvio da mesma requisição

Quando o mesmo `CorrelationId` é recebido novamente e o conteúdo da requisição permanece idêntico, nenhum novo lançamento é criado.

A API retorna o lançamento já existente.

Resposta:

```text
HTTP 200 OK
```

\---

## Mesmo CorrelationId com conteúdo diferente

Caso seja reutilizado um `CorrelationId` com dados diferentes dos originalmente persistidos, a operação é rejeitada.

Resposta:

```text
HTTP 409 Conflict
```

Essa estratégia evita registros duplicados e garante comportamento previsível mesmo diante de falhas de comunicação ou repetição de requisições pelo cliente.

\---

## Unit of Work

Todas as alterações realizadas durante o processamento do lançamento são coordenadas através do padrão Unit of Work.

Esse padrão garante que:

* lançamento;
* evento da Outbox;
* alterações de estado;

sejam persistidos de forma atômica.

Caso qualquer operação falhe, nenhuma alteração é confirmada.

Essa estratégia preserva a consistência dos dados mesmo diante de exceções inesperadas.

\---

## Tratamento de exceções

A aplicação possui tratamento centralizado de exceções.

As exceções de domínio são convertidas em respostas HTTP apropriadas, enquanto falhas inesperadas são registradas através da abstração de logging.

Essa abordagem evita duplicação de código nos endpoints e padroniza as respostas da API.

Além disso, todos os erros relevantes são registrados para facilitar auditoria e diagnóstico operacional.

\---

## Escalabilidade

A arquitetura foi construída considerando a possibilidade de crescimento da aplicação sem exigir mudanças significativas nas regras de negócio.

Os componentes permanecem desacoplados, permitindo que evoluam de forma independente.

A API possui responsabilidade exclusiva pelo registro dos lançamentos, enquanto o processamento da Outbox ocorre de forma assíncrona.

Essa separação reduz o tempo de resposta das requisições e evita que indisponibilidades temporárias da infraestrutura afetem a operação principal.

Em um ambiente de produção, o Worker responsável pela Outbox pode ser extraído para um processo independente, possibilitando escalabilidade horizontal específica para o processamento dos eventos.

\---

## Resiliência

A aplicação adota diferentes mecanismos para aumentar sua tolerância a falhas.

Entre eles destacam-se:

* Transactional Outbox;
* Retry de publicação;
* Persistência transacional;
* Idempotência;
* Processamento assíncrono;
* Separação entre API e mensageria.

Esses mecanismos permitem que falhas temporárias sejam tratadas automaticamente sem perda de informações.

\---

## Segurança

Embora segurança não seja o foco principal deste desafio, a solução foi organizada para permitir sua evolução sem alterações significativas na arquitetura.

Entre as evoluções previstas encontram-se:

* autenticação utilizando JWT;
* autorização baseada em perfis;
* criptografia de dados sensíveis;
* auditoria das operações;
* rastreabilidade completa através do CorrelationId.

A separação entre as camadas facilita a inclusão desses mecanismos futuramente.

\---

## Testabilidade

Toda a solução foi desenvolvida priorizando testabilidade.

As dependências são abstraídas através de interfaces, permitindo utilização de objetos simulados durante os testes unitários.

A separação entre domínio, aplicação e infraestrutura permite validar regras de negócio sem necessidade de banco de dados ou infraestrutura externa.

Além dos testes unitários, foram implementados testes de integração para validar o comportamento completo da aplicação.

\---

## Arquitetura da solução

```text
                    +----------------------+
                    |      Cliente         |
                    +----------+-----------+
                               |
                               |
                               v
                    +----------------------+
                    |     Minimal API      |
                    +----------+-----------+
                               |
                               |
                               v
                    +----------------------+
                    |     Aplicação        |
                    +----------+-----------+
                               |
                               |
                               v
                    +----------------------+
                    |       Domínio        |
                    +----------+-----------+
                               |
                               |
                               v
                    +----------------------+
                    |   Infraestrutura     |
                    +----------+-----------+
                               |
         +---------------------+----------------------+
         |                                            |
         |                                            |
         v                                            v
+----------------------+                  +----------------------+
|     PostgreSQL       |                  |      RabbitMQ        |
+----------+-----------+                  +----------------------+
           |
           |
           v
+----------------------+
| Transactional Outbox |
+----------+-----------+
           |
           |
           v
+----------------------+
| PublicadorOutboxWorker |
+----------------------+
```

\---

## Estrutura da solução

```text
FluxoDeCaixa
│
├── src
│   └── LivroRazao
│       ├── Aplicacao
│       ├── Dominio
│       ├── Infraestrutura
│       ├── IoC
│       └── Rest
│
├── tests
│   ├── LivroRazao.TesteDeUnidade
│   └── LivroRazao.TesteDeIntegracao
│
├── docker
│
├── docker-compose.yml
│
└── FluxoDeCaixa.sln
```

\---

## Tecnologias utilizadas

A solução foi desenvolvida utilizando as seguintes tecnologias.

* .NET 8
* ASP.NET Core Minimal API
* C#
* Entity Framework Core
* PostgreSQL
* RabbitMQ
* FluentValidation
* xUnit
* Moq
* Testcontainers
* Docker
* Docker Compose
* Swagger / OpenAPI
* Health Checks

\---

## Pré-requisitos

Antes de executar o projeto é necessário possuir os seguintes componentes instalados.

* .NET SDK 8
* Docker Desktop
* Docker Compose
* Visual Studio 2022 ou superior

\---

## Organização do projeto

A solução encontra-se organizada de acordo com as responsabilidades de cada camada.

## Aplicacao

Contém os casos de uso responsáveis por coordenar o fluxo da aplicação.

\---

## Dominio

Contém entidades, contratos, objetos de valor e regras de negócio.

\---

## Infraestrutura

Contém implementações relacionadas à persistência, mensageria, Outbox, logging e acesso ao banco de dados.

\---

## IoC

Centraliza o registro das dependências utilizadas pela aplicação.

\---

## Rest

Contém os endpoints da Minimal API responsáveis pela comunicação HTTP.

\---

## Dependências externas

Durante a execução da aplicação são utilizados apenas dois componentes de infraestrutura.

## PostgreSQL

Responsável pela persistência dos lançamentos e dos eventos da Outbox.

\---

## RabbitMQ

Responsável pela publicação assíncrona dos eventos produzidos pela aplicação.

\---

## Considerações arquiteturais

Embora esta entrega implemente apenas o serviço de registro de lançamentos, a arquitetura foi construída para permitir evolução gradual.

Novos consumidores poderão ser adicionados futuramente sem necessidade de alteração da API.

Da mesma forma, o Worker responsável pela Outbox poderá ser executado em um processo independente caso o volume de eventos justifique essa separação.

Essa abordagem mantém a solução simples para o escopo do desafio, preservando ao mesmo tempo características importantes de arquiteturas distribuídas como baixo acoplamento, consistência, escalabilidade e facilidade de manutenção.

\---

## Executando com Docker

A forma mais simples de executar a aplicação é utilizando o Docker Compose.

Na raiz da solução execute:

```bash
docker compose up --build
```

Na primeira execução as imagens serão construídas automaticamente.

Após a conclusão da inicialização estarão disponíveis os seguintes serviços:

|Serviço|Endereço|
|-|-|
|API|http://localhost:8080|
|Swagger|http://localhost:8080/swagger|
|Health Check|http://localhost:8080/health|
|RabbitMQ Management|http://localhost:15672|
|PostgreSQL|localhost:5432|

\---

## Credenciais locais

## PostgreSQL

```text
Host........: localhost
Port........: 5432
Database....: fluxo\_de\_caixa
User........: postgres
Password....: postgres
```

\---

## RabbitMQ

```text
URL.........: http://localhost:15672

User........: guest
Password....: guest
```

\---

## Containers executados

Após a inicialização do ambiente os seguintes containers deverão estar em execução.

* PostgreSQL
* RabbitMQ
* Migrations
* LivroRazao

Verifique utilizando:

```bash
docker ps
```

\---

## Executando pelo Visual Studio

Também é possível executar o projeto diretamente pelo Visual Studio.

Primeiramente inicie apenas a infraestrutura.

```bash
docker compose up -d postgres rabbitmq
```

Abra a solução:

```text
FluxoDeCaixa.sln
```

Defina o projeto **LivroRazao** como Startup Project.

Execute normalmente utilizando o Visual Studio.

\---

## Migrations

O projeto utiliza Entity Framework Core para gerenciamento do banco de dados.

Quando executado através do Docker Compose, as migrations são aplicadas automaticamente pelo container **migrations**.

A API **não** executa migrations durante sua inicialização.

Essa abordagem evita concorrência entre múltiplas instâncias da aplicação e mantém a responsabilidade de criação da estrutura do banco em um componente específico.

\---

## Executando migrations manualmente

Caso deseje executar as migrations manualmente:

```powershell
Update-Database
```

Ou utilizando a CLI do .NET:

```bash
dotnet ef database update
```

\---

## Criando uma nova migration

No Package Manager Console:

```powershell
Add-Migration NomeDaMigration `
-OutputDir Infraestrutura\\Migracao `
-Namespace LivroRazao.Infraestrutura.Migracao
```

Ou utilizando a CLI:

```bash
dotnet ef migrations add NomeDaMigration
```

Após criada, a migration poderá ser aplicada normalmente utilizando o container de migrations ou os comandos do Entity Framework.

\---

## Validando as migrations

Após a criação da estrutura do banco execute:

```sql
SELECT \*
FROM "\_\_EFMigrationsHistory";
```

A migration criada deverá aparecer registrada.

\---

## Testando a idempotência das migrations

Uma característica importante do Entity Framework Core é que as migrations são idempotentes.

Para validar esse comportamento execute novamente:

```bash
docker compose up
```

ou

```powershell
Update-Database
```

Nenhuma alteração adicional deverá ser realizada no banco.

A tabela `\_\_EFMigrationsHistory` permanecerá contendo apenas uma entrada para cada migration aplicada.

\---

## Swagger

Após a aplicação estar em execução acesse:

```text
http://localhost:8080/swagger
```

Toda a documentação da API estará disponível através da interface do Swagger.

Os endpoints podem ser executados diretamente pelo navegador.

\---

## Health Check

O endpoint de monitoramento pode ser acessado através de:

```text
http://localhost:8080/health
```

Esse endpoint informa se a aplicação está operacional.

\---

## PostgreSQL

Para acessar o banco utilizando linha de comando:

```bash
docker exec -it postgres psql -U postgres -d fluxo\_de\_caixa
```

Algumas consultas úteis.

Listar tabelas:

```sql
\\dt
```

Consultar lançamentos:

```sql
SELECT \*
FROM TB\_LANCAMENTO;
```

Consultar eventos da Outbox:

```sql
SELECT \*
FROM TB\_LANCAMENTO\_OUTBOX;
```

Consultar histórico das migrations:

```sql
SELECT \*
FROM "\_\_EFMigrationsHistory";
```

\---

## RabbitMQ

A interface administrativa pode ser acessada em:

```text
http://localhost:15672
```

Credenciais:

```text
guest
guest
```

Através dessa interface é possível:

* visualizar filas;
* acompanhar mensagens;
* validar exchanges;
* verificar consumidores;
* monitorar publicações.

\---

## Reconstruindo completamente o ambiente

Caso seja necessário reconstruir completamente o ambiente execute:

```bash
docker compose down
```

Depois remova os volumes.

```bash
docker compose down -v
```

Reconstrua todas as imagens.

```bash
docker compose build --no-cache
```

Inicie novamente.

```bash
docker compose up
```

Esse procedimento garante um ambiente completamente limpo para novos testes.

\---

## Testes

A solução possui testes automatizados em diferentes níveis para validar tanto as regras de negócio quanto a integração entre os componentes.

Foram implementados:

* Testes unitários;
* Testes de integração;
* Testes da API;
* Testes da persistência;
* Testes da Transactional Outbox.

Os testes procuram validar os principais cenários de sucesso, falha e idempotência.

\---

## Executando os testes

Todos os testes podem ser executados através do comando:

```bash
dotnet test
```

\---

## Coletando cobertura

Para gerar a cobertura de código execute:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Ao término da execução serão gerados arquivos `coverage.cobertura.xml`.

\---

## Instalando o ReportGenerator

Caso ainda não esteja instalado:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

\---

## Gerando o relatório HTML

```bash
reportgenerator \\
-reports:"tests/\*\*/coverage.cobertura.xml" \\
-targetdir:"CoverageReport" \\
-reporttypes:Html
```

Após a geração abra:

```text
CoverageReport/index.html
```

O relatório apresentará informações como:

* cobertura por projeto;
* cobertura por classe;
* cobertura por método;
* linhas cobertas;
* linhas não cobertas;
* cobertura de branches.

\---

## Estratégia de testes

Os testes foram organizados buscando validar cada camada da aplicação de forma independente.

## Testes unitários

Validam:

* entidades;
* regras de domínio;
* validações;
* casos de uso;
* idempotência;
* tratamento de exceções.

Os testes unitários não dependem de infraestrutura externa.

\---

## Testes de integração

Validam o comportamento da aplicação utilizando infraestrutura real.

São utilizados:

* PostgreSQL;
* RabbitMQ;
* Testcontainers.

Esses testes verificam o comportamento completo dos componentes integrados.

\---

## Testes da API

Validam:

* códigos HTTP;
* contratos da API;
* serialização;
* validações;
* respostas produzidas pelos endpoints.

\---

## Testcontainers

Os testes de integração utilizam Testcontainers.

Dessa forma, cada execução cria um ambiente isolado contendo apenas os recursos necessários para aquele conjunto de testes.

Essa abordagem reduz interferências entre execuções e aproxima os testes do ambiente real.

\---

## Testando o projeto do zero

O procedimento abaixo permite validar completamente a aplicação em um ambiente limpo.

## 1\. Remover containers

```bash
docker compose down
```

\---

## 2\. Remover volumes

```bash
docker compose down -v
```

\---

## 3\. Reconstruir as imagens

```bash
docker compose build --no-cache
```

\---

## 4\. Iniciar a aplicação

```bash
docker compose up
```

\---

## 5\. Confirmar os containers

```bash
docker ps
```

Todos os containers esperados devem estar em execução.

\---

## 6\. Acessar o Swagger

```text
http://localhost:8080/swagger
```

\---

## 7\. Criar um lançamento

Execute um POST utilizando um `CorrelationId` novo.

A resposta esperada é:

```text
HTTP 201 Created
```

\---

## 8\. Repetir exatamente a mesma requisição

A resposta deverá ser:

```text
HTTP 200 OK
```

Nenhum novo lançamento deverá ser criado.

\---

## 9\. Alterar o payload mantendo o mesmo CorrelationId

A resposta deverá ser:

```text
HTTP 409 Conflict
```

Essa validação comprova o funcionamento da idempotência.

\---

## 10\. Consultar o banco

```sql
SELECT \*
FROM TB\_LANCAMENTO;
```

Verifique se apenas um lançamento foi persistido.

\---

## 11\. Consultar a Outbox

```sql
SELECT \*
FROM TB\_LANCAMENTO\_OUTBOX;
```

Verifique se existe um registro correspondente ao lançamento na tabela TB_LANCAMENTO_OUTBOX e observe o valor da coluna STATUS.

\---

## 12\. Validar publicação

Acesse o RabbitMQ Management.

Caso o STATUS seja PUBLICADO, confirme no RabbitMQ que a mensagem foi publicada.

Caso o STATUS ainda seja PENDENTE ou PROCESSANDO, aguarde o processamento do Worker e consulte novamente.

Caso o STATUS seja ERRO, verifique a quantidade de tentativas e a última mensagem de erro registrada.

\---

## 13\. Executar todos os testes

```bash
dotnet test
```

Todos os testes devem finalizar com sucesso.

\---

## 14\. Gerar cobertura

```bash
dotnet test --collect:"XPlat Code Coverage"
```

\---

## 15\. Gerar relatório HTML

```bash
reportgenerator \\
-reports:"tests/\*\*/coverage.cobertura.xml" \\
-targetdir:"CoverageReport" \\
-reporttypes:Html
```

\---

## Organização dos testes

A solução encontra-se organizada da seguinte forma:

```text
tests
│
├── LivroRazao.TesteDeUnidade
│
└── LivroRazao.TesteDeIntegracao
```

Cada projeto possui responsabilidades bem definidas, facilitando manutenção e evolução dos testes.

\---

## Benefícios da estratégia adotada

A combinação de testes unitários e testes de integração permite validar tanto as regras de negócio quanto o comportamento da aplicação utilizando infraestrutura real.

Essa abordagem aumenta a confiabilidade da solução e reduz significativamente a probabilidade de regressões durante futuras evoluções.

\---

## Utilizando a API

A API expõe endpoints REST para registro e consulta de lançamentos.

Toda comunicação utiliza JSON.

\---

## Registrar lançamento

## Requisição

```http
POST /api/lancamentos
Content-Type: application/json
```

Exemplo:

```json
{
  "correlationId": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
  "tipo": 2,
  "dataLancamento": "2026-07-19T15:00:00Z",
  "valor": 150.50,
  "descricao": "Recebimento"
}
```

\---

## Campos

|Campo|Obrigatório|Descrição|
|-|-|-|
|correlationId|Sim|Identificador único da requisição.|
|tipo|Sim|Tipo do lançamento.|
|dataLancamento|Sim|Data do lançamento.|
|valor|Sim|Valor financeiro.|
|descricao|Sim|Descrição do lançamento.|

\---

## Tipos de lançamento

|Valor|Descrição|
|-|-|
|1|Débito|
|2|Crédito|

\---

## Respostas

### Lançamento criado

```http
HTTP/1.1 201 Created
```

\---

### Requisição idempotente

```http
HTTP/1.1 200 OK
```

\---

### CorrelationId reutilizado com payload diferente

```http
HTTP/1.1 409 Conflict
```

\---

### Requisição inválida

```http
HTTP/1.1 400 Bad Request
```

\---

## Consultar lançamento

```http
GET /api/lancamentos/{id}
```

\---

## Respostas

### Encontrado

```http
HTTP/1.1 200 OK
```

\---

### Não encontrado

```http
HTTP/1.1 404 Not Found
```

\---

## Fluxo completo

```text
Cliente

↓

POST /api/lancamentos

↓

Validação

↓

Domínio

↓

Persistência

↓

Transactional Outbox

↓

Commit

↓

Resposta HTTP

↓

PublicadorOutboxWorker

↓

RabbitMQ
```

\---

## Registro de eventos

A aplicação utiliza a abstração `IRegistroDeEventos` para centralizar o registro de informações operacionais.

A implementação atual utiliza `ILogger<T>` da Microsoft.

Essa abordagem permite substituir o mecanismo de logging sem alterar as regras de negócio ou os casos de uso.

Os principais componentes registram eventos durante sua execução, incluindo:

* endpoints;
* casos de uso;
* repositórios;
* publicador RabbitMQ;
* Worker da Outbox;
* serviços de infraestrutura.

\---

## Retry da Outbox

Caso a publicação de um evento falhe, o Worker realiza novas tentativas automaticamente.

Cada tentativa executa as seguintes ações:

1. altera o estado do evento para PROCESSANDO;
2. tenta publicar o evento no RabbitMQ;
3. em caso de sucesso, altera o estado para PUBLICADO;
4. em caso de falha, incrementa o número de tentativas, registra a data da tentativa e a última mensagem de erro;
5. se ainda houver tentativas disponíveis, retorna o evento para PENDENTE;
6. caso o limite de tentativas seja atingido, altera o estado para ERRO.

Essa estratégia permite recuperar automaticamente falhas temporárias da infraestrutura.

\---

## Fluxo do Retry

```text
Evento pendente

↓

Publicação

↓

Sucesso?

├──────────────► SIM

│                  │

│                  ▼

│            PUBLICADO

│

└────► NÃO

         │

         ▼

Incrementa Tentativas

         │

         ▼

Limite atingido?

      │

      ├────► NÃO

      │

      ▼

PENDENTE

      │

      ▼

Nova tentativa

      │

      └────► SIM

                │

                ▼

              ERRO
```

\---

## Acompanhando a Outbox

Consultar todos os eventos:

```sql
SELECT \*
FROM TB\_LANCAMENTO\_OUTBOX;
```

\---

Consultar apenas eventos pendentes:

```sql
SELECT \*
FROM TB\_LANCAMENTO\_OUTBOX
WHERE STATUS = 'PENDENTE';
```

\---

Consultar eventos publicados:

```sql
SELECT \*
FROM TB\_LANCAMENTO\_OUTBOX
WHERE STATUS = 'PUBLICADO';
```

\---

Consultar eventos com erro:

```sql
SELECT \*
FROM TB\_LANCAMENTO\_OUTBOX
WHERE STATUS = 'ERRO';
```

\---

Consultar quantidade por estado:

```sql
SELECT
    STATUS,
    COUNT(\*)
FROM TB\_LANCAMENTO\_OUTBOX
GROUP BY STATUS;
```

\---

## Simulando falha de publicação

Uma forma simples de validar o Retry consiste em interromper temporariamente o RabbitMQ.

```bash
docker stop rabbitmq
```

Realize um novo POST.

O lançamento será persistido normalmente.

O evento permanecerá na Outbox aguardando publicação.

Após iniciar novamente o RabbitMQ:

```bash
docker start rabbitmq
```

O Worker continuará o processamento automaticamente.

Não é necessário reenviar a requisição.

\---

## Observabilidade

A aplicação registra informações suficientes para permitir rastreamento completo de uma requisição.

O `CorrelationId` acompanha todo o fluxo:

```text
Cliente

↓

API

↓

Domínio

↓

Banco de Dados

↓

Transactional Outbox

↓

Worker

↓

RabbitMQ
```

Esse identificador facilita auditoria, troubleshooting e análise de incidentes.

\---

## Evolução da arquitetura

Nesta entrega o `PublicadorOutboxWorker` é executado como um `BackgroundService` hospedado junto à API.

Essa decisão reduz a complexidade da solução sem comprometer sua arquitetura.

Em um ambiente de produção, esse componente poderá ser extraído para um Worker Service independente.

Essa mudança permitirá:

* escalabilidade independente;
* implantação independente;
* isolamento de falhas;
* atualização do publicador sem interromper a API;
* dimensionamento específico para o volume da Outbox.

Como toda a comunicação ocorre através da persistência compartilhada da Outbox, essa evolução exige apenas alterações de infraestrutura, preservando o domínio e os casos de uso.

\---

## Limitações desta implementação

Esta implementação foi desenvolvida considerando exclusivamente o escopo da primeira etapa do desafio.

Algumas funcionalidades normalmente presentes em ambientes de produção não fazem parte desta entrega, seja por estarem fora do escopo proposto, seja para manter o projeto objetivo e focado nas decisões arquiteturais mais relevantes.

Entre elas destacam-se:

* serviço de consolidação diária;
* autenticação e autorização;
* observabilidade distribuída;
* métricas;
* dashboards operacionais;
* pipeline de CI/CD;
* deploy em ambiente Kubernetes;
* versionamento de eventos;
* múltiplos consumidores.

A arquitetura foi organizada para permitir a inclusão dessas funcionalidades sem alterações significativas nas regras de negócio.

\---

## Próximas evoluções

Embora não façam parte desta entrega, as seguintes melhorias podem ser adicionadas futuramente.

## Worker dedicado

Extração do `PublicadorOutboxWorker` para um Worker Service independente.

Benefícios:

* escalabilidade horizontal;
* isolamento de falhas;
* implantação independente;
* redução do consumo de recursos da API.

\---

## Backoff exponencial

Atualmente o Worker executa novas tentativas de publicação respeitando o intervalo configurado.

Como evolução, poderá ser adotado backoff exponencial para reduzir pressão sobre componentes temporariamente indisponíveis.

\---

## Consumer Worker

Criação de um consumidor responsável por receber os eventos publicados no RabbitMQ.

Essa separação permitirá que novos serviços sejam adicionados sem alterações na API.

\---

## API de saldo consolidado

Implementação do serviço responsável pelo cálculo do saldo diário previsto pelo desafio.

Esse serviço poderá consumir os eventos produzidos pelo registro de lançamentos.

\---

## Dead Letter Queue

Como evolução futura, poderá ser configurada uma Dead Letter Exchange com uma fila dedicada para mensagens que não puderem ser processadas pelos consumidores.

Essa estratégia permitirá isolar mensagens problemáticas, facilitar sua inspeção e apoiar processos controlados de reprocessamento.

A implementação atual não utiliza Dead Letter Queue.

\---

## OpenTelemetry

Instrumentação completa da aplicação utilizando OpenTelemetry para coleta de:

* traces;
* métricas;
* logs.

\---

## Segurança

Implementação de:

* JWT;
* OAuth2;
* autorização baseada em papéis;
* auditoria das operações.

\---

## Pipeline CI/CD

Automação de:

* build;
* testes;
* análise estática;
* publicação;
* deploy.

\---

## Troubleshooting

## A API não inicia

Verifique se as portas utilizadas encontram-se disponíveis.

```bash
docker ps
```

\---

## O PostgreSQL não responde

Verifique se o container está em execução.

```bash
docker ps
```

Caso necessário:

```bash
docker restart postgres
```

\---

## RabbitMQ indisponível

Verifique:

```bash
docker ps
```

Reinicie o serviço.

```bash
docker restart rabbitmq
```

\---

## Swagger não abre

Confirme que a aplicação foi inicializada corretamente.

Verifique os logs.

```bash
docker logs livrorazao
```

\---

## Migration não aplicada

Consulte:

```sql
SELECT \*
FROM "\_\_EFMigrationsHistory";
```

Caso necessário execute novamente.

```powershell
Update-Database
```

ou

```bash
docker compose up migrations
```

\---

## Eventos permanecem pendentes

Consultar:

```sql
SELECT \*
FROM TB\_LANCAMENTO\_OUTBOX
WHERE STATUS='PENDENTE';
```

Verifique:

* RabbitMQ;
* Worker;
* logs da aplicação.

\---

## Eventos com erro

Consultar:

```sql
SELECT \*
FROM TB\_LANCAMENTO\_OUTBOX
WHERE STATUS='ERRO';
```

Verifique:

* quantidade de tentativas;
* último erro registrado;
* disponibilidade do RabbitMQ.

\---

## Testes falhando

Confirme que o Docker Desktop encontra-se em execução.

Execute novamente:

```bash
dotnet test
```

\---

## Comandos úteis

## Restaurar dependências

```bash
dotnet restore
```

\---

## Compilar

```bash
dotnet build
```

\---

## Executar

```bash
dotnet run
```

\---

## Executar testes

```bash
dotnet test
```

\---

## Executar testes com cobertura

```bash
dotnet test --collect:"XPlat Code Coverage"
```

\---

## Gerar relatório HTML

```bash
reportgenerator \\
-reports:"tests/\*\*/coverage.cobertura.xml" \\
-targetdir:"CoverageReport" \\
-reporttypes:Html
```

\---

## Subir ambiente

```bash
docker compose up
```

\---

## Reconstruir ambiente

```bash
docker compose down -v
docker compose build --no-cache
docker compose up
```

\---

## Visualizar containers

```bash
docker ps
```

\---

## Visualizar logs

```bash
docker logs livrorazao
```

\---

## Acessar PostgreSQL

```bash
docker exec -it postgres psql -U postgres -d fluxo\_de\_caixa
```

\---

## Considerações finais

Este projeto demonstra a implementação de um serviço de registro de lançamentos utilizando princípios modernos de arquitetura de software.

Durante o desenvolvimento foram priorizados aspectos como:

* separação de responsabilidades;
* consistência transacional;
* desacoplamento entre componentes;
* idempotência;
* resiliência;
* testabilidade;
* simplicidade de manutenção.

A utilização do padrão Transactional Outbox garante consistência entre persistência e mensageria, enquanto o processamento assíncrono permite que a API permaneça disponível mesmo diante de falhas temporárias da infraestrutura.

Embora esta entrega implemente apenas a primeira etapa do desafio, a arquitetura foi organizada para permitir evolução gradual sem necessidade de alterações significativas nas regras de negócio.

\---

## Licença

Este projeto foi desenvolvido exclusivamente para fins de avaliação técnica e demonstração de conhecimentos em arquitetura de software, .NET, Domain-Driven Design e integração assíncrona utilizando o padrão Transactional Outbox.

