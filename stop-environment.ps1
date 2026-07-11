<#
.SYNOPSIS
Stops only the Compose project belonging to one worktree/environment.

.DESCRIPTION
Usage: ./stop-environment.ps1 [debug|test|ci|<dynamic-name>] [--remove-data]
Reserved names select fixed environments. With no name, the current Git worktree directory is
normalized and used as a dynamic environment. Generated .env files and named volumes are preserved
by default. --remove-data removes only the selected Compose project's volumes. The script is
non-interactive and throws on invalid input or Compose failure. It never uses fixed container names
or global Docker cleanup commands, because unrelated worktrees are not collateral damage.
#>

[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Position = 0)]
    [string] $EnvironmentName,

    [Alias('remove-data')]
    [switch] $RemoveData
)

$ErrorActionPreference = 'Stop'
$RootDirectory = $PSScriptRoot
$ComposeDirectory = Join-Path $RootDirectory 'docker'
$RepositoryPrefix = 'neventstore'

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

$projectName = "$RepositoryPrefix-$normalizedEnvironment"
$overrideFile = Get-ComposeOverride $normalizedEnvironment
$arguments = @(
    'compose'
    '--project-name', $projectName
    '-f', (Join-Path $ComposeDirectory 'docker-compose.yml')
    '-f', $overrideFile
    'down'
)
if ($RemoveData) {
    $arguments += '--volumes'
}

& docker @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose failed while stopping environment '$normalizedEnvironment'."
}

Write-Host "Stopped environment $normalizedEnvironment (Compose project $projectName)."
if ($RemoveData) {
    Write-Host "Removed only this environment's named volumes; generated .env files were preserved."
}
else {
    Write-Host 'Named volumes and generated .env files were preserved.'
}
