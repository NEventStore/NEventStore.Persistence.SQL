Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptDirectory
$projectPrefix = 'neventstore-sql'
$maximumEnvironmentNameLength = 48
$removeData = $false
$environmentName = $null

function Normalize-EnvironmentName {
	param([Parameter(Mandatory)][string] $Value)
	$normalized = [Regex]::Replace($Value.ToLowerInvariant(), '[^a-z0-9]+', '-').Trim('-')
	if ([string]::IsNullOrWhiteSpace($normalized)) { throw "Environment name '$Value' does not contain any supported characters." }
	if ($normalized.Length -gt $maximumEnvironmentNameLength) { $normalized = $normalized.Substring(0, $maximumEnvironmentNameLength).TrimEnd('-') }
	return $normalized
}

foreach ($argument in $args) {
	switch ($argument) {
		'--remove-data' { $removeData = $true; continue }
		'-RemoveData' { $removeData = $true; continue }
		default {
			if ($argument.StartsWith('-')) { throw "Unknown option: $argument" }
			if ($null -ne $environmentName) { throw 'Only one environment name may be provided.' }
			$environmentName = $argument
		}
	}
}

if ([string]::IsNullOrWhiteSpace($environmentName)) { $environmentName = Split-Path -Leaf $repositoryRoot }
$normalizedEnvironmentName = Normalize-EnvironmentName $environmentName
$portMode = if ($normalizedEnvironmentName -in @('debug', 'test', 'ci')) { $normalizedEnvironmentName } else { 'dynamic' }
$projectName = "$projectPrefix-$normalizedEnvironmentName"
$composeArguments = @('compose', '--project-name', $projectName, '--file', (Join-Path $scriptDirectory 'docker-compose.yml'), '--file', (Join-Path $scriptDirectory "docker-compose.$portMode.yml"), 'down')
if ($removeData) { $composeArguments += '--volumes' }

Write-Host "Stopping '$projectName'..."
& docker @composeArguments
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed with exit code $LASTEXITCODE." }
