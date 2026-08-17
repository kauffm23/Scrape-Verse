[CmdletBinding()]
param()

$workspace = Split-Path -Parent $PSScriptRoot
$dotnetState = Join-Path $workspace '.dotnet-home'
$packageCache = Join-Path $workspace '.nuget\packages'
$env:DOTNET_CLI_HOME = $dotnetState
$env:NUGET_PACKAGES = $packageCache
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:ASPNETCORE_ENVIRONMENT = 'Development'

Write-Host 'Starting LabWatch fixture at http://localhost:5181/advisories'
Start-Process dotnet -ArgumentList 'run','--project','src/LabWatch.Fixture','--no-launch-profile','--urls','http://localhost:5181' -WorkingDirectory $workspace -WindowStyle Hidden
Write-Host 'Starting LabWatch dashboard at http://localhost:5180'
dotnet run --project (Join-Path $workspace 'src\LabWatch.Web') --no-launch-profile --urls http://localhost:5180
