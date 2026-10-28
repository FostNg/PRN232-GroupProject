param([int]$Port = 5041, [switch]$Configure)
$ErrorActionPreference = 'Stop'
if ($Port -lt 1 -or $Port -gt 65535) { throw 'Invalid API port.' }
$project = Join-Path $PSScriptRoot 'PRN232-RoomRum/RoomRum/RoomRum.csproj'
$releaseDirectory = Join-Path $PSScriptRoot 'PRN232-RoomRum/artifacts/bin/RoomRum/Release/net10.0'
if (-not (Test-Path (Join-Path $releaseDirectory 'RoomRum.dll'))) {
    throw 'Build the Release backend before running this script.'
}
if ($Configure) { & (Join-Path $PSScriptRoot 'Setup-GmailSecrets.ps1') }
# Only replace a Release API from this exact workspace; never stop unrelated listeners.
if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
    $owners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique)
    $apiExecutable = [IO.Path]::GetFullPath((Join-Path $releaseDirectory 'RoomRum.exe'))
    $runningApi = @($owners | ForEach-Object { Get-Process -Id $_ -ErrorAction Stop })
    foreach ($process in $runningApi) {
        if ($process.Path -ne $apiExecutable) { throw "Port $Port is used by another process. Stop it manually or select a different port." }
    }
    foreach ($process in $runningApi) { Stop-Process -Id $process.Id; $process.WaitForExit(5000) | Out-Null }
}
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    Write-Host 'Starting RoomRum in Development; SMTP credentials are loaded from .NET User Secrets.'
    Write-Host "API: http://localhost:$Port/swagger. First-time setup: ./Setup-GmailSecrets.ps1"
    & dotnet run --project $project --launch-profile RoomRum --configuration Release --no-build --no-restore --urls "http://localhost:$Port" --EmailVerification:ApiBaseUrl="http://localhost:$Port"
    if ($LASTEXITCODE -ne 0) { throw 'Backend startup failed; check whether the API port is already in use.' }
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
}
