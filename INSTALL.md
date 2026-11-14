# Install tvtLobby Mission Editor

Download the Mission Editor ZIP from [GitHub Releases](https://github.com/Drewmaster/tvtLobby-MissionEditor/releases/latest).

**Mission Editor 1.0.7 requires tvtLobby 1.0.7.** For tvtLobby 1.0.5 or 1.0.6, use [Mission Editor 1.0.5](https://github.com/Drewmaster/tvtLobby-MissionEditor/releases/tag/v1.0.5).

1. Open your saved mission in Workbench with **tvtLobby** enabled as a dependency.
2. Click **tvtLobby Mission Editor** in the World Editor toolbar or select **Plugins → tvtLobby: Mission Editor**. In the dialog, click **Open install folder**.
3. Extract the **tvtLobby-MissionEditor** folder from the ZIP into the folder that opens. If the folder does not open, click **Copy install path** and paste the path into File Explorer. The application should be at `profile\tvtLobby-MissionEditor\bin\tvtLobby.MissionEditor.exe`.
4. In the main Workbench window, go to **File → Options → General → Net API**. Turn on **Enabled**, leave **Port** at **5775**, and click **OK**.
5. Click **tvtLobby Mission Editor** again. If the editor is already open, click **Refresh**.

Workbench and Mission Editor connect on the same PC. Your default firewall and router settings are sufficient. If you use a custom port, enter the same number in the Mission Editor plug-in settings.

If Workbench reports that it prepared a session but could not start the editor, keep Workbench open and double-click `bin\tvtLobby.MissionEditor.exe` in the installed folder.

## Update

Restart Workbench after updating the tvtLobby addon. If Workbench reports an incompatible or incomplete package, close Mission Editor and click **Delete old version**. This keeps the old package as a backup and opens the install folder. Extract the new **tvtLobby-MissionEditor** folder there and click **Mission Editor** again.

With earlier addon versions, open the install folder and replace **tvtLobby-MissionEditor** manually. Your missions and drafts are stored separately.

The ZIP includes Microsoft's DirectXTex texture converter. [License](vendor/DirectXTex/LICENSE) · [Source and checksum](vendor/DirectXTex/source.json)
