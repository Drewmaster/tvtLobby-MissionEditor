using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed partial class AdvancedSettingsPage
    {
        ForcesView mapForces;
        List<string> selectedMapGroups=new List<string>();
        readonly TableLayoutPanel mapGroups=EditorTheme.Stack(12);
        readonly Label mapCoverage=EditorTheme.Label("",9,EditorTheme.Muted);
        readonly Label mapWarning=EditorTheme.Label("",9,EditorTheme.Amber);
        void BuildMapSettings()
        {
            var map=Section("Map & markers");
            tabs.Group(map,"Vanilla & player markers","Player-created, authored and automatic markers have independent rules.");
            Check(map,"VanillaLeaders","Show vanilla squad-leader markers","Live positions from the native squad system. Hidden by default.");
            Number(map,"MarkerLimit","Player marker limit","Per player. Adding a marker at the limit replaces that player's oldest marker.",1,500);
            Choice(map,"MarkerDeletion","Who can delete player markers",new[]{"Keep mission header permissions","Author and administrators","Own faction and administrators","Administrators only"},"Only player-created markers. Authored and automatic markers remain protected.");
            tabs.Group(map,"Automatic group starts","The native SCR_GroupIdentityComponent supplies each group's symbol.");
            Check(map,"StartMarkers","Enable automatic start markers","");
            Check(map,"StartEmpty","Include empty playable groups","A group does not need an occupied slot at capture time.");
            Choice(map,"StartAudience","Visible to",new[]{"Own faction","Own and allied factions","All players"});
            Check(map,"StartAdminSame","Admins follow the same visibility rules","Off allows administrators to see all included starts.");
            tabs.Group(map,"Position & lifetime","Capture once; a start marker never tracks a moving player.");
            Choice(map,"StartPosition","Position source",new[]{"Actual commander position","Authored group position"},"If the commander has not spawned yet, wait for its body; never use 0,0 as a fallback.");
            string[] phases={"Start of Waiting","Start of Lobby","Start of Pre-brief","Start of Briefing","Start of freeze"};
            Choice(map,"StartCapture","Capture position at",phases,"If an optional phase is skipped, capture at the next available phase.");
            Choice(map,"StartShow","Show from",phases,"Cannot precede capture. Opening the map does not change the saved position.");
            Choice(map,"StartHide","Hide at",new[]{"Start of freeze","End of freeze / combat start","Start of Debriefing"});
            Check(map,"StartKeepIndex","Keep group index after hiding markers","The search list still centres and zooms to the saved start. It remains own-faction only.");
            EditorTheme.Add(map,mapWarning);
            tabs.Group(map,"Appearance & group selection");
            Check(map,"StartLabels","Show callsign labels","");
            Choice(map,"StartWhite","Marker colour",new[]{"Faction colour","Neutral white"});
            Number(map,"StartSize","Icon size","24–96 UI units; the native default is 48. Map zoom does not stretch the symbol.",24,96);
            Check(map,"StartAll","Include all playable groups","New playable groups are included automatically. Turn off to choose individual groups below.");
            mapGroups.BackColor=EditorTheme.Panel;EditorTheme.Add(map,mapCoverage);EditorTheme.Add(map,mapGroups);
            Note(map,"Groups are taken from the current world. Use Groups & vehicles to adjust callsigns and faction membership. Authored icons, areas and lines are edited separately in Map & markers.");
        }
        void Number(TableLayoutPanel body,string key,string name,string help,int min,int max)
        {
            var n=new NumericUpDown{Minimum=min,Maximum=max,Width=115,ThousandsSeparator=true,DecimalPlaces=key=="StartSize"?1:0,BackColor=EditorTheme.Panel,ForeColor=EditorTheme.Text};
            controls[key]=n;n.ValueChanged+=(s,e)=>Changed();Row(body,name,help,n);
        }
        static string MapGroupReference(EditorGroup group)
        {
            return !String.IsNullOrEmpty(group.Id)?group.Id:group.Name!="(unnamed group)"&&!String.IsNullOrEmpty(group.Name)?group.Name:"@"+group.Key;
        }
        void RebuildMapGroups()
        {
            bool wasLoading=loading;loading=true;
            while(mapGroups.Controls.Count>0)mapGroups.Controls[0].Dispose();mapGroups.RowCount=0;mapGroups.RowStyles.Clear();
            var found=new HashSet<string>();
            if(mapForces!=null)foreach(var group in mapForces.OrderedGroups().Where(g=>g.Playable!=0))
            {
                string reference=MapGroupReference(group);
                // Keep the authored alias so a refresh never silently rewrites references.
                string selected=selectedMapGroups.FirstOrDefault(x=>x==reference||x=="@"+group.Key||x==group.Name);
                if(selected!=null)reference=selected;
                found.Add(reference);string captured=reference;
                var row=new CheckBox{AutoSize=true,Dock=DockStyle.Top,UseMnemonic=false,Text=group.Callsign+"  ·  "+group.EngineKey+"  ·  "+group.Name,Checked=selected!=null,Margin=new Padding(0,8,0,8)};
                row.CheckedChanged+=(s,e)=>{if(loading)return;if(row.Checked){if(!selectedMapGroups.Contains(captured))selectedMapGroups.Add(captured);}else selectedMapGroups.Remove(captured);Changed();};
                EditorTheme.Add(mapGroups,row);
            }
            foreach(string missing in selectedMapGroups.Where(x=>!found.Contains(x)).ToList())
            {
                string captured=missing;var row=new CheckBox{AutoSize=true,Dock=DockStyle.Top,Text="Missing group: "+missing,Checked=true,ForeColor=EditorTheme.Amber,UseMnemonic=false};
                row.CheckedChanged+=(s,e)=>{if(!loading){if(row.Checked){if(!selectedMapGroups.Contains(captured))selectedMapGroups.Add(captured);}else selectedMapGroups.Remove(captured);Changed();}};
                EditorTheme.Add(mapGroups,row);
            }
            if(mapGroups.Controls.Count==0)EditorTheme.Add(mapGroups,EditorTheme.Label("No playable groups yet. Place groups in World Editor, then Refresh.",9,EditorTheme.Muted));
            loading=wasLoading;
        }
        void MapState()
        {
            if(!controls.ContainsKey("StartMarkers"))return;
            bool enabled=((CheckBox)controls["StartMarkers"]).Checked,all=((CheckBox)controls["StartAll"]).Checked;
            foreach(var pair in controls.Where(p=>p.Key.StartsWith("Start")&&p.Key!="StartMarkers"))pair.Value.Enabled=enabled;
            mapGroups.Visible=!all;mapGroups.Enabled=enabled&&!all;
            int total=mapForces==null?0:mapForces.Groups.Count(g=>g.Playable!=0);
            mapCoverage.Text=all?"All "+total+" playable groups included":"Selected: "+selectedMapGroups.Count+" / "+total+" playable groups";
            var draft=ReadFields();string problem=draft.Validate();mapWarning.Text=enabled&&problem!=null?problem:"";mapWarning.Visible=mapWarning.Text.Length>0;
        }
    }
}
