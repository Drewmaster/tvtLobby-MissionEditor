# Mission Editor development

## Build from source

On Windows, run [Build.ps1](Build.ps1) in PowerShell to build `bin\tvtLobby.MissionEditor.exe` with the .NET Framework C# compiler. The tvtLobby addon provides the Workbench integration and mission templates. To create the installation ZIP, run [Package.ps1](Package.ps1) from PowerShell 7:

```powershell
.\Package.ps1 -OutputDirectory "$env:TEMP\tvtLobby-MissionEditor"
```

[GitHub Actions](.github/workflows/release.yml) runs that build on a version tag. Each release links to its source commit and build log and lists the ZIP's SHA-256. To verify the download, compare the release's hash with the output of:

```powershell
Get-FileHash -Algorithm SHA256 .\tvtLobby-MissionEditor-1.0.10.zip
```

Public builds also have a [GitHub artifact attestation](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations). To verify which repository and build produced the ZIP, use `gh attestation verify <path-to-zip> --repo <owner>/<repository>`.

The package includes Microsoft's [DirectXTex texture converter](https://github.com/microsoft/DirectXTex/releases/tag/may2026) for map images. Its [MIT license](vendor/DirectXTex/LICENSE) and [source and SHA-256](vendor/DirectXTex/source.json) accompany it.

## Compatibility

Mission Editor 1.0.10 and tvtLobby 1.0.10 retain protocol 3. The addon now requires editor 1.0.10 for random-start authoring, Workshop map loading and cleanup settings; its launcher checks the minimum package version as well as the protocol. This contract requires the loaded bridge version, required editor version and addon location in both the session file and the live `hello` reply. The editor verifies those identities match before loading mission data. Workbench rejects older installed protocols before launch; all operation handlers also reject them before edits. The package manifest is generated from the compiled executable.

Editor and addon release numbers are independent. Only change the protocol when the editor/Workbench contract requires a matching package, not for unrelated addon updates. `BRIDGE_VERSION` identifies the Workbench integration build; it does not need to follow every gameplay release. Future compatible bridge builds may use the same editor. The client's running version must meet the minimum requested by the bridge.

Historical pairs: tvtLobby 1.0.8 / editor 1.0.7 use protocol 2; Workshop tvtLobby 1.0.5–1.0.7 / editor 1.0.5 use protocol 1. The 1.0.7 package includes the coordinated briefing/group batch operations and Advanced settings schema 11.
