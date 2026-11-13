param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler is missing.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    ('/out:' + (Join-Path $OutputDirectory 'tvtLobby.MissionEditor.exe')) `
    (Join-Path $PSScriptRoot 'MissionEditor.cs') (Join-Path $PSScriptRoot 'EditorOperationRetry.cs') (Join-Path $PSScriptRoot 'EditorSaveRecovery.cs') (Join-Path $PSScriptRoot 'FoundationClient.cs') (Join-Path $PSScriptRoot 'EditorWindow.cs') (Join-Path $PSScriptRoot 'ForcesPage.cs') (Join-Path $PSScriptRoot 'MapModel.cs') `
    (Join-Path $PSScriptRoot 'MapStyle.cs') (Join-Path $PSScriptRoot 'MapAssets.cs') (Join-Path $PSScriptRoot 'MapIconCatalog.cs') (Join-Path $PSScriptRoot 'MapIconPicker.cs') (Join-Path $PSScriptRoot 'MapCanvas.cs') (Join-Path $PSScriptRoot 'MapTopology.cs') (Join-Path $PSScriptRoot 'MapWindow.cs') (Join-Path $PSScriptRoot 'MapTexture.cs') (Join-Path $PSScriptRoot 'MapArchive.cs') (Join-Path $PSScriptRoot 'VehiclesPanel.cs') (Join-Path $PSScriptRoot 'NativeFactionDialog.cs') (Join-Path $PSScriptRoot 'MissionSettings.cs') (Join-Path $PSScriptRoot 'AdvancedSettings.cs') (Join-Path $PSScriptRoot 'MapSettingsPage.cs') (Join-Path $PSScriptRoot 'VanillaSettingsPage.cs') (Join-Path $PSScriptRoot 'FreezeZonesPage.cs') (Join-Path $PSScriptRoot 'EditorPresentation.cs') (Join-Path $PSScriptRoot 'EditorPages.cs') (Join-Path $PSScriptRoot 'FactionCardsPage.cs') (Join-Path $PSScriptRoot 'GroupPresentation.cs') (Join-Path $PSScriptRoot 'MapAppearanceEditor.cs') (Join-Path $PSScriptRoot 'MapInspector.cs') (Join-Path $PSScriptRoot 'MissionEndModel.cs') (Join-Path $PSScriptRoot 'MissionEndPage.cs') (Join-Path $PSScriptRoot 'CaptureExpression.cs') (Join-Path $PSScriptRoot 'ObjectivesModel.cs') (Join-Path $PSScriptRoot 'ObjectivesPage.cs') (Join-Path $PSScriptRoot 'ObjectiveNotifications.cs') (Join-Path $PSScriptRoot 'BriefingModel.cs') (Join-Path $PSScriptRoot 'BriefingTextEditor.cs') (Join-Path $PSScriptRoot 'BriefingPage.cs')
if ($LASTEXITCODE -ne 0) { throw 'Mission Editor build failed.' }
Write-Output (Join-Path $OutputDirectory 'tvtLobby.MissionEditor.exe')
