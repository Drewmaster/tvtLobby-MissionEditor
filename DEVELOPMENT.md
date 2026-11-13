# Mission Editor development

## Build from source

On Windows, run [Build.ps1](Build.ps1) in PowerShell to build `bin\tvtLobby.MissionEditor.exe` with the .NET Framework C# compiler. The tvtLobby addon provides the Workbench integration and mission templates. To create the installation ZIP, run [Package.ps1](Package.ps1) from PowerShell 7:

```powershell
.\Package.ps1 -OutputDirectory "$env:TEMP\tvtLobby-MissionEditor"
```

[GitHub Actions](.github/workflows/release.yml) runs that build on a version tag. Each release links to its source commit and build log and lists the ZIP's SHA-256. To verify the download, compare the release's hash with the output of:

```powershell
Get-FileHash -Algorithm SHA256 .\tvtLobby-MissionEditor-1.0.5.zip
```

Public builds also have a [GitHub artifact attestation](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations). To verify which repository and build produced the ZIP, use `gh attestation verify <path-to-zip> --repo <owner>/<repository>`.

The package includes Microsoft's [DirectXTex texture converter](https://github.com/microsoft/DirectXTex/releases/tag/may2026) for map images. Its [MIT license](vendor/DirectXTex/LICENSE) and [source and SHA-256](vendor/DirectXTex/source.json) accompany it.
