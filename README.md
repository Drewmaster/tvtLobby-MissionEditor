# tvtLobby Mission Editor

Use Mission Editor to set up and edit your tvtLobby missions in Arma Reforger Workbench.

**[Download Mission Editor 1.0.11 (ZIP)](https://github.com/Drewmaster/tvtLobby-MissionEditor/releases/download/v1.0.11/tvtLobby-MissionEditor-1.0.11.zip)**

For **tvtLobby 1.0.11**. Update both the addon and editor for this release, then restart Workbench. Later addon updates can keep using this editor unless Workbench asks for a new package.

This release adds mission mod selection with clipboard import/export, publishes settings for Creating and exposes spectator map visibility controls. New missions enable overhead spectator markers by default, with a personal toggle within mission permissions.

## Install

1. Open your saved mission in Workbench with **tvtLobby** enabled as a dependency.
2. Click **tvtLobby Mission Editor** in the World Editor toolbar. In the dialog, click **Open install folder**.
3. Extract the **tvtLobby-MissionEditor** folder from the ZIP into the folder that opens.
4. In the main Workbench window, go to **File → Options → General → Net API**. Turn on **Enabled**, leave **Port** at **5775**, and click **OK**.
5. Click **tvtLobby Mission Editor** again. If the editor is already open, click **Refresh**.

Workbench and Mission Editor connect on the same PC. Your default firewall and router settings are sufficient.

[Installation help](INSTALL.md) · [Changelog](CHANGELOG.md) · [All releases](https://github.com/Drewmaster/tvtLobby-MissionEditor/releases) · [Developer notes](DEVELOPMENT.md)

## Mods and Creating settings

Available with tvtLobby 1.0.11 and Mission Editor 1.0.11.

Open **Mods** to prepare the requirements of this mission:

1. **Scan resources** finds addons referenced by the mission's resources. Review the results; addons selected only by scripts may need adding manually.
2. **Loaded addons...** lets you select addons enabled in the current Workbench session, including **Open with mods**.
3. **Add folder...** reads a local or Workshop addon folder containing `addon.gproj` or `ServerData.json`.
4. Add or remove entries, check the Workbench status and optionally edit required versions. Click **Apply**, then **Save mission**. Removed entries stay excluded from subsequent scans until you restore them or add them explicitly.
5. **Import from clipboard** reads a copied mod list. It adds new mods and updates names and versions for matching IDs, keeping other entries. Review the draft, then click **Apply** and **Save mission**. Invalid input leaves the list unchanged.
6. **Export to clipboard** copies comma-separated `modId`, `name`, `version` objects, matching the game's export without outer `[]`. Paste them inside `game.mods` in the server configuration. The export includes tvtLobby and the mission pack automatically and uses your current draft.

Import accepts this same format or a complete JSON array. Repeated IDs create one entry; the last imported entry supplies its name and version.

This list does not add project dependencies or copy addon files. Different known versions and missing mods produce a warning in Creating; the administrator can still attempt a launch. An incomplete resource scan preserves existing requirements.

**Save mission** also publishes the mission's terrain name and public settings for Creating. Administrators can adjust the settings before launching; changes apply to that launch only. Save existing missions once with the updated addon, then publish the mission pack to expose this information on servers. If the Map column says **Not specified**, the scenario is missing its saved terrain name; this also applies to modded terrains. The packaged Crossroads and Borrowed Ground missions already include this information.

## Random starts

Available with tvtLobby 1.0.10 and Mission Editor 1.0.10.

1. Open **Map**, click **+ Random start**, and place points directly on the map. Press Esc to stop placing. Points can be saved before assigning anything to them.
2. Select a point, or Ctrl-select several points on the map or in the left list. In the right sidebar, tick the groups, vehicles, factions or objects allowed to start there. Search and the type filter help find them.
3. A faction moves together, preserving its authored arrangement and directions. Vehicles linked to a group follow it automatically. To move a combination together, Ctrl-select its rows in the sidebar and click **Move together**; the items must have the same allowed points. **Separate** splits a combination back into its assignments.
4. Choose one method for the **whole mission**: **Independent choice** allows assignments to select the same point; **Distribution without repeats** gives each assignment a different allowed point. A complete faction or combination uses one point. Apply explains if there are too few compatible points.
5. Drag a point to move it; drag its arrow to rotate the whole arrangement. Rotation 0 keeps the original directions. **Preview** shows one complete draw without moving the authored objects. Seed 0 gives a new draw on each mission launch; a positive seed repeats the same selection.
6. Click the map's **Apply** once for all random-start edits. This creates destination prefabs in the separate **tvtLobby_RandomStarts** world layer. Existing embedded points are converted on their next Apply. **Save world** saves the points and assignments together.
7. You can move or rotate the prefabs directly in World Editor, or select points on the map and click **Select in World Editor**. Use the map's **Refresh** to see native edits; mission startup always reads the current prefab transforms, without requiring another map Apply. Selection happens before briefing, once per mission; joining or reconnecting does not reroll it.

The prefab is **Prefabs/tvtLobby/Mission/tvtLobby_RandomStart.et**. You can also place or duplicate it in World Editor, then assign its destinations through the map. It has an editor-only sphere and direction arrow, with no visible mesh or collision in game. Changing the label keeps assignments; the entity name identifies the point. Locked or inherited points can be used as destinations but cannot be moved or deleted through the map. Deleting a world point removes it from available destinations. If an assignment loses its last point, its participants stay at their authored positions. Deleting all start points restores ordinary mission starts without another map Apply.

**Follow terrain** preserves each member's original ground clearance. Turn it off and set an absolute height for a bridge or raised platform. Check space around destinations, particularly for large formations and modded objects; different points do not guarantee that nearby formations cannot overlap. Linked freeze zones follow their groups. A shared vehicle or freeze zone cannot belong to separate assignments: combine the affected groups or assign separate vehicles/zones.
