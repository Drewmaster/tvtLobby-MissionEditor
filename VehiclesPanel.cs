using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class VehicleAssignmentEdit
    {
        public string VehicleKey {get;set;} public string GroupKey {get;set;} public int Assigned {get;set;}
    }
    public sealed class EditorVehicle
    {
        public string Key {get;set;} public string Name {get;set;} public string Title {get;set;}
        public string Layer {get;set;} public string EngineKey {get;set;} public string ParentGroup {get;set;} public string Problem {get;set;}
        public int Editable {get;set;}
        public List<string> Groups {get;set;} public List<string> ExplicitGroups {get;set;}
        public EditorVehicle(){Groups=new List<string>();ExplicitGroups=new List<string>();}
        public bool IsChild {get{return !String.IsNullOrEmpty(ParentGroup);}}
        public string Binding {get{return IsChild?"Child · automatic":Editable==0?"Inherited / locked":Groups.Count==0?"Unassigned":Groups.Count>1?"Shared · "+Groups.Count+" groups":"Explicit";}}
    }
    public sealed class VehiclesPanel : UserControl
    {
        public const string UpdateRequired="Vehicle assignments require updated Workbench scripts. Save your world, restart Workbench, then reopen Mission Editor from Plugins.";
        readonly Func<string,ForcesEdit,Task<FoundationReply>> send;
        readonly TabControl tabs=new EditorTabs();
        readonly DataGridView available=new DataGridView(),assigned=new DataGridView();
        readonly Button assign,remove,select,apply,discard;
        readonly Label heading=new EditorLabel(),details=new EditorLabel(),draftStatus=new EditorLabel();
        readonly TextBox search=new TextBox();
        readonly EditorCombo groupPicker=new EditorCombo();
        readonly Func<string,bool> chooseGroup;
        readonly bool sharedApply;
        ForcesView data=new ForcesView(),display=new ForcesView();string groupKey="",factionKey="",fingerprint="",baseline="";bool blocked,filling,conflict;
        readonly List<VehicleAssignmentEdit> changes=new List<VehicleAssignmentEdit>();
        public event Action DraftChanged;
        public bool HasDraft {get{return changes.Count>0;}}
        public ForcesView Preview {get{return display;}}
        public int PendingCount {get{return changes.Count;}}
        public string DraftError {get{return !HasDraft?null:conflict?"Vehicle assignments changed in Workbench. Discard the assignment draft and review the current scene.":data.VehicleAssignmentsVersion<2?UpdateRequired:null;}}

        public VehiclesPanel(Func<string,ForcesEdit,Task<FoundationReply>> callback,Func<string,bool> groupSelection=null,bool sharedApply=false)
        {
            chooseGroup=groupSelection;this.sharedApply=sharedApply;
            send=callback;Height=380;Dock=DockStyle.Top;BackColor=Color.FromArgb(22,28,31);ForeColor=Color.Gainsboro;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,66));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));Controls.Add(layout);
            var destination=EditorTheme.Stack();heading.Text="Assign vehicles to";heading.AutoSize=true;heading.Font=EditorTheme.Font(11,FontStyle.Bold);EditorTheme.Add(destination,heading);EditorTheme.Add(destination,groupPicker);layout.Controls.Add(destination,0,0);
            groupPicker.SelectedIndexChanged+=(s,e)=>{if(filling)return;var group=groupPicker.SelectedItem as GroupOption;if(group==null||group.Key==groupKey)return;string old=groupKey;if(chooseGroup!=null&&!chooseGroup(group.Key)){filling=true;groupPicker.SelectedIndex=groupPicker.Items.Cast<GroupOption>().ToList().FindIndex(g=>g.Key==old);filling=false;}};
            var searchRow=new FlowLayoutPanel{Dock=DockStyle.Fill};searchRow.Controls.Add(new EditorLabel{Text="Search vehicles",AutoSize=true,Margin=new Padding(0,4,8,0)});search.Width=300;search.BackColor=Color.FromArgb(30,38,42);search.ForeColor=Color.Gainsboro;searchRow.Controls.Add(search);layout.Controls.Add(searchRow,0,1);
            search.TextChanged+=(s,e)=>{fingerprint="";Accept(data,groupKey,blocked,factionKey);};
            tabs.Dock=DockStyle.Fill;layout.Controls.Add(tabs,0,2);CreateTab("Available vehicles",available);CreateTab("Assigned to group",assigned);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};layout.Controls.Add(actions,0,3);
            assign=Button("Assign to group",()=>StageSelected(true));remove=Button("Remove assignment",()=>StageSelected(false));select=Button("Select in world",async()=>await Send("vehicle_select"));
            actions.Controls.AddRange(new Control[]{assign,remove,select});details.Dock=DockStyle.Fill;details.UseMnemonic=false;details.ForeColor=Color.FromArgb(158,178,184);layout.Controls.Add(details,0,4);
            apply=Button("Apply assignments",async()=>await ApplyPending());discard=Button("Discard changes",Applied);if(!sharedApply)actions.Controls.AddRange(new Control[]{apply,discard});
            draftStatus.Dock=DockStyle.Fill;draftStatus.ForeColor=EditorTheme.Amber;layout.Controls.Add(draftStatus,0,5);
            if(sharedApply)
            {
                // The common footer replaces this panel's Apply/status rows.
                // Keep the vehicle list usable when the editor window is smaller.
                layout.Controls.Remove(destination);layout.Controls.Remove(searchRow);
                searchRow.Controls.Remove(search);searchRow.Dispose();
                var filters=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
                filters.Controls.Add(destination,0,0);search.Dock=DockStyle.Top;filters.Controls.Add(EditorTheme.Field("Search vehicles",search),1,0);
                layout.Controls.Add(filters,0,0);layout.RowStyles[0].Height=62;layout.RowStyles[1].Height=0;layout.RowStyles[4].Height=44;layout.RowStyles[5].Height=0;draftStatus.Visible=false;
                Controls.Remove(layout);var viewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true};layout.Dock=DockStyle.Top;layout.Height=340;
                viewport.Controls.Add(layout);Controls.Add(viewport);viewport.SizeChanged+=(s,e)=>layout.Height=Math.Max(340,viewport.ClientSize.Height);
            }
            EditorTheme.Apply(this);tabs.SelectedIndex=0;tabs.SelectedIndexChanged+=(s,e)=>UpdateActions();UpdateActions();
        }
        sealed class GroupOption{public string Key,Label;public override string ToString(){return Label;}}
        static Button Button(string text,Action action)
        {
            var result=new EditorButton{Text=text,Primary=text.StartsWith("Apply")};result.Click+=(s,e)=>action();return result;
        }
        void CreateTab(string text,DataGridView grid)
        {
            var tab=new TabPage(text){BackColor=BackColor};tabs.TabPages.Add(tab);tab.Controls.Add(grid);
            grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersVisible=false;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.AutoGenerateColumns=false;
            grid.BackgroundColor=BackColor;grid.BorderStyle=BorderStyle.None;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(30,38,42);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.Gainsboro;grid.DefaultCellStyle.BackColor=BackColor;grid.DefaultCellStyle.ForeColor=Color.Gainsboro;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(92,76,47);grid.RowTemplate.Height=28;grid.ColumnHeadersHeight=30;
            foreach(var column in new[]{Tuple.Create("Name","World vehicle",25f),Tuple.Create("Title","Vehicle type",25f),Tuple.Create("Layer","Layer",20f),Tuple.Create("Binding","Assignment",22f)})
                grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=column.Item1,HeaderText=column.Item2,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=column.Item3});
            grid.BindingContext=new BindingContext();grid.SelectionChanged+=(s,e)=>{if(!filling)UpdateActions();};
        }
        public static List<EditorVehicle> AvailableFor(ForcesView view,EditorGroup group)
        {
            if(group==null)return new List<EditorVehicle>();
            return view.Vehicles.Where(v=>!v.IsChild&&v.EngineKey==group.EngineKey&&!v.Groups.Contains(group.Key)).OrderBy(v=>v.Name,StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static List<EditorVehicle> AssignedTo(ForcesView view,string group)
        {
            return view.Vehicles.Where(v=>v.Groups.Contains(group)).OrderBy(v=>v.Name,StringComparer.OrdinalIgnoreCase).ToList();
        }
        public void Accept(ForcesView view,string key,bool hasDraft,string faction="")
        {
            if(HasDraft&&AssignmentStamp(view)!=baseline)conflict=true;
            data=view;groupKey=key;blocked=hasDraft;factionKey=faction;display=Projection();
            string next=key+"|"+faction+"|"+display.Fingerprint();
            if(next!=fingerprint)
            {
                var group=data.Groups.FirstOrDefault(g=>g.Key==key);filling=true;
                groupPicker.Items.Clear();foreach(var candidate in data.OrderedGroups().Where(g=>factionKey==""||g.EngineKey==factionKey))groupPicker.Items.Add(new GroupOption{Key=candidate.Key,Label=candidate.Name+" · "+(candidate.Assigned!=0?candidate.Callsign:candidate.EngineKey)});
                groupPicker.SelectedIndex=groupPicker.Items.Cast<GroupOption>().ToList().FindIndex(g=>g.Key==key);
                bool supported=data.VehicleAssignmentsVersion>=1;
                Bind(available,supported?AvailableFor(display,group).Where(MatchesSearch).ToList():new List<EditorVehicle>());Bind(assigned,supported?AssignedTo(display,key).Where(MatchesSearch).ToList():new List<EditorVehicle>());
                tabs.TabPages[0].Text=supported?"Available vehicles ("+available.Rows.Count+")":"Available vehicles — update required";tabs.TabPages[1].Text=supported?"Assigned to group ("+assigned.Rows.Count+")":"Assigned to group — update required";
                heading.Text=!supported?"Vehicles — Workbench scripts need updating":"Assign vehicles to this group";
                groupPicker.Enabled=chooseGroup!=null&&!blocked;
                search.Enabled=tabs.Enabled=supported;
                filling=false;fingerprint=next;
            }
            UpdateActions();
        }
        bool MatchesSearch(EditorVehicle vehicle)
        {
            return ((vehicle.Name??"")+" "+(vehicle.Title??"")+" "+(vehicle.Layer??"")).IndexOf(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)>=0;
        }
        static void Bind(DataGridView grid,List<EditorVehicle> rows)
        {
            var prior=grid.SelectedRows.Count==0?null:grid.SelectedRows[0].DataBoundItem as EditorVehicle;
            grid.DataSource=rows;grid.ClearSelection();
            var next=rows.FirstOrDefault(v=>prior!=null&&v.Key==prior.Key)??rows.FirstOrDefault();
            if(next!=null)foreach(DataGridViewRow row in grid.Rows)if(row.DataBoundItem==next){grid.CurrentCell=row.Cells[0];row.Selected=true;break;}
        }
        EditorVehicle Selected()
        {
            var grid=tabs.SelectedIndex==0?available:assigned;return grid.SelectedRows.Count==0?null:grid.SelectedRows[0].DataBoundItem as EditorVehicle;
        }
        void UpdateActions()
        {
            var vehicle=Selected();var group=data.Groups.FirstOrDefault(g=>g.Key==groupKey);
            bool supported=data.VehicleAssignmentsVersion>=1;
            bool editable=data.VehicleAssignmentsVersion>=2&&!blocked&&!conflict&&vehicle!=null&&vehicle.Editable!=0&&!vehicle.IsChild&&group!=null;
            if(assign==null)return;groupPicker.Enabled=chooseGroup!=null&&!blocked;
            assign.Text=group==null?"Choose destination group":"Assign to "+group.Name;
            assign.Enabled=editable&&group.Playable!=0&&vehicle.EngineKey==group.EngineKey&&!vehicle.Groups.Contains(groupKey);
            remove.Enabled=editable&&vehicle.Groups.Contains(groupKey);select.Enabled=supported&&vehicle!=null&&!blocked;
            apply.Enabled=HasDraft&&!blocked&&DraftError==null;discard.Enabled=HasDraft;
            apply.Text=HasDraft?"Apply assignments ("+changes.Count+")":"Apply assignments";
            draftStatus.Text=data.VehicleAssignmentsVersion<2?UpdateRequired:DraftError??(sharedApply?"Assignments and group order share the Apply button below.":HasDraft?changes.Count+" pending changes across groups. Apply assignments or Apply & save mission to send them together.":"Assign and Remove only change this draft. Apply once when all assignments are ready.");
            details.Text=!supported?UpdateRequired:blocked?"Apply or reload the group fields before changing assignments.":vehicle==null?"Place vehicles in the faction's Vehicles_<key> layer, then Refresh. Child vehicles are assigned automatically.":
                "Groups: "+(vehicle.Groups.Count==0?"none":String.Join(", ",vehicle.Groups.Select(k=>{var g=data.Groups.FirstOrDefault(a=>a.Key==k);return g==null?k:g.Name+" · "+g.Callsign;})))+
                "\n"+(vehicle.IsChild?"Child of a group — change the hierarchy in World Editor to reassign.":"A vehicle may be shared by several groups. Removing one assignment keeps the others.")+
                (String.IsNullOrEmpty(vehicle.Problem)?"":"\n"+vehicle.Problem);
        }
        string AssignmentStamp(ForcesView view)
        {
            var parts=new List<string>();
            foreach(string key in changes.Select(c=>c.VehicleKey).Distinct().OrderBy(k=>k,StringComparer.Ordinal))
            {
                var v=view.Vehicles.FirstOrDefault(a=>a.Key==key);
                parts.Add(v==null?key+":missing":EditorJson.Serialize(new {v.Key,v.EngineKey,v.ParentGroup,v.Editable,Groups=v.Groups.OrderBy(k=>k,StringComparer.Ordinal).ToArray(),Explicit=v.ExplicitGroups.OrderBy(k=>k,StringComparer.Ordinal).ToArray()}));
            }
            foreach(string key in changes.Select(c=>c.GroupKey).Distinct().OrderBy(k=>k,StringComparer.Ordinal))
            {
                var g=view.Groups.FirstOrDefault(a=>a.Key==key);
                parts.Add(g==null?key+":missing":EditorJson.Serialize(new {g.Key,g.EngineKey,g.Playable,g.Editable}));
            }
            return String.Join("\n",parts);
        }
        ForcesView Projection()
        {
            var projected=new ForcesView{Groups=data.Groups,Order=data.Order,VehicleAssignmentsVersion=data.VehicleAssignmentsVersion};
            foreach(var v in data.Vehicles)
            {
                var row=new EditorVehicle{Key=v.Key,Name=v.Name,Title=v.Title,Layer=v.Layer,EngineKey=v.EngineKey,ParentGroup=v.ParentGroup,Problem=v.Problem,Editable=v.Editable,Groups=new List<string>(v.Groups),ExplicitGroups=new List<string>(v.ExplicitGroups)};
                foreach(var change in changes.Where(c=>c.VehicleKey==v.Key)){row.Groups.Remove(change.GroupKey);if(change.Assigned!=0)row.Groups.Add(change.GroupKey);}
                projected.Vehicles.Add(row);
            }
            return projected;
        }
        void StageSelected(bool add){var vehicle=Selected();if(vehicle!=null)StageAssignment(vehicle.Key,groupKey,add);}
        public bool CanStageAssignment(string vehicleKey,string destination,bool add)
        {
            var vehicle=data.Vehicles.FirstOrDefault(v=>v.Key==vehicleKey);var group=data.Groups.FirstOrDefault(g=>g.Key==destination);
            if(blocked||conflict||data.VehicleAssignmentsVersion<2||vehicle==null||vehicle.Editable==0||vehicle.IsChild||group==null)return false;
            if(add&&(group.Playable==0||vehicle.EngineKey!=group.EngineKey))return false;
            return true;
        }
        public bool StageAssignment(string vehicleKey,string destination,bool add)
        {
            if(!CanStageAssignment(vehicleKey,destination,add))return false;
            var vehicle=data.Vehicles.First(v=>v.Key==vehicleKey);
            changes.RemoveAll(c=>c.VehicleKey==vehicleKey&&c.GroupKey==destination);
            if(add!=vehicle.Groups.Contains(destination))changes.Add(new VehicleAssignmentEdit{VehicleKey=vehicleKey,GroupKey=destination,Assigned=add?1:0});
            baseline=AssignmentStamp(data);Accept(data,groupKey,blocked,factionKey);if(DraftChanged!=null)DraftChanged();return true;
        }
        public ForcesEdit ReadChanges(){return new ForcesEdit{VehicleAssignments=changes.Select(c=>new VehicleAssignmentEdit{VehicleKey=c.VehicleKey,GroupKey=c.GroupKey,Assigned=c.Assigned}).ToList()};}
        public async Task<bool> ApplyPending()
        {
            if(!HasDraft)return true;if(blocked||DraftError!=null)return false;
            var result=await send("vehicle_batch",ReadChanges());if(result.Ok!=1)return false;Applied();return true;
        }
        public void Applied(){changes.Clear();conflict=false;baseline="";fingerprint="";Accept(data,groupKey,blocked,factionKey);if(DraftChanged!=null)DraftChanged();}
        public bool CanLeave()
        {
            if(!HasDraft)return true;
            if(MessageBox.Show(this,"Discard all unapplied vehicle assignments?","Unapplied vehicle assignments",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return false;
            Applied();return true;
        }
        async Task Send(string operation)
        {
            var vehicle=Selected();if(vehicle==null||blocked||data.VehicleAssignmentsVersion<1)return;
            await send(operation,new ForcesEdit{Key=groupKey,VehicleKey=vehicle.Key});
        }
    }
}
