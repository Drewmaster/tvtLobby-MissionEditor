# Mission Editor changelog

## 1.0.10

- Load buildings, roads and other map details from packed Workshop terrains.
- Fix map loading with mixed numeric and boolean mission fields.
- Add battlefield cleanup controls in Advanced settings.
- Place random-start points directly on the map, then assign groups, vehicles, factions, objects or combinations in the sidebar.
- Store start points as prefabs in a separate world layer; moving them changes the destination, and deleting the last allowed point restores authored starts.
- Keep factions in their authored formation and automatically move linked vehicles with their groups.
- Choose independent selection or distribution without repeats for the whole mission, preview the result and apply all edits together.

Use with **tvtLobby 1.0.10**. Update the addon, restart Workbench and replace the editor folder with the 1.0.10 ZIP when prompted. Missions and drafts are kept separately.

## 1.0.9

- Check the loaded addon identity when connecting, before opening mission data.
- Show the running editor version in the window title.
- Include the loaded integration version and addon location in compatibility messages.

Use with **tvtLobby 1.0.9**. Update both for this release, restart Workbench, then replace the editor folder using **Delete old version** and the new ZIP. Later gameplay-only addon updates can keep using this package.

## 1.0.7

- Prepare all briefing sections before applying them together.
- Apply group sorting and vehicle assignments together with one Apply.
- Assign vehicles by dragging between groups and vehicles in the Assignments tab.
- Configure friendly nametag distance, with a default of 3 metres.

Requires **tvtLobby 1.0.8**. The published Workshop addon 1.0.7 still uses the earlier editor compatibility contract. Restart Workbench after updating the addon. If prompted, close Mission Editor, use **Delete old version**, and extract the new ZIP into the install folder.

## 1.0.5

- Initial standalone Mission Editor package for Workshop users.

Compatible with Workshop **tvtLobby 1.0.5–1.0.7**; newer authoring features require the updated addon and editor.
