using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class FreezeCoverage
    {
        public string Group {get;set;} public string Zone {get;set;} public decimal Radius {get;set;}
    }
    public sealed class FreezeZoneSettings
    {
        public int Version {get;set;} public int Mode {get;set;}
        public decimal Radius {get;set;} public decimal WarningBand {get;set;}
        public string Template {get;set;} public string Problem {get;set;}
        public List<FreezeCoverage> Coverage {get;set;} public List<TaskChoice> Zones {get;set;}
        public FreezeZoneSettings(){Version=3;Mode=1;Radius=50;WarningBand=10;Template="{7846A93EA395CFBA}PrefabsEditable/RestrictionZone/E_EditorRestrictionZoneBase.et";Problem="";Coverage=new List<FreezeCoverage>();Zones=new List<TaskChoice>();}
        public FreezeZoneSettings Clone(){var json=new JavaScriptSerializer();return json.Deserialize<FreezeZoneSettings>(json.Serialize(this));}
        public string Fingerprint(){return EditorJson.Serialize(this);}
        public string Validate()
        {
            if(Version!=3)return "Reopen Mission Editor to load freeze areas.";
            if(!String.IsNullOrEmpty(Problem))return Problem;
            if(Mode<0||Mode>3)return "Choose a supported area mode and consequence.";
            if(Radius<1||Radius>5000||WarningBand<0.1m||WarningBand>5000||((Mode==1||Mode==3)&&WarningBand>Radius))return "Kill radius must be 1–5000m. Warning must start inside it; leave at least 0.1m between the two boundaries.";
            if((Mode==1||Mode==3)&&!Template.EndsWith(".et",StringComparison.OrdinalIgnoreCase))return "Choose a registered vanilla restriction-zone prefab (.et).";
            if(Coverage==null||Coverage.Any(x=>x==null||String.IsNullOrEmpty(x.Group))||Coverage.Select(x=>x.Group).Distinct().Count()!=Coverage.Count)return "Missing or duplicate group assignment.";
            if(Coverage.Any(x=>x.Radius!=0 && (x.Radius<1||x.Radius>5000)))return "Group radius must be 0 (mission default) or 1–5000m.";
            if(Coverage.Any(x=>x.Radius>0 && String.IsNullOrEmpty(x.Zone) && x.Radius<WarningBand))return "Warning band exceeds an overridden group radius.";
            if(Coverage.Any(x=>!String.IsNullOrEmpty(x.Zone)&&x.Zone!="-"&&!Zones.Any(z=>z.Key==x.Zone)))return "An assigned zone is missing. Choose another zone or No zone.";
            return null;
        }
    }
    public sealed class FreezeZonesPage : UserControl
    {
        readonly SettingsDeck tabs=new SettingsDeck();
        readonly Dictionary<string,Control> controls=new Dictionary<string,Control>();
        readonly Dictionary<string,decimal> radii=new Dictionary<string,decimal>();
        readonly Dictionary<string,string> coverage=new Dictionary<string,string>();
        readonly TableLayoutPanel groupRows=EditorTheme.Stack(12);
        readonly Label summary=EditorTheme.Label("",9,EditorTheme.Muted),message=EditorTheme.Label("",9,EditorTheme.Muted);
        readonly Button apply=new EditorButton{Text="Apply freeze areas",Primary=true},reload=new EditorButton{Text="Reload fields"};
        readonly Func<FreezeZoneSettings,Task<FoundationReply>> send;
        readonly EditorToggle enabled=new EditorToggle{Compact=true},manualOverrides=new EditorToggle{Compact=true};
        readonly NumericUpDown warningRadius=new NumericUpDown{Minimum=0,Maximum=5000,DecimalPlaces=1,Width=100};
        readonly Label boundarySummary=EditorTheme.Label("",9,EditorTheme.Muted);
        Control automaticOptions,customOptions;
        FreezeZoneSettings current; ForcesView forces;string baseline="",world="";bool loading,dirty,conflict;
        public event Action DraftChanged;public bool HasDraft{get{return dirty;}}
        public int ModifiedCount {get{return tabs.ModifiedCount;}}
        public void Embed(){tabs.Embedded=true;}
        public void RestoreDefaults(){tabs.ResetSelected();}
        int SelectedMode {get{return !enabled.Checked?0:((ComboBox)controls["Mode"]).SelectedIndex==1?2:manualOverrides.Checked?3:1;}}
        public string DraftError{get{return conflict?"Freeze areas changed in Workbench. Reload fields first.":ReadFields().Validate();}}
        public FreezeZonesPage(Func<FreezeZoneSettings,Task<FoundationReply>> action,Action openMap,Action openTiming)
        {
            send=action;Dock=DockStyle.Fill;BackColor=EditorTheme.Background;
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,90));tabs.Dock=DockStyle.Fill;frame.Controls.Add(tabs);
            var p=tabs.AddSection("Freeze areas","Vanilla circles and shared faction boundaries");
            Choice(p,"Mode","Zone creation",new[]{"Automatic circles","Custom zones"},"Automatic: one circle at each group commander's position. Custom: create areas on the map, then assign them to groups.");
            warningRadius.ValueChanged+=(s,e)=>Changed();tabs.Row(p,"WarningRadius","Warning radius from group center (m)","Distance from the same center where the vanilla warning starts. The screen effect grows towards the kill radius.",warningRadius,40m);
            Number(p,"Radius","Kill radius from group center (m)",1,5000,"Distance from the commander to the outer boundary. Crossing it kills the character.");
            EditorTheme.Add(p,boundarySummary);
            enabled.CheckedChanged+=(s,e)=>Changed();tabs.Row(p,"Enabled","Restrict players during freeze","Uses vanilla warning effects and death beyond the allowed territory.",enabled,1);
            manualOverrides.CheckedChanged+=(s,e)=>Changed();tabs.Row(p,"ManualOverrides","Allow custom replacements for individual groups","Assigned manual areas replace those groups' circles. Other groups keep automatic circles.",manualOverrides,0);
            automaticOptions=manualOverrides.Parent;
            Number(p,"WarningBand","Contour warning distance inside boundary (m)",0.1m,5000,"For custom Vector Tool contours. Manually placed vanilla circles use their own warning and kill radii.");customOptions=controls["WarningBand"].Parent;
            EditorTheme.Add(p,EditorTheme.Button("Create / edit custom zones on the map",openMap,true));
            tabs.Note(p,"Crossing the outer boundary kills the character without a return countdown. No punishment during freeze hold or technical pause. No teleportation of players or vehicles. The restriction is horizontal, as in vanilla.");
            tabs.Note(p,"Restrictions and native warning effects end with freeze. Automatic entities are removed; manual source objects remain. A later combat pause does not recreate areas.");
            EditorTheme.Add(p,EditorTheme.Button("Freeze timing, readiness & damage →",openTiming));
            var c=p;
            tabs.Group(c,"Playable groups","Groups supply starting zones; territory is shared across their entire faction. No generated zone removes only that circle, not the player’s faction restrictions.");EditorTheme.Add(c,summary);EditorTheme.Add(c,groupRows);
            tabs.Note(c,"Custom mode only uses assigned zones. Create a Freeze circle or Zone contour on the map, then assign its name below the group. Existing native circles and Vector Tool contours are supported. Allied and enemy factions remain separate.");
            tabs.Group(p,"Automatic area template","Vanilla restriction-zone prefab used for generated circles.");
            var template=new TextBox{Width=340};controls["Template"]=template;template.TextChanged+=(s,e)=>Changed();tabs.Row(p,"Template","Base prefab resource","Uses SCR_EditorRestrictionZoneEntity and its native screen effects.",template,new FreezeZoneSettings().Template);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,12,0,0)};footer.Controls.Add(apply);footer.Controls.Add(reload);message.MaximumSize=new Size(640,0);footer.Controls.Add(message);frame.Controls.Add(footer,0,1);Controls.Add(frame);
            apply.Click+=async(s,e)=>{var d=ReadFields();string error=d.Validate();if(error!=null){message.Text=error;return;}var r=await send(d);if(r.Ok!=1)message.Text=r.Message;};reload.Click+=(s,e)=>LoadFields();
        }
        void Choice(TableLayoutPanel p,string key,string title,string[] choices,string help){var c=new EditorCombo{Width=300};c.Items.AddRange(choices);controls[key]=c;c.SelectedIndexChanged+=(s,e)=>Changed();tabs.Row(p,key,title,help,c,0);}
        void Number(TableLayoutPanel p,string key,string title,decimal min,decimal max,string help=""){var c=new NumericUpDown{Minimum=min,Maximum=max,DecimalPlaces=1,Width=100};controls[key]=c;c.ValueChanged+=(s,e)=>Changed();tabs.Row(p,key,title,help,c,typeof(FreezeZoneSettings).GetProperty(key).GetValue(new FreezeZoneSettings(),null));}
        public void Accept(FoundationReply reply,bool replace)
        {
            bool changed=world!=reply.WorldSession;world=reply.WorldSession;forces=reply.Forces;
            if(!replace&&!changed&&dirty&&reply.FreezeZones!=null&&baseline!=reply.FreezeZones.Fingerprint())conflict=true;
            current=reply.FreezeZones;if(replace||changed||!dirty)LoadFields();else {RebuildGroups();State();}
        }
        void LoadFields()
        {
            loading=true;var d=current??new FreezeZoneSettings();
            enabled.Checked=d.Mode!=0;manualOverrides.Checked=d.Mode==3;
            warningRadius.Value=Math.Max(0,Math.Min(5000,d.Radius-d.WarningBand));
            foreach(var pair in controls){object v=typeof(FreezeZoneSettings).GetProperty(pair.Key).GetValue(d,null);var n=pair.Value as NumericUpDown;var t=pair.Value as TextBox;if(n!=null)n.Value=Math.Max(n.Minimum,Math.Min(n.Maximum,Convert.ToDecimal(v)));else if(t!=null)t.Text=Convert.ToString(v);else ((ComboBox)pair.Value).SelectedIndex=d.Mode==2?1:0;}
            coverage.Clear();radii.Clear();foreach(var row in d.Coverage){coverage[row.Group]=row.Zone;radii[row.Group]=row.Radius;}baseline=d.Fingerprint();dirty=conflict=false;RebuildGroups();loading=false;State();if(DraftChanged!=null)DraftChanged();
        }
        void RebuildGroups()
        {
            bool was=loading;loading=true;groupRows.SuspendLayout();foreach(Control child in groupRows.Controls.Cast<Control>().ToArray())child.Dispose();groupRows.Controls.Clear();groupRows.RowStyles.Clear();groupRows.RowCount=0;
            var seen=new HashSet<string>();int count=0;
            if(forces!=null)foreach(var group in forces.Groups.Where(g=>g.Playable!=0))
            {
                string id=!String.IsNullOrEmpty(group.Id)?group.Id:group.Name!="(unnamed group)"?group.Name:"@"+group.Key;
                string existing=coverage.ContainsKey(id)?id:coverage.ContainsKey(group.Name)?group.Name:coverage.ContainsKey("@"+group.Key)?"@"+group.Key:id;
                seen.Add(existing);string selected=coverage.ContainsKey(existing)?coverage[existing]:"";
                var keys=new List<string>{"","-"};var combo=new EditorCombo{Width=300};combo.Items.AddRange(new object[]{"Automatic / default","No generated zone"});
                foreach(var zone in (current??new FreezeZoneSettings()).Zones){keys.Add(zone.Key);combo.Items.Add("Custom · "+zone.Label);}
                if(!keys.Contains(selected)){keys.Add(selected);combo.Items.Add("Missing · "+selected);}combo.SelectedIndex=keys.IndexOf(selected);
                var row=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(10),BackColor=EditorTheme.Panel};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,300));
                row.Controls.Add(EditorTheme.Label(group.Callsign+"\n"+group.EngineKey+" · "+group.Name,10));var options=EditorTheme.Stack();EditorTheme.Add(options,combo);
                var radius=new NumericUpDown{Minimum=1,Maximum=5000,DecimalPlaces=1,Width=100,Value=radii.ContainsKey(existing)&&radii[existing]>0?radii[existing]:Math.Max(1,((NumericUpDown)controls["Radius"]).Value)};
                var inherit=new EditorToggle{Text="Use mission radius",Checked=!radii.ContainsKey(existing)||radii[existing]==0,AutoSize=true};radius.Enabled=!inherit.Checked;
                EditorTheme.Add(options,inherit);EditorTheme.Add(options,EditorTheme.Field("Group radius (m)",radius));row.Controls.Add(options);EditorTheme.Add(groupRows,row);
                inherit.CheckedChanged+=(s,e)=>{radius.Enabled=!inherit.Checked;if(loading)return;radii[existing]=inherit.Checked?0:radius.Value;if(!coverage.ContainsKey(existing))coverage[existing]="";Changed();};
                radius.ValueChanged+=(s,e)=>{if(loading||inherit.Checked)return;radii[existing]=radius.Value;if(!coverage.ContainsKey(existing))coverage[existing]="";Changed();};
                combo.SelectedIndexChanged+=(s,e)=>{if(loading)return;string value=keys[combo.SelectedIndex];coverage.Remove(existing);if(!String.IsNullOrEmpty(value)||(radii.ContainsKey(existing)&&radii[existing]>0))coverage[existing]=value;if(value!=""&&value!="-"&&SelectedMode==1)manualOverrides.Checked=true;Changed();};count++;
            }
            foreach(var missing in coverage.Keys.Where(k=>!seen.Contains(k)).ToArray())
            {string key=missing;var row=EditorTheme.Stack(10);EditorTheme.Add(row,EditorTheme.Label("Missing group: "+key,10,EditorTheme.Amber));EditorTheme.Add(row,EditorTheme.Button("Remove assignment",()=>{coverage.Remove(key);RebuildGroups();Changed();}));EditorTheme.Add(groupRows,row);}
            summary.Text=count+" playable groups · "+(current==null?0:current.Zones.Count)+" reusable zones";groupRows.ResumeLayout(true);EditorTheme.Apply(groupRows);loading=was;
        }
        public FreezeZoneSettings ReadFields()
        {
            var d=(current??new FreezeZoneSettings()).Clone();foreach(var pair in controls){object value;var n=pair.Value as NumericUpDown;var t=pair.Value as TextBox;if(n!=null)value=n.Value;else if(t!=null)value=t.Text;else value=SelectedMode;typeof(FreezeZoneSettings).GetProperty(pair.Key).SetValue(d,value,null);}if(d.Mode==1||d.Mode==3)d.WarningBand=d.Radius-warningRadius.Value;d.Coverage=coverage.Select(p=>new FreezeCoverage{Group=p.Key,Zone=p.Value,Radius=radii.ContainsKey(p.Key)?radii[p.Key]:0}).ToList();return d;
        }
        void Changed(){if(loading)return;bool auto=((ComboBox)controls["Mode"]).SelectedIndex==0;loading=true;if(auto){decimal band=((NumericUpDown)controls["Radius"]).Value-warningRadius.Value;if(band>=0.1m)((NumericUpDown)controls["WarningBand"]).Value=band;}else warningRadius.Value=Math.Max(0,((NumericUpDown)controls["Radius"]).Value-((NumericUpDown)controls["WarningBand"]).Value);loading=false;dirty=ReadFields().Fingerprint()!=baseline;State();if(DraftChanged!=null)DraftChanged();}
        void State()
        {
            int mode=SelectedMode;bool auto=mode==1||mode==3;
            controls["Template"].Enabled=controls["Radius"].Enabled=auto;controls["WarningBand"].Enabled=mode>0;
            controls["Mode"].Enabled=enabled.Checked;warningRadius.Enabled=auto;automaticOptions.Visible=auto;customOptions.Visible=mode==2;
            controls["Radius"].Parent.Visible=warningRadius.Parent.Visible=auto;controls["Template"].Parent.Visible=auto;
            boundarySummary.Text=auto?"From the group center: warning at "+warningRadius.Value.ToString("0.#")+" m · death beyond "+((NumericUpDown)controls["Radius"]).Value.ToString("0.#")+" m":"Custom circles keep their own radii. Assign created areas to groups below.";
            groupRows.Enabled=mode>0;tabs.UpdateValues(mode==0?"No spatial freeze restriction":mode==2?"Custom areas · group assignments":"Automatic areas · fixed group starts");
            bool available=current!=null&&current.Version==3;tabs.Enabled=available;apply.Enabled=available&&dirty&&!conflict&&String.IsNullOrEmpty(current.Problem);reload.Enabled=available&&dirty;
            message.Text=!available?"Refresh after reopening Workbench.":!String.IsNullOrEmpty(current.Problem)?current.Problem:conflict?"World changed. Draft kept; Reload fields before Apply.":dirty?"Unapplied changes. Apply, then Save mission.":"Native Undo before Save. Contours are edited in the connected map.";
        }
        public void Applied(){dirty=conflict=false;}
        public bool CanLeave(){return !dirty||MessageBox.Show(this,"Discard unapplied freeze areas?","Unapplied settings",MessageBoxButtons.OKCancel)==DialogResult.OK;}
    }
}

