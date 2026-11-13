using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class EditorFaction
    {
        public string Key {get;set;} public string Id {get;set;} public string Side {get;set;}
        public string DefaultName {get;set;} public string Name {get;set;} public string Alliance {get;set;}
        public int Groups {get;set;} public int Slots {get;set;} public int Layers {get;set;}
        public List<string> Companies {get;set;} public List<string> Platoons {get;set;} public List<string> Squads {get;set;}
        public string Format {get;set;}
        public string TemplateResource {get;set;} public string Flag {get;set;}
        public int Red {get;set;} public int Green {get;set;} public int Blue {get;set;}
        public string DisplayName {get{return String.IsNullOrEmpty(Name)?DefaultName:Name;}}
        public override string ToString(){return DefaultName+" ["+Key+"]";}
    }
    public sealed class EditorGroup
    {
        public string Key {get;set;} public string Name {get;set;} public string Id {get;set;} public string EngineKey {get;set;} public string Callsign {get;set;}
        public int Playable {get;set;} public int Slots {get;set;} public int Assigned {get;set;} public int Company {get;set;} public int Platoon {get;set;} public int Squad {get;set;} public int Editable {get;set;}
        public string PlayableLabel {get{return Playable!=0?"Yes":"No";}}
    }
    public sealed class ForcesView
    {
        public int VehicleAssignmentsVersion {get;set;}
        public int NativeFactionCreationVersion {get;set;}
        public string FactionDirectory {get;set;}
        public List<EditorFaction> Factions {get;set;} public List<EditorFaction> Available {get;set;}
        public List<EditorGroup> Groups {get;set;} public List<string> Alliances {get;set;} public List<string> Order {get;set;}
        public List<EditorVehicle> Vehicles {get;set;}
        public ForcesView(){Factions=new List<EditorFaction>();Available=new List<EditorFaction>();Groups=new List<EditorGroup>();Vehicles=new List<EditorVehicle>();Alliances=new List<string>();Order=new List<string>();}
        public string Fingerprint(){return EditorJson.Serialize(this);}
        public List<EditorGroup> OrderedGroups()
        {
            return Groups.OrderBy(g=>{int i=Order.IndexOf(g.Id??"");return i<0?Int32.MaxValue:i;}).ThenBy(g=>g.Id??g.Key,StringComparer.Ordinal).ToList();
        }
    }
    public sealed class ForcesEdit
    {
        public string Key {get;set;} public string Name {get;set;} public string Alliance {get;set;}
        public string VehicleKey {get;set;}
        public List<VehicleAssignmentEdit> VehicleAssignments {get;set;}
        public string BaseKey {get;set;} public string Flag {get;set;}
        public int Red {get;set;} public int Green {get;set;} public int Blue {get;set;}
        public int DefaultName {get;set;} public int Assigned {get;set;} public int Company {get;set;} public int Platoon {get;set;} public int Squad {get;set;} public int Playable {get;set;} public int Delta {get;set;}
        public ForcesEdit(){DefaultName=1;Playable=1;Name="";Alliance="";VehicleAssignments=new List<VehicleAssignmentEdit>();}
    }
    public sealed partial class ForcesPage : UserControl
    {
        static readonly Color Dark=Color.FromArgb(22,28,31),PanelColor=Color.FromArgb(30,38,42),Gold=Color.FromArgb(224,175,86),Muted=Color.FromArgb(158,178,184);
        readonly bool groupMode;
        readonly Func<string,ForcesEdit,Task<FoundationReply>> send;
        readonly DataGridView grid=new DataGridView();
        readonly ComboBox available=new EditorCombo(),alliance=new EditorCombo(),filter=new EditorCombo();
        readonly ComboBox company=new EditorCombo(),platoon=new EditorCombo(),squad=new EditorCombo();
        readonly TextBox displayName=new TextBox();
        readonly CheckBox defaultName=new EditorToggle(),assigned=new EditorToggle(),playable=new EditorToggle();
        readonly Label info=new EditorLabel(),preview=new EditorLabel(),notice=new EditorLabel();
        readonly FlowLayoutPanel actions=new FlowLayoutPanel();
        readonly Button apply,remove,up,down,select;
        readonly VehiclesPanel vehicles;
        ForcesView data=new ForcesView();string session="",baseline="",selected="";int priorFilter;bool filling,dirty,conflict;
        public bool HasDraft {get{return dirty||(vehicles!=null&&vehicles.HasDraft);}}
        public string DraftError{get{return dirty?(conflict?"Group changed in Workbench. Reload its fields first.":String.IsNullOrEmpty(selected)?"Select a group.":null):vehicles==null?null:vehicles.DraftError;}}
        public async Task<bool> ApplyPending()
        {
            if(dirty){if(conflict||String.IsNullOrEmpty(selected))return false;var result=await send("group_update",ReadFields());if(result.Ok!=1)return false;}
            return vehicles==null||await vehicles.ApplyPending();
        }
        public void Applied(string operation){if(operation=="vehicle_batch"&&vehicles!=null)vehicles.Applied();else if(operation=="group_update")DiscardFields();}
        public ForcesEdit ReadFields(){return new ForcesEdit{Key=selected,Name=displayName.Text,DefaultName=defaultName.Checked?1:0,Alliance=alliance.Text.Trim(),Playable=playable.Checked?1:0,Assigned=assigned.Checked?1:0,Company=company.SelectedIndex,Platoon=platoon.SelectedIndex,Squad=squad.SelectedIndex};}
        public event Action DraftChanged;
        public ForcesPage(bool groups,Func<string,ForcesEdit,Task<FoundationReply>> callback)
        {
            groupMode=groups;send=callback;Dock=DockStyle.Fill;BackColor=Dark;ForeColor=Color.Gainsboro;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,AutoScroll=true,Padding=new Padding(0,5,12,12)};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(layout);
            Add(layout,TextLabel(groups?"Native callsigns and lobby order":"Mission factions and alliances",14));
            Add(layout,TextLabel(groups?"Automatic callsigns follow lobby order at launch. Drag groups within their faction to reorder them. Explicit callsigns stay assigned to their groups.":"Choose from factions registered in this world's FactionManager, including mod factions. Adding a faction creates Groups_<key> and Vehicles_<key> layers.",10));
            var top=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};Add(layout,top);
            if(groups)
            {
                StyleCombo(filter);filter.Width=400;top.Controls.Add(filter);filter.SelectedIndexChanged+=(s,e)=>{if(!filling){if(CanLeaveFields()){priorFilter=filter.SelectedIndex;FillGrid();}else {filling=true;filter.SelectedIndex=priorFilter;filling=false;}}};
            }
            else
            {
                StyleCombo(available);available.Width=360;top.Controls.Add(available);top.Controls.Add(Button("Add faction",async()=>{var f=available.SelectedItem as EditorFaction;if(f!=null&&CanLeave())await send("faction_add",new ForcesEdit{Key=f.Key});}));
                top.Controls.Add(Button("Create faction…",()=>{if(!CanLeave())return;if(data.NativeFactionCreationVersion<1){MessageBox.Show(this,"Save and restart Workbench, then reopen Mission Editor to load native faction creation.","Workbench scripts need updating");return;}using(var dialog=new NativeFactionDialog(data,send))dialog.ShowDialog(this);}));
            }
            grid.Dock=DockStyle.Top;grid.Height=260;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersVisible=false;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.AutoGenerateColumns=false;grid.BackgroundColor=PanelColor;grid.BorderStyle=BorderStyle.None;grid.GridColor=Dark;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=PanelColor;grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.Gainsboro;grid.DefaultCellStyle.BackColor=Dark;grid.DefaultCellStyle.ForeColor=Color.Gainsboro;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(92,76,47);grid.RowTemplate.Height=30;grid.ColumnHeadersHeight=32;
            if(groups){Column("Name","World group",26);Column("Callsign","Callsign",32);Column("EngineKey","Faction",15);Column("Slots","Slots",10);Column("PlayableLabel","Playable",10);}
            else{Column("DisplayName","Mission name",30);Column("Key","Engine key",16);Column("Alliance","Alliance",20);Column("Groups","Groups",10);Column("Slots","Slots",10);}
            grid.BindingContext=new BindingContext();Add(layout,grid);grid.SelectionChanged+=(s,e)=>SelectRow();
            info.AutoSize=true;info.ForeColor=Muted;Add(layout,info);
            if(groups)
            {
                playable.Text="Playable group";assigned.Text="Assign a specific native callsign";
                foreach(var check in new[]{playable,assigned}){check.AutoSize=true;check.CheckedChanged+=(s,e)=>Changed();Add(layout,check);}
                var selectors=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};
                foreach(var pair in new[]{Tuple.Create("Company",company),Tuple.Create("Platoon",platoon),Tuple.Create("Squad",squad)})
                {selectors.Controls.Add(TextLabel(pair.Item1));StyleCombo(pair.Item2);pair.Item2.Width=165;pair.Item2.SelectedIndexChanged+=(s,e)=>Changed();selectors.Controls.Add(pair.Item2);}
                Add(layout,selectors);preview.AutoSize=true;preview.ForeColor=Gold;Add(layout,preview);
            }
            else
            {
                defaultName.Text="Use faction default name";defaultName.AutoSize=true;defaultName.CheckedChanged+=(s,e)=>{displayName.Enabled=!defaultName.Checked;Changed();};Add(layout,defaultName);
                displayName.Dock=DockStyle.Top;displayName.BackColor=PanelColor;displayName.ForeColor=Color.White;displayName.MaxLength=128;displayName.TextChanged+=(s,e)=>Changed();Add(layout,displayName);
                Add(layout,TextLabel("Alliance — select or enter an ID; empty = no alliance"));StyleCombo(alliance);alliance.DropDownStyle=ComboBoxStyle.DropDown;alliance.Width=400;alliance.TextChanged+=(s,e)=>Changed();Add(layout,alliance);
                Add(layout,TextLabel("The same alliance makes sides friendly and enables alliance audiences. Unrelated pairs retain their configured/native relations. Engine keys and command-side IDs remain unchanged.",10));
            }
            actions.AutoSize=true;actions.Dock=DockStyle.Top;
            apply=Button(groups?"Apply group":"Apply faction",async()=>await Apply());actions.Controls.Add(apply);
            actions.Controls.Add(Button("Reload fields",()=>{DiscardFields();LoadSelected();}));
            if(groups)
            {
                up=Button("Move up",async()=>await MoveGroup(-1));down=Button("Move down",async()=>await MoveGroup(1));select=Button("Select in world",async()=>{if(!String.IsNullOrEmpty(selected))await send("group_select",new ForcesEdit{Key=selected});});actions.Controls.Add(up);actions.Controls.Add(down);actions.Controls.Add(select);
            }
            else
            {
                remove=Button("Remove faction",async()=>{if(String.IsNullOrEmpty(selected)||!CanLeave())return;if(MessageBox.Show(this,"Remove this faction's mission entry? World objects and layers are retained. Referenced factions cannot be removed.","Remove faction",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)==DialogResult.OK)await send("faction_remove",new ForcesEdit{Key=selected});});actions.Controls.Add(remove);
                actions.Controls.Add(Button("Create missing faction layers",async()=>{if(!String.IsNullOrEmpty(selected)&&CanLeave())await send("faction_layers",new ForcesEdit{Key=selected});}));
            }
            Add(layout,actions);notice.AutoSize=true;notice.ForeColor=Gold;Add(layout,notice);
            if(groups){vehicles=new VehiclesPanel(send,SelectVehicleGroup);vehicles.DraftChanged+=()=>{if(DraftChanged!=null)DraftChanged();};Add(layout,vehicles);}
            else Add(layout,TextLabel("Create faction makes a derived native config and connects it to this mission. Existing faction configs are kept intact. Mission display names and alliances can be edited here afterward.",10));
            if(groups){ArrangeGroupPage(layout,top);EnableGroupDragging();}
            EditorTheme.Apply(this);UpdateActions();
        }
        static Label TextLabel(string text,float size=10){return new EditorLabel{Text=text,UseMnemonic=false,AutoSize=true,MaximumSize=new Size(840,0),Font=new Font("Segoe UI",size),Margin=new Padding(0,4,8,10)};}
        static void Add(TableLayoutPanel parent,Control c){int row=parent.RowCount++;parent.RowStyles.Add(new RowStyle(SizeType.AutoSize));parent.Controls.Add(c,0,row);c.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;c.Margin=new Padding(0,0,0,10);}
        static void StyleCombo(ComboBox c){c.DropDownStyle=ComboBoxStyle.DropDownList;c.BackColor=PanelColor;c.ForeColor=Color.Gainsboro;c.Width=200;}
        static Button Button(string text,Action action){return EditorTheme.Button(text,action,text.StartsWith("Apply"));}
        void Column(string property,string title,int width){grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=property,HeaderText=title,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=width,SortMode=DataGridViewColumnSortMode.NotSortable});}
        public bool CanLeave(){return CanLeaveFields()&&(vehicles==null||vehicles.CanLeave());}
        bool CanLeaveFields(){if(!dirty)return true;if(MessageBox.Show(this,"Discard the unapplied faction/group fields?","Unapplied fields",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return false;DiscardFields();return true;}
        void DiscardFields(){dirty=false;conflict=false;if(DraftChanged!=null)DraftChanged();}
        public void DiscardDraft(){DiscardFields();if(vehicles!=null)vehicles.Applied();}
        public void Accept(FoundationReply reply,bool replace)
        {
            var next=reply.Forces??new ForcesView();bool changed=session!=reply.WorldSession;
            if(changed||replace)DiscardDraft();else if(dirty&&baseline!=next.Fingerprint())conflict=true;
            data=next;session=reply.WorldSession;
            filling=true;
            string prior=available.SelectedItem is EditorFaction?((EditorFaction)available.SelectedItem).Key:"";
            available.Items.Clear();foreach(var candidate in data.Available.Where(f=>!data.Factions.Any(a=>a.Key==f.Key)))available.Items.Add(candidate);if(available.Items.Count>0)available.SelectedIndex=0;for(int i=0;i<available.Items.Count;i++)if(((EditorFaction)available.Items[i]).Key==prior)available.SelectedIndex=i;
            string allianceText=alliance.Text;alliance.Items.Clear();alliance.Items.Add("");foreach(string a in data.Alliances)alliance.Items.Add(a);alliance.Text=allianceText;
            string filterKey=(filter.SelectedItem as FilterItem??new FilterItem()).Key;
            var filters=new List<FilterItem>{new FilterItem{Key="",Text="All factions · "+data.Groups.Where(g=>g.Playable!=0).Sum(g=>g.Slots)+" slots"}};
            foreach(string key in data.Groups.Select(g=>g.EngineKey).Concat(data.Factions.Select(f=>f.Key)).Distinct().OrderBy(k=>k)){var f=data.Factions.FirstOrDefault(a=>a.Key==key)??data.Available.FirstOrDefault(a=>a.Key==key);filters.Add(new FilterItem{Key=key,Text=(f==null?key:f.DisplayName)+" · "+data.Groups.Where(g=>g.EngineKey==key&&g.Playable!=0).Sum(g=>g.Slots)+" slots"});}
            filter.Items.Clear();foreach(var item in filters)filter.Items.Add(item);filter.SelectedIndex=Math.Max(0,filters.FindIndex(f=>f.Key==filterKey));priorFilter=filter.SelectedIndex;filling=false;
            FillGrid();RebuildFactionTabs();UpdateActions();
        }
        sealed class FilterItem{public string Key="",Text="";public override string ToString(){return Text;}}
        void FillGrid()
        {
            filling=true;
            if(groupMode){string key=(filter.SelectedItem as FilterItem??new FilterItem()).Key;grid.DataSource=data.OrderedGroups().Where(g=>(key==""||g.EngineKey==key)&&(groupSearch==null||((g.Name??"")+" "+(g.Callsign??"")).IndexOf(groupSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)>=0)).ToList();}
            else grid.DataSource=data.Factions;
            if(!groupMode)grid.Height=Math.Min(230,Math.Max(125,34+30*grid.Rows.Count));
            grid.ClearSelection();foreach(DataGridViewRow row in grid.Rows)if(RowKey(row)==selected){row.Selected=true;grid.CurrentCell=row.Cells[0];break;}
            if(grid.SelectedRows.Count==0&&grid.Rows.Count>0){grid.Rows[0].Selected=true;grid.CurrentCell=grid.Rows[0].Cells[0];}
            filling=false;if(!dirty)SelectRow();
        }
        string RowKey(DataGridViewRow row){return groupMode?((EditorGroup)row.DataBoundItem).Key:((EditorFaction)row.DataBoundItem).Key;}
        void SelectRow()
        {
            if(filling)return;string next=grid.SelectedRows.Count==0?"":RowKey(grid.SelectedRows[0]);
            if(next!=selected&&!CanLeaveFields()){filling=true;grid.ClearSelection();foreach(DataGridViewRow row in grid.Rows)if(RowKey(row)==selected)row.Selected=true;filling=false;return;}
            selected=next;if(!dirty)LoadSelected();
        }
        void LoadSelected()
        {
            filling=true;
            if(groupMode)
            {
                var row=data.Groups.FirstOrDefault(g=>g.Key==selected);var faction=row==null?null:data.Available.FirstOrDefault(f=>f.Key==row.EngineKey);
                playable.Checked=row!=null&&row.Playable!=0;assigned.Checked=row!=null&&row.Assigned!=0;
                FillNames(company,faction==null?null:faction.Companies,row==null?0:row.Company);FillNames(platoon,faction==null?null:faction.Platoons,row==null?0:row.Platoon);FillNames(squad,faction==null?null:faction.Squads,row==null?0:row.Squad);
                info.Text=row==null?"Place groups in World Editor, then Refresh.":row.Name+" · "+row.Slots+" slots"+(row.Editable==0?" · inherited / locked":"");
            }
            else
            {
                var row=data.Factions.FirstOrDefault(f=>f.Key==selected);defaultName.Checked=row==null||String.IsNullOrEmpty(row.Name);displayName.Text=row==null?"":row.Name;alliance.Text=row==null?"":row.Alliance;
                info.Text=row==null?"No mission factions yet. Add one above.":"Engine: "+row.Key+" · Default: "+row.DefaultName+" · "+(row.Layers!=0?"Faction layers present":"Faction layers missing");
            }
            filling=false;baseline=data.Fingerprint();UpdateActions();
        }
        static void FillNames(ComboBox c,List<string> names,int selected){c.Items.Clear();if(names!=null)for(int i=0;i<names.Count;i++){string name=names[i]??"";c.Items.Add(name.Trim().Length>0&&name.Trim().All(Char.IsDigit)?name:name+" ("+i+")");}if(c.Items.Count>0)c.SelectedIndex=Math.Max(0,Math.Min(c.Items.Count-1,selected));}
        bool SelectVehicleGroup(string key)
        {
            if(!CanLeaveFields())return false;
            string faction=(filter.SelectedItem as FilterItem??new FilterItem()).Key;
            if(!data.Groups.Any(g=>g.Key==key&&(faction==""||g.EngineKey==faction)))return false;
            selected=key;filling=true;groupSearch.Text="";filling=false;
            FillGrid();RebuildFactionTabs();return selected==key;
        }
        void Changed(){if(filling)return;dirty=true;UpdateActions();if(DraftChanged!=null)DraftChanged();}
        void UpdateActions()
        {
            bool has=!String.IsNullOrEmpty(selected),editable=has;
            if(groupMode){var row=data.Groups.FirstOrDefault(g=>g.Key==selected);editable=row!=null&&row.Editable!=0;company.Enabled=platoon.Enabled=squad.Enabled=assigned.Checked&&editable;assigned.Enabled=playable.Enabled=editable;if(up!=null){var order=row==null?new List<EditorGroup>():data.OrderedGroups().Where(g=>g.EngineKey==row.EngineKey&&g.Playable!=0).ToList();int position=order.IndexOf(row);up.Enabled=editable&&!dirty&&position>0;down.Enabled=editable&&!dirty&&position>=0&&position<order.Count-1;select.Enabled=has;}
                var f=row==null?null:data.Available.FirstOrDefault(a=>a.Key==row.EngineKey);preview.Text="Automatic: assigned in lobby order at launch. Explicit callsigns are reserved.";
                if(assigned.Checked&&f!=null&&company.SelectedIndex>=0&&platoon.SelectedIndex>=0&&squad.SelectedIndex>=0)preview.Text=(f.Format??"%1 %2 %3").Replace("%1",f.Companies[company.SelectedIndex]).Replace("%2",f.Platoons[platoon.SelectedIndex]).Replace("%3",f.Squads[squad.SelectedIndex]);}
            else {displayName.Enabled=has&&!defaultName.Checked;defaultName.Enabled=alliance.Enabled=has;if(remove!=null)remove.Enabled=has&&!dirty;}
            if(apply!=null)apply.Enabled=editable&&!conflict;
            if(vehicles!=null)vehicles.Accept(data,selected,dirty||conflict,(filter.SelectedItem as FilterItem??new FilterItem()).Key);
            notice.Text=conflict?"Workbench changed. Your fields are retained; Reload fields before applying.":dirty?"Unapplied fields — Apply or Reload before switching rows.":groupMode&&data.VehicleAssignmentsVersion<1?VehiclesPanel.UpdateRequired:"Changes use native Undo. Save mission writes them to disk.";
        }
        async Task Apply(){if(String.IsNullOrEmpty(selected)||conflict)return;await send(groupMode?"group_update":"faction_update",new ForcesEdit{Key=selected,Name=displayName.Text,DefaultName=defaultName.Checked?1:0,Alliance=alliance.Text.Trim(),Playable=playable.Checked?1:0,Assigned=assigned.Checked?1:0,Company=company.SelectedIndex,Platoon=platoon.SelectedIndex,Squad=squad.SelectedIndex});}
        async Task MoveGroup(int delta)
        {
            if(String.IsNullOrEmpty(selected)||dirty||conflict||groupMoving||delta==0)return;
            groupMoving=true;up.Enabled=down.Enabled=false;
            try{await send("group_move",new ForcesEdit{Key=selected,Delta=delta});}
            finally{groupMoving=false;if(!IsDisposed)UpdateActions();}
        }
    }
}
