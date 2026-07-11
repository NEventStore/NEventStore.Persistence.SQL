<#
.SYNOPSIS
Starts the complete isolated database stack for one worktree/environment.

.DESCRIPTION
Usage: ./start-environment.ps1 [debug|test|ci|<dynamic-name>]
Reserved environments use predictable ports. With no name, the current Git worktree directory is
normalized and Docker assigns collision-free ports. A successful start atomically writes exactly
one of .env.debug, .env.test, .env.ci, or .env.dynamic after every service is healthy.
Volumes are preserved by stop by default; use stop-environment.ps1 <name> --remove-data to remove
only that environment's data. The script is non-interactive and throws on every failure.

Fixed ports exist for predictable human and CI workflows; dynamic ports exist for parallel
worktrees. Compose, not the scripts, owns container/network/volume names, so container_name is
forbidden. Generated files contain provider components rather than complete connection strings so
construction remains test-code responsibility. Global Docker cleanup commands are prohibited
because they can destroy unrelated environments.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $EnvironmentName
)

$ErrorActionPreference = 'Stop'
$RootDirectory = $PSScriptRoot
$ComposeDirectory = Join-Path $RootDirectory 'docker'
$RepositoryPrefix = 'neventstore'
$DatabaseName = 'NEventStore'
$DatabaseUsername = 'sa'
$DatabasePassword = 'Password1'

function Normalize-EnvironmentName {
    param([Parameter(Mandatory)][string] $Name)

    $normalized = $Name.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $normalized = $normalized -replace '-+', '-'
    return $normalized.Trim('-')
}

function Get-DefaultEnvironmentName {
    $worktreeRoot = (& git -C $RootDirectory rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($worktreeRoot)) {
        $worktreeRoot = $RootDirectory
    }

    return Split-Path $worktreeRoot.Trim() -Leaf
}

function Get-ComposeOverride {
    param([Parameter(Mandatory)][string] $Name)

    if ($Name -in @('debug', 'test', 'ci')) {
        return Join-Path $ComposeDirectory "docker-compose.$Name.yml"
    }

    return Join-Path $ComposeDirectory 'docker-compose.dynamic.yml'
}

function Get-EnvironmentFile {
    param([Parameter(Mandatory)][string] $Name)

    if ($Name -in @('debug', 'test', 'ci')) {
        return Join-Path $RootDirectory ".env.$Name"
    }

    return Join-Path $RootDirectory '.env.dynamic'
}

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)

    & docker compose --project-name $script:ProjectName `
        -f (Join-Path $ComposeDirectory 'docker-compose.yml') `
        -f $script:OverrideFile @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed: $($Arguments -join ' ')"
    }
}

function Wait-ServiceHealthy {
    param([Parameter(Mandatory)][string] $Service)

    $containerId = (Invoke-Compose ps -q $Service | Select-Object -Last 1).Trim()
    if ([string]::IsNullOrWhiteSpace($containerId)) {
        throw "Compose did not create service '$Service'."
    }

    $status = 'unknown'
    for ($attempt = 1; $attempt -le 120; $attempt++) {
        $status = (& docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $containerId 2>$null)
        if ($status -eq 'healthy') {
            return
        }

        if ($status -in @('exited', 'dead')) {
            break
        }

        Start-Sleep -Seconds 2
    }

    & docker logs --tail 100 $containerId 2>$null | Write-Error
    throw "Service '$Service' did not become healthy (last status: $status)."
}

function Get-PublishedPort {
    param(
        [Parameter(Mandatory)][string] $Service,
        [Parameter(Mandatory)][int] $ContainerPort
    )

    $binding = (Invoke-Compose port $Service $ContainerPort | Select-Object -Last 1).Trim()
    $port = $binding.Substring($binding.LastIndexOf(':') + 1)
    if ($port -notmatch '^\d+$') {
        throw "Could not resolve the published port for '$Service`:$ContainerPort'."
    }

    return $port
}

& docker compose version *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker Compose v2 is unavailable.'
}

$rawEnvironment = if ([string]::IsNullOrWhiteSpace($EnvironmentName)) {
    Get-DefaultEnvironmentName
}
else {
    $EnvironmentName
}

$normalizedEnvironment = Normalize-EnvironmentName $rawEnvironment
if ([string]::IsNullOrWhiteSpace($normalizedEnvironment)) {
    throw 'The environment name becomes empty after normalization.'
}

$script:ProjectName = "$RepositoryPrefix-$normalizedEnvironment"
$script:OverrideFile = Get-ComposeOverride $normalizedEnvironment
$environmentFile = Get-EnvironmentFile $normalizedEnvironment

Invoke-Compose up --detach
foreach ($service in @('sqlexpress', 'mysql', 'postgres', 'oracle')) {
    Wait-ServiceHealthy $service
}

$sqlContainer = (Invoke-Compose ps -q sqlexpress | Select-Object -Last 1).Trim()
& docker exec $sqlContainer /opt/mssql-tools/bin/sqlcmd `
    -S localhost -U sa -P $DatabasePassword `
    -Q "IF DB_ID(N'$DatabaseName') IS NULL CREATE DATABASE [$DatabaseName];" *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to create or verify the SQL Server NEventStore database.'
}

$sqlServerPort = Get-PublishedPort sqlexpress 1433
$mySqlPort = Get-PublishedPort mysql 3306
$postgresPort = Get-PublishedPort postgres 5432
$oraclePort = Get-PublishedPort oracle 1521

$temporaryFile = Join-Path $RootDirectory ".env.tmp.$([Guid]::NewGuid().ToString('N'))"
try {
    @(
        'SQLSERVER_HOST=127.0.0.1'
        "SQLSERVER_PORT=$sqlServerPort"
        "SQLSERVER_DATABASE=$DatabaseName"
        "SQLSERVER_USERNAME=$DatabaseUsername"
        "SQLSERVER_PASSWORD=$DatabasePassword"
        'MYSQL_HOST=127.0.0.1'
        "MYSQL_PORT=$mySqlPort"
        "MYSQL_DATABASE=$DatabaseName"
        "MYSQL_USERNAME=$DatabaseUsername"
        "MYSQL_PASSWORD=$DatabasePassword"
        'POSTGRES_HOST=127.0.0.1'
        "POSTGRES_PORT=$postgresPort"
        "POSTGRES_DATABASE=$DatabaseName"
        "POSTGRES_USERNAME=$DatabaseUsername"
        "POSTGRES_PASSWORD=$DatabasePassword"
        'ORACLE_HOST=127.0.0.1'
        "ORACLE_PORT=$oraclePort"
        'ORACLE_SERVICE=XE'
        'ORACLE_USERNAME=system'
        "ORACLE_PASSWORD=$DatabasePassword"
    ) | Set-Content -Path $temporaryFile -Encoding utf8NoBOM

    Move-Item -Path $temporaryFile -Destination $environmentFile -Force
}
finally {
    Remove-Item $temporaryFile -Force -ErrorAction SilentlyContinue
}

$testEnvironment = if ($normalizedEnvironment -in @('debug', 'test', 'ci')) { $normalizedEnvironment } else { 'dynamic' }
Write-Host "Environment: $normalizedEnvironment"
Write-Host "Compose project: $script:ProjectName"
Write-Host "Configuration: $environmentFile"
Write-Host "Ports: SQL Server=$sqlServerPort, MySQL=$mySqlPort, PostgreSQL=$postgresPort, Oracle=$oraclePort"
Write-Host "Tests: `$env:NEVENTSTORE_ENVIRONMENT='$testEnvironment'; dotnet test ./src/NEventStore.Persistence.Sql.Core.sln"
