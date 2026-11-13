# Install tvtLobby Mission Editor

Download the Mission Editor ZIP from [GitHub Releases](https://github.com/Drewmaster/tvtLobby-MissionEditor/releases/latest).

1. Open your saved mission in Workbench with **tvtLobby** enabled as a dependency.
2. Click **tvtLobby Mission Editor** in the World Editor toolbar or select **Plugins → tvtLobby: Mission Editor**. In the dialog, click **Open install folder**.
3. Extract the **tvtLobby-MissionEditor** folder from the ZIP into the folder that opens. If the folder does not open, click **Copy install path** and paste the path into File Explorer. The application should be at `profile\tvtLobby-MissionEditor\bin\tvtLobby.MissionEditor.exe`.
4. In the main Workbench window, go to **File → Options → General → Net API**. Turn on **Enabled**, leave **Port** at **5775**, and click **OK**.
5. Click **tvtLobby Mission Editor** again. If the editor is already open, click **Refresh**.

Workbench and Mission Editor connect on the same PC. Your default firewall and router settings are sufficient. If you use a custom port, enter the same number in the Mission Editor plug-in settings.

If Workbench reports that it prepared a session but could not start the editor, keep Workbench open and double-click `bin\tvtLobby.MissionEditor.exe` in the installed folder.

## Update

Close Mission Editor and replace its installed **tvtLobby-MissionEditor** folder with the one from the new ZIP. Restart Workbench after updating the tvtLobby addon. Your missions and drafts are stored separately.

The ZIP includes Microsoft's DirectXTex texture converter. [License](vendor/DirectXTex/LICENSE) · [Source and checksum](vendor/DirectXTex/source.json)
