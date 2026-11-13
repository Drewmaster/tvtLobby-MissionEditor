param([Parameter(Mandatory)][string]$SessionPath)
$ErrorActionPreference = 'Stop'
$session = (Resolve-Path -LiteralPath $SessionPath).Path
$application = Join-Path $PSScriptRoot 'bin/tvtLobby.MissionEditor.exe'
if (-not (Test-Path -LiteralPath $application)) { & (Join-Path $PSScriptRoot 'Build.ps1') | Out-Null }
# Interactive window explicitly requested by this launcher; no console helper is kept alive.
Start-Process -FilePath $application -ArgumentList @('--session', ('"' + $session + '"')) -WindowStyle Normal -PassThru
