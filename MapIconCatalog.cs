using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TvtLobby.MissionEditor
{
    public sealed class MapIconSet
    {
        public string Name,Resource,Error;
        public List<MapIcon> Icons=new List<MapIcon>();
        public override string ToString(){return Name+"  ·  "+Icons.Count;}
    }
    public static class MapIconCatalog
    {
        // Resource identifiers, not an exported copy of the game's icon list.
        // Symbols are enumerated from the currently installed imagesets.
        public static readonly string[] Resources={
            "UI/Imagesets/buttons/pc_buttons.imageset",
            "UI/Imagesets/Characters/CharacterWrapperUI.imageset",
            "UI/Imagesets/Commanding/Commanding_Icons.imageset",
            "UI/Imagesets/Conflict/conflict-icons-bw.imageset",
            "UI/Imagesets/Conflict/conflict-icons.imageset",
            "UI/Imagesets/Conflict/ConflictIconsSet.imageset",
            "UI/Imagesets/Conflict/services-icons-bw.imageset",
            "UI/Imagesets/Editor/Icons/EditableEntities.imageset",
            "UI/Imagesets/Editor/Nametag-filters-attributes.imageset",
            "UI/Imagesets/EffectModule/EffectModule.imageset",
            "UI/Imagesets/Notifications/NotificationIcons.imageset",
            "UI/Imagesets/SupportStations/InventoryHints.imageset",
            "UI/Imagesets/SupportStations/Notifications.imageset",
            "UI/Imagesets/Tasks/Task_Icons.imageset",
            "UI/Imagesets/Tutorial-Courses/Tutorial-Courses.imageset",
            "UI/Imagesets/Tutorial-Waypoints/Tutorial-Waypoints.imageset",
            "UI/Imagesets/TutorialHud/Tutorial-Main-Icon-Wrapper.imageset",
            "UI/Imagesets/TutorialHud/TutorialHUD.imageset",
            "UI/Imagesets/TutorialHud/TutorialHUD_White.imageset",
            "UI/Textures/Cursor/cursors.imageset",
            "UI/Textures/DeployMenu/Objectives-Briefing/Objectives-briefing.imageset",
            "UI/Textures/GroupManagement/FlagIcons/GroupFlagsBlufor.imageset",
            "UI/Textures/GroupManagement/FlagIcons/GroupFlagsIndfor.imageset",
            "UI/Textures/GroupManagement/FlagIcons/GroupFlagsOpfor.imageset",
            "UI/Textures/Icons/icons_keyboard_64.imageset",
            "UI/Textures/Icons/icons_mapMarkersUI.imageset",
            "UI/Textures/Icons/icons_mapMarkersUI_drawing/icons_DR_mapMarkersUI.imageset",
            "UI/Textures/Icons/icons_wrapperUI-128.imageset",
            "UI/Textures/Icons/icons_wrapperUI.imageset",
            "UI/Textures/InventoryIcons/InventoryIcons.imageset",
            "UI/Textures/InventoryIcons/Medical/Medical-icons.imageset",
            "UI/Textures/Map/topographicIcons/icons_topographic_map.imageset",
            "UI/Textures/MilitaryIcons/MilitaryIcons.imageset",
            "UI/Textures/Nametags/nametagicons.imageset",
            "UI/Textures/RadialMenu/Radial-Menu-icons.imageset",
            "UI/Textures/StanceIndicator/icons_stanceIndicator.imageset"
        };
        public static string Key(MapIcon icon){return MapAssets.Relative(icon.IconResource).ToLowerInvariant()+"|"+icon.Quad;}
        public static List<MapIconSet> Read(MapReply scene,MapAssets assets,Func<bool> cancelled)
        {
            var result=new List<MapIconSet>{new MapIconSet{Name="Mission map symbols",Resource="",Icons=scene.Icons.ToList()}};
            foreach(string resource in Resources)
            {
                if(cancelled())break;
                string label=resource.Replace("UI/Imagesets/","").Replace("UI/Textures/","").Replace(".imageset","").Replace("/"," / ");
                var set=new MapIconSet{Name=label,Resource=resource};
                try{set.Icons=assets.ReadIcons(resource);}
                catch(Exception error){set.Error=error.Message;}
                result.Add(set);
            }
            return result;
        }
    }
}
