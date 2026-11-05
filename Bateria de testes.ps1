# ********************************************
# ********************************************
# ***** ROTEIRO PARA EXECUÇÃO DOS TESTES *****
# ********************************************
# ********************************************

& {

# =====================================================================
# BATERIA INTEGRADA COMPLETA
# FLUXODECAIXA + RELATORIOFLUXODECAIXA
# BUILDINGBLOCKS.SEGURANCA + BUILDINGBLOCKS.MENSAGERIA
# OUTBOX + RETRY + DLQ + RESILIENCIA + CONCORRENCIA
# =====================================================================

$ErrorActionPreference = "Continue"

$script:QuantidadeErros = 0
$script:ErrosEncontrados = [System.Collections.Generic.List[string]]::new()

# =====================================================================
# CONFIGURACAO
# =====================================================================

$baseUrlFluxo = "http://localhost:8080"
$baseUrlRelatorio = "http://localhost:8081"
$rabbitManagementUrl = "http://localhost:15672"

$servicoPostgresFluxo = "postgres-fluxo"
$servicoPostgresRelatorio = "postgres-relatorio"
$servicoRabbitMq = "rabbitmq"
$servicoLivroRazao = "livro-razao"
$servicoConsolidado = "consolidado-diario"

$bancoFluxo = "fluxo_de_caixa"
$bancoRelatorio = "relatorio_fluxo_de_caixa"

$exchangePrincipal = "fluxocaixa.eventos"
$exchangeRetry = "fluxocaixa.eventos.retry"
$exchangeDlx = "fluxocaixa.eventos.dlx"

$filaPrincipal = "fluxocaixa.lancamento.criado"
$filaRetry = "fluxocaixa.lancamento.criado.retry"
$filaDlq = "fluxocaixa.lancamento.criado.dlq"

$routingKeyPrincipal = "lancamento.criado"
$routingKeyDlq = "lancamento.criado.dlq"

$dataTeste = "2026-07-28"

$correlationNormal = [Guid]::NewGuid().ToString()
$correlationRabbitDown = [Guid]::NewGuid().ToString()
$correlationPostgresDown = [Guid]::NewGuid().ToString()
$correlationInvalido = [Guid]::NewGuid().ToString()
$correlationConcorrencia = [Guid]::NewGuid().ToString()

# =====================================================================
# FUNCOES
# =====================================================================

function Ok {
    param([string]$Mensagem)
    Write-Host "[OK] $Mensagem" -ForegroundColor Green
}

function Erro {
    param([string]$Mensagem)
    $script:QuantidadeErros++
    $script:ErrosEncontrados.Add($Mensagem)
    Write-Host "[ERRO] $Mensagem" -ForegroundColor Red
}

function Info {
    param([string]$Mensagem)
    Write-Host "[INFO] $Mensagem" -ForegroundColor Yellow
}

function Titulo {
    param(
        [int]$Numero,
        [string]$Descricao
    )

    Write-Host ""
    Write-Host "============================================================"
    Write-Host ("TESTE {0:D2} - {1}" -f $Numero, $Descricao)
    Write-Host "============================================================"
}

function Ler-Env {
    param([string]$Nome)
    return [Environment]::GetEnvironmentVariable($Nome, "Process")
}

function Carregar-EnvLocal {
    $arquivoEnv = Join-Path $PSScriptRoot ".env"

    if (-not (Test-Path -LiteralPath $arquivoEnv -PathType Leaf)) {
        throw "Arquivo .env nao encontrado: $arquivoEnv"
    }

    $variaveisObrigatorias = @(
        "POSTGRES_USER",
        "POSTGRES_PASSWORD",
        "RABBITMQ_USER",
        "RABBITMQ_PASSWORD",
        "RABBITMQ_ERLANG_COOKIE",
        "JWT_CHAVE",
        "AUTENTICACAO_DEV_USUARIO",
        "AUTENTICACAO_DEV_SENHA"
    )

    $valores = @{}

    foreach ($linha in [IO.File]::ReadAllLines($arquivoEnv)) {
        $texto = $linha.Trim()

        if ([string]::IsNullOrWhiteSpace($texto) -or
            $texto.StartsWith("#")) {
            continue
        }

        if ($texto -notmatch '^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') {
            throw "Formato invalido no arquivo .env."
        }

        $nome = $matches[1]
        $valor = $matches[2].Trim()

        if ($valor.Length -ge 2) {
            if (($valor.StartsWith('"') -and $valor.EndsWith('"')) -or
                ($valor.StartsWith("'") -and $valor.EndsWith("'"))) {
                $valor = $valor.Substring(1, $valor.Length - 2)
            }
        }

        $valores[$nome] = $valor
    }

    foreach ($nome in $variaveisObrigatorias) {
        if (-not $valores.ContainsKey($nome) -or
            [string]::IsNullOrWhiteSpace($valores[$nome])) {
            throw "Variavel obrigatoria ausente no .env: $nome"
        }
    }

    foreach ($nome in $variaveisObrigatorias) {
        [Environment]::SetEnvironmentVariable(
            $nome,
            $valores[$nome],
            "Process"
        )
    }

    Write-Host "[OK] Configuracoes carregadas do .env local." -ForegroundColor Green
}

Carregar-EnvLocal
$postgresUsuario = Ler-Env "POSTGRES_USER"
$rabbitUsuario = Ler-Env "RABBITMQ_USER"
$rabbitSenha = Ler-Env "RABBITMQ_PASSWORD"
$jwtChaveEnv = Ler-Env "JWT_CHAVE"
$autenticacaoDevUsuario = Ler-Env "AUTENTICACAO_DEV_USUARIO"
$autenticacaoDevSenha = Ler-Env "AUTENTICACAO_DEV_SENHA"

$credencialRabbit =
    [Convert]::ToBase64String(
        [Text.Encoding]::ASCII.GetBytes(
            "${rabbitUsuario}:${rabbitSenha}"
        )
    )

function Aguardar-RabbitManagement {

    param(
        [int]$Tentativas = 60,
        [int]$IntervaloSegundos = 2
    )

    for ($i = 1; $i -le $Tentativas; $i++) {

        try {

            $resultado =
                Invoke-RestMethod `
                    -Method Get `
                    -Uri "$rabbitManagementUrl/api/overview" `
                    -Headers @{
                        Authorization = "Basic $credencialRabbit"
                    } `
                    -TimeoutSec 5

            if ($resultado.rabbitmq_version) {
                return $true
            }
        }
        catch {
        }

        Start-Sleep -Seconds $IntervaloSegundos
    }

    return $false
}

function Obter-FilaRabbit {

    param([string]$NomeFila)

    Invoke-RestMethod `
        -Method Get `
        -Uri "$rabbitManagementUrl/api/queues/%2F/$NomeFila" `
        -Headers @{
            Authorization = "Basic $credencialRabbit"
        } `
        -TimeoutSec 10
}

function Limpar-FilaRabbit {

    param([string]$NomeFila)

    try {

        Invoke-RestMethod `
            -Method Delete `
            -Uri "$rabbitManagementUrl/api/queues/%2F/$NomeFila/contents" `
            -Headers @{
                Authorization = "Basic $credencialRabbit"
            } `
            -TimeoutSec 10 | Out-Null

        Ok "Fila limpa: $NomeFila"
    }
    catch {
        Erro "Nao foi possivel limpar a fila $NomeFila."
    }
}

function Aguardar-Postgres {

    param(
        [string]$Servico,
        [string]$Banco,
        [int]$Tentativas = 40
    )

    for ($i = 1; $i -le $Tentativas; $i++) {

        docker compose exec -T $Servico `
            pg_isready `
            -U $postgresUsuario `
            -d $Banco *> $null

        if ($LASTEXITCODE -eq 0) {
            return $true
        }

        Start-Sleep -Seconds 2
    }

    return $false
}

function Sql-Fluxo {

    param([string]$Sql)

    $Sql |
        docker compose exec -T $servicoPostgresFluxo `
            psql `
            -U $postgresUsuario `
            -d $bancoFluxo
}

function Sql-Fluxo-Escalar {

    param([string]$Sql)

    $resultado =
        $Sql |
        docker compose exec -T $servicoPostgresFluxo `
            psql `
            -U $postgresUsuario `
            -d $bancoFluxo `
            -t `
            -A

    "$resultado".Trim()
}

function Sql-Relatorio {

    param([string]$Sql)

    $Sql |
        docker compose exec -T $servicoPostgresRelatorio `
            psql `
            -U $postgresUsuario `
            -d $bancoRelatorio
}

function Sql-Relatorio-Escalar {

    param([string]$Sql)

    $resultado =
        $Sql |
        docker compose exec -T $servicoPostgresRelatorio `
            psql `
            -U $postgresUsuario `
            -d $bancoRelatorio `
            -t `
            -A

    "$resultado".Trim()
}

function Aguardar-OutboxPublicado {

    param(
        [string]$CorrelationId,
        [int]$Tentativas = 120
    )

    for ($i = 1; $i -le $Tentativas; $i++) {

        $sql = @"
SELECT CASE
    WHEN "DATA_PUBLICACAO" IS NOT NULL THEN 1
    ELSE 0
END
FROM "TB_LANCAMENTO_OUTBOX"
WHERE "CORRELATION_ID" = '$CorrelationId';
"@

        $resultado = Sql-Fluxo-Escalar $sql

        if ($resultado -eq "1") {
            return $true
        }

        Start-Sleep -Seconds 1
    }

    return $false
}

function Aguardar-HistoricoRelatorio {

    param(
        [string]$CorrelationId,
        [int]$Tentativas = 120
    )

    for ($i = 1; $i -le $Tentativas; $i++) {

        $sql = @"
SELECT COUNT(*)
FROM "TB_HISTORICO_LANCAMENTO"
WHERE "CORRELATION_ID" = '$CorrelationId';
"@

        $resultado = Sql-Relatorio-Escalar $sql

        if ($resultado -eq "1") {
            return $true
        }

        Start-Sleep -Seconds 1
    }

    return $false
}

function Publicar-RabbitDireto {

    param(
        [string]$CorrelationId,
        [string]$Tipo,
        [decimal]$Valor,
        [string]$Descricao,
        [long]$LancamentoId,
        [string]$DataLancamento
    )

    $payload = @{
        Payload = @{
            Tipo = $Tipo
            Valor = $Valor
            Descricao = $Descricao
            LancamentoId = $LancamentoId
            DataLancamento = $DataLancamento
        }
        TipoEvento = "EventoDeLancamentoCriado"
        CorrelationId = $CorrelationId
        DataOcorrencia = [DateTimeOffset]::UtcNow.ToString("o")
    } | ConvertTo-Json -Depth 10 -Compress

    $mensagem = @{
        properties = @{
            correlation_id = $CorrelationId
            type = "EventoDeLancamentoCriado"
            content_type = "application/json"
            delivery_mode = 2
        }
        routing_key = $routingKeyPrincipal
        payload = $payload
        payload_encoding = "string"
    } | ConvertTo-Json -Depth 10

    Invoke-RestMethod `
        -Method Post `
        -Uri "$rabbitManagementUrl/api/exchanges/%2F/$exchangePrincipal/publish" `
        -Headers @{
            Authorization = "Basic $credencialRabbit"
        } `
        -ContentType "application/json" `
        -Body $mensagem
}

function Autenticar-Dev {

    param([string]$BaseUrl)

    $body = @{
        usuario = $autenticacaoDevUsuario
        senha = $autenticacaoDevSenha
    } | ConvertTo-Json

    try {
        return Invoke-RestMethod `
            -Method Post `
            -Uri "$BaseUrl/api/dev/autenticacao/token" `
            -ContentType "application/json" `
            -Body $body `
            -TimeoutSec 10 `
            -ErrorAction Stop
    }
    catch {
        return $null
    }
}

function Obter-TokenResposta {

    param($Resposta)

    if ($null -eq $Resposta) {
        return $null
    }

    foreach ($nome in @("token", "Token", "accessToken", "AccessToken")) {
        $propriedade = $Resposta.PSObject.Properties[$nome]
        if ($null -ne $propriedade -and
            -not [string]::IsNullOrWhiteSpace([string]$propriedade.Value)) {
            return [string]$propriedade.Value
        }
    }

    return $null
}

function Criar-LancamentoViaApi {

    param(
        [string]$CorrelationId,
        [decimal]$Valor,
        [string]$Descricao,
        [string]$Hora
    )

    $body = @{
        correlationId = $CorrelationId
        tipo = 1
        dataLancamento = "${dataTeste}T${Hora}-03:00"
        valor = $Valor
        descricao = $Descricao
    } | ConvertTo-Json

    try {

        return Invoke-WebRequest `
            -UseBasicParsing `
            -Method Post `
            -Uri "$baseUrlFluxo/api/lancamentos/" `
            -Headers @{
                Authorization = "Bearer $token"
            } `
            -ContentType "application/json" `
            -Body $body `
            -ErrorAction Stop
    }
    catch {
        return $_
    }
}

# =====================================================================
# INICIO
# =====================================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " TESTES INTEGRADOS - CASE FLUXO DE CAIXA" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

Info "Normal               : $correlationNormal"
Info "RabbitMQ indisponivel: $correlationRabbitDown"
Info "Postgres indisponivel: $correlationPostgresDown"
Info "Mensagem invalida    : $correlationInvalido"
Info "Concorrencia         : $correlationConcorrencia"

# =====================================================================
# TESTE 01
# =====================================================================

Titulo 1 "Docker"

# Bateria destrutiva: manter limpeza de filas e parada de servicos.
# Executar somente em ambiente local isolado.
Titulo 2 "Variaveis de ambiente"

$envOk = $true

foreach ($variavel in @(
    "POSTGRES_USER",
    "POSTGRES_PASSWORD",
    "RABBITMQ_USER",
    "RABBITMQ_PASSWORD",
    "RABBITMQ_ERLANG_COOKIE",
    "AUTENTICACAO_DEV_USUARIO",
    "AUTENTICACAO_DEV_SENHA",
    "JWT_CHAVE"
)) {

    $valor = Ler-Env $variavel

    if ([string]::IsNullOrWhiteSpace($valor)) {
        Erro "$variavel nao configurada."
        $envOk = $false
    }
    else {
        Ok "$variavel configurada."
    }
}

if (-not $envOk) {
    return
}

# =====================================================================
# TESTE 03
# =====================================================================

Titulo 3 "Build e Compose integrado"

docker compose up -d --build

if ($LASTEXITCODE -eq 0) {
    Ok "Compose integrado iniciado."
}
else {
    Erro "Falha ao iniciar Compose."
    return
}

# =====================================================================
# TESTE 04
# =====================================================================

Titulo 4 "PostgreSQL Fluxo"

if (Aguardar-Postgres $servicoPostgresFluxo $bancoFluxo) {
    Ok "PostgreSQL Fluxo disponivel."
}
else {
    Erro "PostgreSQL Fluxo indisponivel."
    return
}

# =====================================================================
# TESTE 05
# =====================================================================

Titulo 5 "PostgreSQL Relatorio"

if (Aguardar-Postgres $servicoPostgresRelatorio $bancoRelatorio) {
    Ok "PostgreSQL Relatorio disponivel."
}
else {
    Erro "PostgreSQL Relatorio indisponivel."
    return
}

# =====================================================================
# TESTE 06
# =====================================================================

Titulo 6 "RabbitMQ compartilhado"

if (Aguardar-RabbitManagement) {
    Ok "RabbitMQ disponivel."
}
else {
    Erro "RabbitMQ indisponivel."
    return
}

# =====================================================================
# TESTE 07
# =====================================================================

Titulo 7 "Containers"

docker compose ps

# =====================================================================
# TESTE 08
# =====================================================================

Titulo 8 "Testes automatizados"

$projetosTeste =
    Get-ChildItem `
        -Path .\BuildingBlocks,.\FluxoDeCaixa,.\RelatorioFluxoDeCaixa `
        -Recurse `
        -Filter *.csproj `
        -File |
    Where-Object {
        $_.FullName -notmatch '\\bin\\|\\obj\\' -and
        (
            $_.Name -match 'Teste|Test' -or
            $_.DirectoryName -match '\\tests\\'
        )
    } |
    Sort-Object FullName -Unique

$automacaoOk = $true

foreach ($projeto in $projetosTeste) {

    Info "Executando: $($projeto.Name)"

    dotnet test $projeto.FullName

    if ($LASTEXITCODE -eq 0) {
        Ok "$($projeto.Name)"
    }
    else {
        Erro "$($projeto.Name)"
        $automacaoOk = $false
    }
}

if ($automacaoOk) {
    Ok "Todos os testes automatizados passaram."
}

# =====================================================================
# TESTE 09
# =====================================================================

Titulo 9 "API LivroRazao sem token"

try {
    Invoke-WebRequest `
        -UseBasicParsing `
        "$baseUrlFluxo/api/lancamentos/999999999" `
        -ErrorAction Stop | Out-Null
}
catch {

    if ([int]$_.Exception.Response.StatusCode -eq 401) {
        Ok "LivroRazao exige autenticacao."
    }
    else {
        Erro "Resposta inesperada."
    }
}

# =====================================================================
# TESTE 10
# =====================================================================

Titulo 10 "API Consolidado sem token"

try {

    Invoke-WebRequest `
        -UseBasicParsing `
        "$baseUrlRelatorio/api/consolidados-diarios/$dataTeste" `
        -ErrorAction Stop | Out-Null
}
catch {

    $status = [int]$_.Exception.Response.StatusCode

    if ($status -eq 401 -or $status -eq 403) {
        Ok "Consolidado exige autenticacao."
    }
    else {
        Erro "Resposta inesperada."
    }
}

# =====================================================================
# TESTE 11
# =====================================================================

Titulo 11 "Autenticacao DEV - credenciais invalidas"

$bodyCredenciaisInvalidas = @{
    usuario = $autenticacaoDevUsuario
    senha = "senhaInvalida"
} | ConvertTo-Json

$credenciaisInvalidasOk = $true

foreach ($api in @(
    @{ Nome = "LivroRazao"; Url = $baseUrlFluxo },
    @{ Nome = "ConsolidadoDiario"; Url = $baseUrlRelatorio }
)) {
    try {
        Invoke-WebRequest `
            -UseBasicParsing `
            -Method Post `
            -Uri "$($api.Url)/api/dev/autenticacao/token" `
            -ContentType "application/json" `
            -Body $bodyCredenciaisInvalidas `
            -ErrorAction Stop | Out-Null

        Erro "$($api.Nome) aceitou credenciais invalidas."
        $credenciaisInvalidasOk = $false
    }
    catch {
        $status = [int]$_.Exception.Response.StatusCode

        if ($status -eq 401) {
            Ok "$($api.Nome) rejeitou credenciais invalidas com HTTP 401."
        }
        else {
            Erro "$($api.Nome) retornou HTTP $status para credenciais invalidas."
            $credenciaisInvalidasOk = $false
        }
    }
}

# =====================================================================
# TESTE 12
# =====================================================================

Titulo 12 "Autenticacao DEV - gerar JWT real"

$respostaAutenticacaoFluxo = Autenticar-Dev $baseUrlFluxo
$respostaAutenticacaoRelatorio = Autenticar-Dev $baseUrlRelatorio

$token = Obter-TokenResposta $respostaAutenticacaoFluxo
$tokenRelatorio = Obter-TokenResposta $respostaAutenticacaoRelatorio

if (-not [string]::IsNullOrWhiteSpace($token)) {
    Ok "JWT obtido pela API LivroRazao."
}
else {
    Erro "Nao foi possivel obter JWT pela API LivroRazao."
    return
}

if (-not [string]::IsNullOrWhiteSpace($tokenRelatorio)) {
    Ok "JWT obtido pela API ConsolidadoDiario."
}
else {
    Erro "Nao foi possivel obter JWT pela API ConsolidadoDiario."
    return
}

# =====================================================================
# TESTE 13
# =====================================================================

Titulo 13 "Autorizacao LivroRazao com JWT DEV"

try {
    Invoke-WebRequest `
        -UseBasicParsing `
        "$baseUrlFluxo/api/lancamentos/999999999" `
        -Headers @{
            Authorization = "Bearer $token"
        } `
        -ErrorAction Stop | Out-Null
}
catch {
    $status = [int]$_.Exception.Response.StatusCode

    if ($status -eq 404) {
        Ok "JWT DEV autorizado para CONTABILIDADE/FLUXO DE CAIXA/LANCAMENTO."
    }
    elseif ($status -eq 403) {
        Erro "JWT valido, mas sem autorizacao de LANCAMENTO."
    }
    else {
        Erro "Resposta inesperada do LivroRazao: HTTP $status."
    }
}

# =====================================================================
# TESTE 14
# =====================================================================

Titulo 14 "Autorizacao ConsolidadoDiario com JWT DEV"

try {

    $r =
        Invoke-WebRequest `
            -UseBasicParsing `
            "$baseUrlRelatorio/api/consolidados-diarios/$dataTeste" `
            -Headers @{
                Authorization = "Bearer $token"
            } `
            -ErrorAction Stop

    Ok "JWT DEV autorizado para CONTABILIDADE/RELATORIO FLUXO DE CAIXA/CONSOLIDADO DIARIO. HTTP $($r.StatusCode)"
}
catch {

    if ([int]$_.Exception.Response.StatusCode -eq 404) {
        Ok "JWT DEV autorizado; sem consolidado para data."
    }
    else {
        if ([int]$_.Exception.Response.StatusCode -eq 403) {
            Erro "JWT valido, mas sem autorizacao de CONSOLIDADO DIARIO."
        }
        else {
            Erro "JWT rejeitado."
        }
    }
}

# =====================================================================
# TESTE 15
# =====================================================================

Titulo 15 "Swagger das duas APIs"

foreach ($api in @(
    @{ Nome = "LivroRazao"; Url = $baseUrlFluxo },
    @{ Nome = "ConsolidadoDiario"; Url = $baseUrlRelatorio }
)) {
    try {
        $swagger = Invoke-WebRequest `
            -UseBasicParsing `
            "$($api.Url)/swagger/index.html" `
            -ErrorAction Stop

        if ($swagger.StatusCode -eq 200) {
            Ok "Swagger proprio da API $($api.Nome) disponivel."
        }
        else {
            Erro "Swagger da API $($api.Nome) retornou HTTP $($swagger.StatusCode)."
        }
    }
    catch {
        Erro "Swagger da API $($api.Nome) indisponivel."
    }
}

# =====================================================================
# TESTE 16
# =====================================================================

Titulo 16 "Topologia RabbitMQ"

foreach ($exchange in @(
    $exchangePrincipal,
    $exchangeRetry,
    $exchangeDlx
)) {

    try {

        Invoke-RestMethod `
            "$rabbitManagementUrl/api/exchanges/%2F/$exchange" `
            -Headers @{
                Authorization = "Basic $credencialRabbit"
            } | Out-Null

        Ok "Exchange $exchange"
    }
    catch {
        Erro "Exchange ausente: $exchange"
    }
}

foreach ($fila in @(
    $filaPrincipal,
    $filaRetry,
    $filaDlq
)) {

    try {
        Obter-FilaRabbit $fila | Out-Null
        Ok "Fila $fila"
    }
    catch {
        Erro "Fila ausente: $fila"
    }
}

# =====================================================================
# TESTE 17
# =====================================================================

Titulo 17 "Retry Queue"

$retry = Obter-FilaRabbit $filaRetry

if (
    $retry.arguments.'x-message-ttl' -eq 30000 -and
    $retry.arguments.'x-dead-letter-exchange' -eq $exchangePrincipal
) {
    Ok "Retry Queue correta."
}
else {
    Erro "Retry Queue incorreta."
}

# =====================================================================
# TESTE 18
# =====================================================================

Titulo 18 "DLQ"

$principal = Obter-FilaRabbit $filaPrincipal

if (
    $principal.arguments.'x-dead-letter-exchange' -eq $exchangeDlx -and
    $principal.arguments.'x-dead-letter-routing-key' -eq $routingKeyDlq
) {
    Ok "DLQ correta."
}
else {
    Erro "DLQ incorreta."
}

# =====================================================================
# TESTE 19
# =====================================================================

Titulo 19 "Consumidor conectado"

$consumerOk = $false

for ($i = 1; $i -le 30; $i++) {

    $q = Obter-FilaRabbit $filaPrincipal

    if ([int]$q.consumers -ge 1) {
        $consumerOk = $true
        break
    }

    Start-Sleep 1
}

if ($consumerOk) {
    Ok "Consumidor conectado."
}
else {
    Erro "Consumidor nao conectado."
}

# =====================================================================
# TESTE 20
# =====================================================================

Titulo 20 "Limpeza das filas"

Limpar-FilaRabbit $filaPrincipal
Limpar-FilaRabbit $filaRetry
Limpar-FilaRabbit $filaDlq

# =====================================================================
# TESTE 21
# =====================================================================

Titulo 21 "Baseline consolidado"

$sql = @"
SELECT COALESCE("TOTAL_CREDITOS", 0)
FROM "TB_SALDO_CONSOLIDADO_DIARIO"
WHERE "DATA_REFERENCIA" = '$dataTeste';
"@

$baselineTexto = Sql-Relatorio-Escalar $sql

if ([string]::IsNullOrWhiteSpace($baselineTexto)) {
    $baseline = [decimal]0
}
else {
    $baseline =
        [decimal]::Parse(
            $baselineTexto,
            [Globalization.CultureInfo]::InvariantCulture
        )
}

Info "Baseline: $baseline"

# =====================================================================
# TESTE 22
# =====================================================================

Titulo 22 "Lancamento normal"

$r =
    Criar-LancamentoViaApi `
        $correlationNormal `
        77.00 `
        "Teste integrado normal" `
        "15:00:00"

if ($r.StatusCode -eq 201) {
    Ok "HTTP 201."
}
else {
    Erro "Criacao falhou."
}

# =====================================================================
# TESTE 23
# =====================================================================

Titulo 23 "Persistencia + Outbox"

$sql = @"
SELECT
    (SELECT COUNT(*) FROM "TB_LANCAMENTO"
     WHERE "CORRELATION_ID"='$correlationNormal') AS "LANCAMENTO",

    (SELECT COUNT(*) FROM "TB_LANCAMENTO_OUTBOX"
     WHERE "CORRELATION_ID"='$correlationNormal') AS "OUTBOX";
"@

$qtdLancamento = Sql-Fluxo-Escalar @"
SELECT COUNT(*)
FROM "TB_LANCAMENTO"
WHERE "CORRELATION_ID"='$correlationNormal';
"@

$qtdOutbox = Sql-Fluxo-Escalar @"
SELECT COUNT(*)
FROM "TB_LANCAMENTO_OUTBOX"
WHERE "CORRELATION_ID"='$correlationNormal';
"@

Info "Lancamentos encontrados: $qtdLancamento"
Info "Outbox encontrados     : $qtdOutbox"

if ($qtdLancamento -eq "1" -and $qtdOutbox -eq "1") {
    Ok "Persistencia e Outbox consistentes: 1 lancamento e 1 evento Outbox."
}
else {
    Erro "Persistencia/Outbox inconsistente. Esperado LANCAMENTO=1 e OUTBOX=1; encontrado LANCAMENTO=$qtdLancamento e OUTBOX=$qtdOutbox."
}

# =====================================================================
# TESTE 24
# =====================================================================

Titulo 24 "Outbox publicado"

if (Aguardar-OutboxPublicado $correlationNormal) {
    Ok "Outbox publicado."
}
else {
    Erro "Outbox nao publicado."
}

# =====================================================================
# TESTE 25
# =====================================================================

Titulo 25 "Consumo pelo Relatorio"

if (Aguardar-HistoricoRelatorio $correlationNormal) {
    Ok "Relatorio recebeu evento."
}
else {
    Erro "Relatorio nao recebeu."
}

# =====================================================================
# TESTE 26
# =====================================================================

Titulo 26 "Historico"

$sql = @"
SELECT *
FROM "TB_HISTORICO_LANCAMENTO"
WHERE "CORRELATION_ID"='$correlationNormal';
"@

Sql-Relatorio $sql

# =====================================================================
# TESTE 27
# =====================================================================

Titulo 27 "Consolidado"

$sql = @"
SELECT
    "DATA_REFERENCIA",
    "TOTAL_CREDITOS",
    "TOTAL_DEBITOS",
    "SALDO_DIARIO_CONSOLIDADO"
FROM "TB_SALDO_CONSOLIDADO_DIARIO"
WHERE "DATA_REFERENCIA"='$dataTeste';
"@

Sql-Relatorio $sql

# =====================================================================
# TESTE 28
# =====================================================================

Titulo 28 "API Consolidado"

try {

    $r =
        Invoke-WebRequest `
            -UseBasicParsing `
            "$baseUrlRelatorio/api/consolidados-diarios/$dataTeste" `
            -Headers @{
                Authorization = "Bearer $token"
            } `
            -ErrorAction Stop

    Write-Host $r.Content
    Ok "HTTP 200."
}
catch {
    Erro "Consulta falhou."
}

# =====================================================================
# TESTE 29
# =====================================================================

Titulo 29 "Idempotencia sequencial"

$r =
    Criar-LancamentoViaApi `
        $correlationNormal `
        77.00 `
        "Teste integrado normal" `
        "15:00:00"

if ($r.StatusCode -eq 200) {
    Ok "Reenvio idempotente."
}
else {
    Erro "Resposta inesperada."
}

# =====================================================================
# TESTE 30
# =====================================================================

Titulo 30 "Mensagem invalida"

Limpar-FilaRabbit $filaPrincipal
Limpar-FilaRabbit $filaRetry
Limpar-FilaRabbit $filaDlq

Publicar-RabbitDireto `
    $correlationInvalido `
    "X" `
    10.00 `
    "Evento invalido" `
    920001 `
    "${dataTeste}T16:00:00+00:00" | Out-Null

$invalidaProcessada = $false

for ($i = 1; $i -le 30; $i++) {

    $q1 = Obter-FilaRabbit $filaPrincipal
    $q2 = Obter-FilaRabbit $filaRetry
    $q3 = Obter-FilaRabbit $filaDlq

    $sql = @"
SELECT COUNT(*)
FROM "TB_HISTORICO_LANCAMENTO"
WHERE "CORRELATION_ID"='$correlationInvalido';
"@

    $qtd = Sql-Relatorio-Escalar $sql

    if (
        [int]$q1.messages -eq 0 -and
        [int]$q2.messages -eq 0 -and
        [int]$q3.messages -eq 0 -and
        $qtd -eq "0"
    ) {
        $invalidaProcessada = $true
        break
    }

    Start-Sleep 1
}

if ($invalidaProcessada) {
    Ok "Mensagem invalida descartada corretamente."
}
else {
    Erro "Mensagem invalida apresentou comportamento inesperado."
}

# =====================================================================
# TESTE 31
# =====================================================================

Titulo 31 "Parar RabbitMQ"

Limpar-FilaRabbit $filaPrincipal
Limpar-FilaRabbit $filaRetry
Limpar-FilaRabbit $filaDlq

docker compose stop $servicoRabbitMq

if ($LASTEXITCODE -eq 0) {
    Ok "RabbitMQ parado."
}
else {
    Erro "Falha ao parar RabbitMQ."
    return
}

# =====================================================================
# TESTE 32
# =====================================================================

Titulo 32 "Criar lancamento sem RabbitMQ"

$r =
    Criar-LancamentoViaApi `
        $correlationRabbitDown `
        33.00 `
        "Teste RabbitMQ indisponivel" `
        "16:30:00"

if ($r.StatusCode -eq 201) {
    Ok "API persistiu sem RabbitMQ."
}
else {
    Erro "API falhou."
}

# =====================================================================
# TESTE 33
# =====================================================================

Titulo 33 "Outbox registra falha"

$falhaObservada = $false

for ($i = 1; $i -le 90; $i++) {

    $sql = @"
SELECT CASE
    WHEN COALESCE("TENTATIVAS",0) > 0
    THEN 1
    ELSE 0
END
FROM "TB_LANCAMENTO_OUTBOX"
WHERE "CORRELATION_ID"='$correlationRabbitDown';
"@

    if ((Sql-Fluxo-Escalar $sql) -eq "1") {
        $falhaObservada = $true
        break
    }

    Start-Sleep 1
}

if ($falhaObservada) {
    Ok "Falha registrada."
}
else {
    Erro "Falha nao registrada."
}

# =====================================================================
# TESTE 34
# =====================================================================

Titulo 34 "Outbox permanece elegivel apos limite"

Info "Aguardando pelo menos 5 tentativas..."

$atingiuLimite = $false

for ($i = 1; $i -le 120; $i++) {

    $sql = @"
SELECT COALESCE("TENTATIVAS",0)
FROM "TB_LANCAMENTO_OUTBOX"
WHERE "CORRELATION_ID"='$correlationRabbitDown';
"@

    $tentativas = Sql-Fluxo-Escalar $sql

    if (
        -not [string]::IsNullOrWhiteSpace($tentativas) -and
        [int]$tentativas -ge 5
    ) {
        $atingiuLimite = $true
        break
    }

    Start-Sleep 1
}

$sql = @"
SELECT
    "CORRELATION_ID",
    "DATA_PUBLICACAO",
    "TENTATIVAS",
    "ULTIMO_ERRO",
    "STATUS"
FROM "TB_LANCAMENTO_OUTBOX"
WHERE "CORRELATION_ID"='$correlationRabbitDown';
"@

Sql-Fluxo $sql

if ($atingiuLimite) {
    Ok "Outbox atingiu pelo menos 5 tentativas sem perder o evento."
}
else {
    Erro "Nao foi possivel observar 5 tentativas."
}

# =====================================================================
# TESTE 35
# =====================================================================

Titulo 35 "Recuperar RabbitMQ"

docker compose start $servicoRabbitMq

if (Aguardar-RabbitManagement) {
    Ok "RabbitMQ recuperado."
}
else {
    Erro "RabbitMQ nao recuperou."
    return
}

Start-Sleep 5

# =====================================================================
# TESTE 36
# =====================================================================

Titulo 36 "Republicacao automatica Outbox"

if (
    Aguardar-OutboxPublicado `
        $correlationRabbitDown `
        180
) {
    Ok "Outbox republicado automaticamente."
}
else {
    Erro "Outbox NAO republicado."
}

# =====================================================================
# TESTE 37
# =====================================================================

Titulo 37 "Estado do Outbox apos recuperacao"

$sql = @"
SELECT
    "CORRELATION_ID",
    "DATA_PUBLICACAO",
    "TENTATIVAS",
    "ULTIMO_ERRO",
    "STATUS"
FROM "TB_LANCAMENTO_OUTBOX"
WHERE "CORRELATION_ID"='$correlationRabbitDown';
"@

Sql-Fluxo $sql

# =====================================================================
# TESTE 38
# =====================================================================

Titulo 38 "Reconexao consumidor"

$consumerOk = $false

for ($i = 1; $i -le 60; $i++) {

    $q = Obter-FilaRabbit $filaPrincipal

    if ([int]$q.consumers -ge 1) {
        $consumerOk = $true
        break
    }

    Start-Sleep 1
}

if ($consumerOk) {
    Ok "Consumidor reconectou."
}
else {
    Erro "Consumidor nao reconectou."
}

# =====================================================================
# TESTE 39
# =====================================================================

Titulo 39 "Entrega apos recuperacao RabbitMQ"

if (
    Aguardar-HistoricoRelatorio `
        $correlationRabbitDown `
        180
) {
    Ok "Evento chegou ao Relatorio."
}
else {
    Erro "Evento nao chegou ao Relatorio."
}

# =====================================================================
# TESTE 40
# =====================================================================

Titulo 40 "Parar PostgreSQL Relatorio"

Limpar-FilaRabbit $filaPrincipal
Limpar-FilaRabbit $filaRetry
Limpar-FilaRabbit $filaDlq

docker compose stop $servicoPostgresRelatorio

if ($LASTEXITCODE -eq 0) {
    Ok "PostgreSQL Relatorio parado."
}
else {
    Erro "Falha ao parar PostgreSQL."
    return
}

# =====================================================================
# TESTE 41
# =====================================================================

Titulo 41 "Publicar com banco Relatorio indisponivel"

$r =
    Criar-LancamentoViaApi `
        $correlationPostgresDown `
        25.00 `
        "Teste Retry Relatorio" `
        "17:00:00"

if ($r.StatusCode -eq 201) {
    Ok "LivroRazao aceitou."
}
else {
    Erro "LivroRazao falhou."
}

if (Aguardar-OutboxPublicado $correlationPostgresDown) {
    Ok "Outbox publicou."
}
else {
    Erro "Outbox nao publicou."
}

# =====================================================================
# TESTE 42
# =====================================================================

Titulo 42 "Retry Queue"

$entrouRetry = $false

for ($i = 1; $i -le 30; $i++) {

    $q = Obter-FilaRabbit $filaRetry

    if ([int]$q.messages -ge 1) {
        $entrouRetry = $true
        break
    }

    Start-Sleep 1
}

if ($entrouRetry) {
    Ok "Evento entrou no Retry."
}
else {
    Erro "Evento nao entrou no Retry."
}

# =====================================================================
# TESTE 43
# =====================================================================

Titulo 43 "Recuperar PostgreSQL Relatorio"

docker compose start $servicoPostgresRelatorio

if (
    Aguardar-Postgres `
        $servicoPostgresRelatorio `
        $bancoRelatorio
) {
    Ok "Banco recuperado."
}
else {
    Erro "Banco nao recuperou."
    return
}

# =====================================================================
# TESTE 44
# =====================================================================

Titulo 44 "TTL e redelivery"

if (
    Aguardar-HistoricoRelatorio `
        $correlationPostgresDown `
        180
) {
    Ok "Evento recuperado apos Retry."
}
else {
    Erro "Evento nao recuperado."
}

# =====================================================================
# TESTE 45
# =====================================================================

Titulo 45 "Idempotencia concorrente"

$bodyConcorrencia = @{
    correlationId = $correlationConcorrencia
    tipo = 1
    dataLancamento = "${dataTeste}T18:00:00-03:00"
    valor = 40.00
    descricao = "Teste concorrencia"
} | ConvertTo-Json

$jobs = 1..10 | ForEach-Object {

    $numero = $_

    Start-Job `
        -ArgumentList `
            $numero,
            $bodyConcorrencia,
            $token,
            $baseUrlFluxo `
        -ScriptBlock {

        param(
            $numero,
            $body,
            $tokenLocal,
            $url
        )

        try {

            $r =
                Invoke-WebRequest `
                    -UseBasicParsing `
                    -Method Post `
                    -Uri "$url/api/lancamentos/" `
                    -Headers @{
                        Authorization = "Bearer $tokenLocal"
                    } `
                    -ContentType "application/json" `
                    -Body $body `
                    -ErrorAction Stop

            [PSCustomObject]@{
                Requisicao = $numero
                Status = [int]$r.StatusCode
                Sucesso = $true
            }
        }
        catch {

            [PSCustomObject]@{
                Requisicao = $numero
                Status = -1
                Sucesso = $false
            }
        }
    }
}

$jobs | Wait-Job | Out-Null

$resultados =
    $jobs |
    Receive-Job |
    Sort-Object Requisicao

$resultados |
    Format-Table -AutoSize

$jobs | Remove-Job

# =====================================================================
# TESTE 46
# =====================================================================

Titulo 46 "Idempotencia concorrente Fluxo"

Start-Sleep 5

$sql = @"
SELECT
    (SELECT COUNT(*)
     FROM "TB_LANCAMENTO"
     WHERE "CORRELATION_ID"='$correlationConcorrencia')
        AS "LANCAMENTOS",

    (SELECT COUNT(*)
     FROM "TB_LANCAMENTO_OUTBOX"
     WHERE "CORRELATION_ID"='$correlationConcorrencia')
        AS "OUTBOX";
"@

$qtdLancamentosConcorrencia = Sql-Fluxo-Escalar @"
SELECT COUNT(*)
FROM "TB_LANCAMENTO"
WHERE "CORRELATION_ID"='$correlationConcorrencia';
"@

$qtdOutboxConcorrencia = Sql-Fluxo-Escalar @"
SELECT COUNT(*)
FROM "TB_LANCAMENTO_OUTBOX"
WHERE "CORRELATION_ID"='$correlationConcorrencia';
"@

Info "Lancamentos encontrados: $qtdLancamentosConcorrencia"
Info "Outbox encontrados     : $qtdOutboxConcorrencia"

if ($qtdLancamentosConcorrencia -eq "1" -and $qtdOutboxConcorrencia -eq "1") {
    Ok "Idempotencia concorrente no Fluxo confirmada: 1 lancamento e 1 evento Outbox."
}
else {
    Erro "Idempotencia concorrente no Fluxo violada. Esperado LANCAMENTOS=1 e OUTBOX=1; encontrado LANCAMENTOS=$qtdLancamentosConcorrencia e OUTBOX=$qtdOutboxConcorrencia."
}

# =====================================================================
# TESTE 47
# =====================================================================

Titulo 47 "Idempotencia concorrente Relatorio"

if (
    Aguardar-HistoricoRelatorio `
        $correlationConcorrencia `
        180
) {

    $sql = @"
SELECT COUNT(*)
FROM "TB_HISTORICO_LANCAMENTO"
WHERE "CORRELATION_ID"='$correlationConcorrencia';
"@

    $qtd = Sql-Relatorio-Escalar $sql

    if ($qtd -eq "1") {
        Ok "Somente um historico."
    }
    else {
        Erro "Historico duplicado."
    }
}
else {
    Erro "Evento nao chegou."
}

# =====================================================================
# TESTE 48
# =====================================================================

Titulo 48 "Estado final das filas"

Start-Sleep 5

foreach ($fila in @(
    $filaPrincipal,
    $filaRetry,
    $filaDlq
)) {

    $q = Obter-FilaRabbit $fila

    [PSCustomObject]@{
        Fila = $q.name
        Mensagens = $q.messages
        Prontas = $q.messages_ready
        EmProcesso = $q.messages_unacknowledged
        Consumers = $q.consumers
    } |
        Format-Table -AutoSize
}

# =====================================================================
# TESTE 49
# =====================================================================

Titulo 49 "Verificar ausencia do warning GSSAPI"

$logsGss =
    docker compose logs --since=60m $servicoLivroRazao |
    Select-String `
        "libgssapi_krb5|GSSAPI|GSS Encryption" `
        -CaseSensitive:$false

if ($logsGss) {
    Erro "Ainda existe referencia ao GSSAPI nos logs:"
    $logsGss
}
else {
    Ok "Nenhum warning libgssapi/GSSAPI encontrado."
}

# =====================================================================
# TESTE 50
# =====================================================================

Titulo 50 "Logs Outbox"

docker compose logs --since=60m $servicoLivroRazao |
    Select-String `
        "$correlationRabbitDown|Outbox|RabbitMQ|Tentativa|public|erro|falha" `
        -CaseSensitive:$false

# =====================================================================
# TESTE 51
# =====================================================================

Titulo 51 "Logs Relatorio"

docker compose logs --since=60m $servicoConsolidado |
    Select-String `
        "$correlationNormal|$correlationRabbitDown|$correlationPostgresDown|$correlationInvalido|$correlationConcorrencia|Retry|consolidado|inval|erro" `
        -CaseSensitive:$false

# =====================================================================
# TESTE 52
# =====================================================================

Titulo 52 "Containers finais"

docker compose ps

# =====================================================================
# RESULTADO FINAL
# =====================================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
if ($script:QuantidadeErros -eq 0) {
    Write-Host " RESULTADO FINAL: BATERIA APROVADA" -ForegroundColor Green
} else {
    Write-Host " RESULTADO FINAL: BATERIA REPROVADA" -ForegroundColor Red
}
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Total de erros: $script:QuantidadeErros"
if ($script:QuantidadeErros -gt 0) {
    Write-Host "Erros encontrados:" -ForegroundColor Red
    for ($i = 0; $i -lt $script:ErrosEncontrados.Count; $i++) {
        Write-Host ("  {0:D2}. {1}" -f ($i + 1), $script:ErrosEncontrados[$i]) -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "CorrelationId normal:"
Write-Host "  $correlationNormal"

Write-Host ""
Write-Host "CorrelationId RabbitMQ indisponivel:"
Write-Host "  $correlationRabbitDown"

Write-Host ""
Write-Host "CorrelationId PostgreSQL Relatorio:"
Write-Host "  $correlationPostgresDown"

Write-Host ""
Write-Host "CorrelationId invalido:"
Write-Host "  $correlationInvalido"

Write-Host ""
Write-Host "CorrelationId concorrencia:"
Write-Host "  $correlationConcorrencia"

}
# =====================================================================
# CODIGO DE SAIDA - AUTOMACAO E CI/CD
# =====================================================================

if ($script:QuantidadeErros -gt 0) {
    exit 1
}

exit 0
