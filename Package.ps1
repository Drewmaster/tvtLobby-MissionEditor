param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$folder = 'tvtLobby-MissionEditor'
$vendor = Join-Path $PSScriptRoot 'vendor/DirectXTex'
$converter = Join-Path $vendor 'texconv.exe'
$provenance = Get-Content -LiteralPath (Join-Path $vendor 'source.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $converter -Algorithm SHA256).Hash -ne $provenance.SHA256) {
    throw 'DirectXTex does not match the SHA-256 recorded in vendor/DirectXTex/source.json.'
}

$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$staging = Join-Path $output ('.mission-editor-' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $staging $folder
New-Item -ItemType Directory -Path $package | Out-Null
try {
    $application = & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory (Join-Path $package 'bin')
    $version = (Get-Item -LiteralPath $application).VersionInfo.ProductVersion
    if ($version -cnotmatch '^\d+\.\d+\.\d+$') {
        throw "Invalid Mission Editor version in the compiled application: $version"
    }
    if ($Tag -and $Tag -cne "v$version") {
        throw "Release tag $Tag does not match the compiled Mission Editor version $version."
    }

    $assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($application))
    $client = $assembly.GetType('TvtLobby.MissionEditor.WorkbenchClient', $true)
    $protocol = [int]$client.GetField('EditorProtocol').GetRawConstantValue()
    if ($protocol -lt 1) { throw 'The compiled Mission Editor protocol must be positive.' }
    [ordered]@{ Version = $version; Protocol = $protocol } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $package 'package.json') -Encoding ascii

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'INSTALL.md') -Destination (Join-Path $package 'README.md')
    $license = Join-Path $PSScriptRoot 'LICENSE'
    if (Test-Path -LiteralPath $license -PathType Leaf) {
        Copy-Item -LiteralPath $license -Destination $package
    }
    $packageVendor = Join-Path $package 'vendor/DirectXTex'
    New-Item -ItemType Directory -Path $packageVendor | Out-Null
    Copy-Item -LiteralPath @($converter, (Join-Path $vendor 'LICENSE'), (Join-Path $vendor 'source.json')) -Destination $packageVendor

    $archiveName = "$folder-$version.zip"
    $archive = Join-Path $output $archiveName
    if (Test-Path -LiteralPath $archive) { throw "Archive already exists: $archive" }
    $stagedArchive = Join-Path $staging $archiveName
    [IO.Compression.ZipFile]::CreateFromDirectory($package, $stagedArchive, [IO.Compression.CompressionLevel]::Optimal, $true)
    Move-Item -LiteralPath $stagedArchive -Destination $archive
    Write-Output $archive
} finally {
    $resolvedStaging = (Resolve-Path -LiteralPath $staging).ProviderPath
    $resolvedOutput = (Resolve-Path -LiteralPath $output).ProviderPath
    if ((Split-Path -Parent $resolvedStaging) -ine $resolvedOutput -or
        (Split-Path -Leaf $resolvedStaging) -cnotmatch '^\.mission-editor-[0-9a-f]{32}$') {
        throw "Unexpected temporary package directory: $resolvedStaging"
    }
    Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
}
