# tvtLobby Mission Editor

A Windows companion application for the tvtLobby addon in Arma Reforger Workbench. It edits the open mission through the Workbench Net API on `127.0.0.1` and saves mission data to your addon.

## Install

1. Download `tvtLobby-MissionEditor-<version>.zip` from **Releases → Assets**.
2. Open a saved world in your mission addon with tvtLobby enabled as a dependency. Select **tvtLobby Mission Editor** in the toolbar or **Plugins → tvtLobby: Mission Editor**.
3. Choose **Open install folder** and extract the `tvtLobby-MissionEditor` folder from the ZIP into the folder that opens. Use **Copy install path** if you need to open the location manually. The executable should be at `profile\tvtLobby-MissionEditor\bin\tvtLobby.MissionEditor.exe`.
4. Select **Mission Editor** again. If prompted to enable Net API, open **File → Options → General → Net API** in Workbench, enable it on port **5775**, and select **Refresh** in Mission Editor.

If Workbench prepares a session and reports a launch error, keep Workbench open and run `bin\tvtLobby.MissionEditor.exe` from the installed folder.

To update, close Mission Editor and replace the installed `tvtLobby-MissionEditor` folder. Restart Workbench after updating the addon. Mission files and local drafts live separately. Workbench checks the protocol in `package.json`, so a compatible editor package can serve multiple Workshop releases.

The package includes Microsoft's DirectXTex texture converter for map images. See [its license](vendor/DirectXTex/LICENSE) and [download source and SHA-256](vendor/DirectXTex/source.json).
