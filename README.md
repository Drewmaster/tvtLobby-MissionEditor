# tvtLobby Mission Editor

Use Mission Editor to set up and edit your tvtLobby missions in Arma Reforger Workbench.

**[Download Mission Editor 1.0.10 (ZIP)](https://github.com/Drewmaster/tvtLobby-MissionEditor/releases/download/v1.0.10/tvtLobby-MissionEditor-1.0.10.zip)**

For **tvtLobby 1.0.10**. Update both the addon and editor for this release, then restart Workbench. Later addon updates can keep using this editor unless Workbench asks for a new package.

This release adds random starts on the mission map, restores details on Workshop terrains and exposes battlefield cleanup settings.

## Install

1. Open your saved mission in Workbench with **tvtLobby** enabled as a dependency.
2. Click **tvtLobby Mission Editor** in the World Editor toolbar. In the dialog, click **Open install folder**.
3. Extract the **tvtLobby-MissionEditor** folder from the ZIP into the folder that opens.
4. In the main Workbench window, go to **File → Options → General → Net API**. Turn on **Enabled**, leave **Port** at **5775**, and click **OK**.
5. Click **tvtLobby Mission Editor** again. If the editor is already open, click **Refresh**.

Workbench and Mission Editor connect on the same PC. Your default firewall and router settings are sufficient.

[Installation help](INSTALL.md) · [Changelog](CHANGELOG.md) · [All releases](https://github.com/Drewmaster/tvtLobby-MissionEditor/releases) · [Developer notes](DEVELOPMENT.md)

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
