using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace TvtLobby.MissionEditor
{
    // Same list + detail cards as v06. Creation is guided; subsequent edits stay inline.
        public sealed class ObjectivesPage : UserControl
    {
        readonly Func<string,ObjectiveEdit,Task<FoundationReply>> send;
        readonly Action openMap;
        readonly Panel detail=new Panel(),listHost=new Panel();
        readonly Label message=EditorTheme.Label("",9,EditorTheme.Muted);
        readonly EditorButton remove=new EditorButton{Text="Remove"};
        readonly EditorButton apply=new EditorButton{Text="Apply objective changes",Primary=true},reload=new EditorButton{Text="Reload fields"},add=new EditorButton{Text="+ Add objective",Primary=true},sets=new EditorButton{Text="Target sets"};
        ObjectivesData data;ObjectiveEdit draft;string baseline="",source="",world="";bool conflict,setsMode,applying;
        public event Action DraftChanged;
        public bool HasDraft{get{return draft!=null&&draft.Serialize()!=baseline;}}
        public string DraftError{get{return conflict?"Objectives changed in Workbench. Reload fields first.":draft==null?"Select an objective.":draft.Validate();}}
        public async Task<bool> ApplyPending(){if(DraftError!=null)return false;await ApplyDraft("objective_apply");return !HasDraft;}
        public ObjectivesPage(Func<string,ObjectiveEdit,Task<FoundationReply>> action,Action map)
        {
            send=action;openMap=map;Dock=DockStyle.Fill;BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;Font=EditorTheme.Font();
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,48));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,82));
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};bar.Controls.AddRange(new Control[]{add,sets,EditorTheme.Button("Open terrain map",openMap)});frame.Controls.Add(bar);
            var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,CellBorderStyle=TableLayoutPanelCellBorderStyle.Single,Margin=Padding.Empty};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,27));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,73));listHost.Dock=detail.Dock=DockStyle.Fill;listHost.Padding=new Padding(12);detail.Padding=new Padding(20,16,4,0);body.Controls.Add(listHost);body.Controls.Add(detail);frame.Controls.Add(body,0,1);
            var footer=EditorTheme.Stack();var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};actions.Controls.AddRange(new Control[]{apply,reload,remove});EditorTheme.Add(footer,actions);EditorTheme.Add(footer,message);frame.Controls.Add(footer,0,2);Controls.Add(frame);
            remove.Click+=async(s,e)=>{if(MessageBox.Show(this,"Remove this definition? Change its dependencies first.","Remove",MessageBoxButtons.OKCancel)==DialogResult.OK)await ApplyDraft("objective_remove");};add.Click+=async(s,e)=>await Create();sets.Click+=(s,e)=>{if(!CanLeave())return;setsMode=!setsMode;draft=null;baseline="";Display();Changed();};reload.Click+=(s,e)=>Select(data==null?null:data.Rows.FirstOrDefault(r=>draft!=null&&r.Key==draft.Key));apply.Click+=async(s,e)=>await ApplyDraft("objective_apply");State();
        }
        public void Accept(FoundationReply reply,bool replace)
        {
            bool changed=world!=reply.WorldSession;world=reply.WorldSession;var incoming=reply.Objectives;if(incoming!=null)incoming.Normalize();
            if(!replace&&!changed&&HasDraft&&incoming!=null&&incoming.Serialize()!=source)conflict=true;
            data=incoming;
            if(replace||changed||!HasDraft){string key=draft==null?"":draft.Key;Select(data==null?null:data.Rows.FirstOrDefault(x=>x.Key==key)??data.Rows.FirstOrDefault(x=>(x.Category=="tvtLobby_TargetSet")==setsMode));}
            else State();
        }
        public ObjectiveEdit ReadFields(){return draft==null?null:draft.Clone();}
        public bool CanLeave(){return !HasDraft||MessageBox.Show(this,"Discard unapplied objective changes?","Unapplied objective",MessageBoxButtons.OKCancel)==DialogResult.OK;}
        public void Applied(){if(draft!=null)baseline=draft.Serialize();conflict=false;}
        void Select(TaskRow row){draft=row==null?null:ObjectiveEdit.From(row);baseline=draft==null?"":draft.Serialize();source=data==null?"":data.Serialize();conflict=false;Display();Changed();}
        void Changed(){State();if(DraftChanged!=null)DraftChanged();}
        void State(){bool ready=data!=null&&data.Version==1&&String.IsNullOrEmpty(data.Problem);add.Enabled=ready&&!applying;sets.Enabled=ready&&!applying;apply.Enabled=ready&&HasDraft&&!conflict&&!applying;reload.Enabled=draft!=null&&!applying;remove.Enabled=ready&&draft!=null&&!String.IsNullOrEmpty(draft.Key)&&!conflict&&!HasDraft&&!applying;message.Text=data==null?"Reopen Mission Editor to load Objectives.":!String.IsNullOrEmpty(data.Problem)?data.Problem:conflict?"Workbench changed. Draft retained. Reload fields before applying.":HasDraft?"Unsaved changes. Apply here, or use Apply & save mission at the top.":"Native components · one Undo per edit · no automatic changes on opening.";}
        void Display()
        {
            foreach(Control c in listHost.Controls.Cast<Control>().ToArray())c.Dispose();foreach(Control c in detail.Controls.Cast<Control>().ToArray())c.Dispose();sets.Text=setsMode?"Back to objectives":"Target sets";add.Text=setsMode?"+ Add target set":"+ Add objective";
            if(data==null)return;var list=new ObjectiveScroll();listHost.Controls.Add(list);
            bool newDraft=draft!=null&&String.IsNullOrEmpty(draft.Key);
            if(newDraft){var pending=new EditorButton{Text=draft.Definition.Text("Name"),Detail=ObjectivesData.Title(draft.Definition.TypeName)+" · Not applied yet",Navigation=true,Selected=true,Height=80};EditorTheme.Add(list.Body,pending);}
            foreach(var row in data.Rows.Where(x=>(x.Category=="tvtLobby_TargetSet")==setsMode))
            {var selected=row;var b=new EditorButton{Text=row.Definition.Text("Name"),Detail=ObjectivesData.Title(row.Category)+"\n"+(row.Active==0?"Category disabled":row.Definition.Text("Enabled")=="0"?"Disabled":row.EndRule>=0?"Ends mission":"Progress objective"),Navigation=true,Selected=draft!=null&&draft.Key==row.Key,Height=95};b.Click+=(s,e)=>{if(CanLeave())Select(selected);};EditorTheme.Add(list.Body,b);}
            if(!newDraft&&!data.Rows.Any(x=>(x.Category=="tvtLobby_TargetSet")==setsMode))EditorTheme.Add(list.Body,EditorTheme.Label(setsMode?"No reusable selections yet.":"No objectives yet. Add only what this mission needs.",9,EditorTheme.Muted));list.Fit();
            if(draft==null){var empty=new ObjectiveScroll();detail.Controls.Add(empty);EditorTheme.Add(empty.Body,EditorTheme.Label(setsMode?"Reusable target sets":"Build the mission logic",17,null,true));EditorTheme.Add(empty.Body,EditorTheme.Label("Choose a task on the left or add one. The editor connects real subjects, placed objects and zones; map geometry remains editable.",10,EditorTheme.Muted));empty.Fit();return;}
            var form=new ObjectiveForm(data,draft,Changed);detail.Controls.Add(form);
            EditorTheme.Apply(this);
        }
        async Task Create()
        {
            if(!CanLeave())return;var next=new ObjectiveEdit();
            if(setsMode){next.Definition=data.Make("tvtLobby_TargetSet");next.Definition.Set("Name",Unique("Target set"));draft=next;baseline="";conflict=false;Display();Changed();return;}
            using(var wizard=new ObjectiveWizard(data,Unique))
            {if(wizard.ShowDialog(this)!=DialogResult.OK)return;draft=wizard.Edit;baseline="";conflict=false;source=data.Serialize();Display();Changed();await ApplyDraft("objective_apply");}
        }
        string Unique(string prefix){string name=prefix;int n=2;while(data.Rows.Any(x=>x.Definition.Text("Name")==name))name=prefix+" "+n++;return name;}
        async Task ApplyDraft(string operation)
        {
            string error=draft==null?"Select an objective.":draft.Validate();if(error!=null){message.Text=error;return;}applying=true;State();
            try{string name=draft.Definition.Text("Name");var r=await send(operation,draft.Clone());if(IsDisposed)return;if(r.Ok==1){data=r.Objectives;data.Normalize();Select(operation=="objective_remove"?null:data.Rows.FirstOrDefault(x=>x.Definition.Text("Name")==name));}else message.Text=r.Message;}
            finally{string last=message.Text;applying=false;State();if(HasDraft)message.Text=last;}
        }
    }
    public class ObjectiveScroll : UserControl
    {
        public readonly TableLayoutPanel Body=EditorTheme.Stack();readonly Panel scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};bool fitting;
        public ObjectiveScroll(){Font=EditorTheme.Font();Dock=DockStyle.Fill;BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;Body.Dock=DockStyle.None;Body.Anchor=AnchorStyles.Top|AnchorStyles.Left;scroll.Controls.Add(Body);Controls.Add(scroll);scroll.ClientSizeChanged+=(s,e)=>Fit();}
        public void Fit(){if(fitting||IsDisposed)return;fitting=true;try{MissionEndPage.FitColumns(Body,Math.Max(80,scroll.ClientSize.Width-22));}finally{fitting=false;}}
        public int ScrollY{get{return -scroll.AutoScrollPosition.Y;}set{scroll.AutoScrollPosition=new Point(0,value);}}
    }
    public sealed partial class ObjectiveForm : ObjectiveScroll
    {
        readonly ObjectivesData data;readonly ObjectiveEdit edit;readonly Action changed;readonly HashSet<TaskNode> expanded=new HashSet<TaskNode>();readonly int step;bool rendering;int category=1;
        public ObjectiveForm(ObjectivesData source,ObjectiveEdit value,Action callback,int wizardStep=-1){data=source;edit=value;changed=callback;step=wizardStep;Render();}
        public static TableLayoutPanel Card(string title){var p=EditorTheme.Stack(18);p.BackColor=EditorTheme.Background;p.Paint+=(s,e)=>{using(var pen=new Pen(EditorTheme.Line))e.Graphics.DrawRectangle(pen,0,0,p.Width-1,p.Height-1);};p.Margin=new Padding(0,0,0,18);EditorTheme.Add(p,EditorTheme.Label(title,10,EditorTheme.Amber,true));return p;}
        void Touch(bool render=false){if(rendering)return;if(capturePreview!=null)capturePreview();changed();if(render){int y=ScrollY;Render();ScrollY=y;}}
        void Render()
        {
            rendering=true;capturePreview=null;try{foreach(Control c in Body.Controls.Cast<Control>().ToArray())c.Dispose();Body.Controls.Clear();Body.RowStyles.Clear();Body.RowCount=0;
                var node=edit.Definition;if(node==null)return;
                EditorTheme.Add(Body,EditorTheme.Label(ObjectivesData.Title(node.TypeName),17,null,true));
                if(step<0)
                {
                    var tabs=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,WrapContents=true,Margin=new Padding(0,0,0,16)};
                    string[] names={"Setup","Conditions","Activation","Outcome","Notifications"};
                    foreach(int targetIndex in new[]{1,2,3,5,4}){int target=targetIndex;var b=new EditorButton{Text=names[target-1],Selected=category==target,Name="ObjectiveTab_"+target};b.Click+=(s,e)=>{category=target;Render();ScrollY=0;};tabs.Controls.Add(b);}EditorTheme.Add(Body,tabs);
                }
                if(step==1||(step<0&&category==1)){Field(Body,node,node.Field("Name"));if(node.Field("Enabled")!=null)Field(Body,node,node.Field("Enabled"));}
                string[] sections={"","Participants & objects","Completion conditions","When this objective becomes active","Mission outcome & author notes"};
                for(int section=1;section<=4;section++)
                {
                    if((step>=0&&step!=section)||(step<0&&category!=section))continue;var fields=node.Fields.Where(f=>f.Name!="Name"&&f.Name!="Enabled"&&f.Name!="Notifications"&&ObjectivesData.Section(f.Name)==section).ToList();
                    if(fields.Count==0&&section!=4)continue;var card=Card(sections[section]);foreach(var field in fields)if(Show(node,field))Field(card,node,field);
                    if(section==4&&node.TypeName!="tvtLobby_TargetSet")Result(card);EditorTheme.Add(Body,card);
                }
                if(step==3)NotificationSetup(Body,node.Field("Notifications"));
                if(step<0&&category==5)Notifications(Body,node.Field("Notifications"));
                EditorTheme.Apply(this);Fit();
            }finally{rendering=false;}
        }
        bool Show(TaskNode n,TaskField f)
        {
			if(f.Name=="AdditionalFlags")return false; // The primary and additional poles share one selector.
            if(f.Name=="Count"&&n.Field("Amount")!=null)return n.Text("Amount")=="2";
            if(f.Name=="Percentage")return n.Text("Amount")=="3";
            if(f.Name=="CaptureRules")return n.Text("Condition")=="3";
            if(f.Name=="CaptureExpression")return n.Text("Condition")=="4";
            if(f.Name=="Superiority")return n.Text("Condition")=="1"||n.Text("Condition")=="2";
            if(f.Name=="ConfirmationSeconds"||f.Name=="Confirmers")return n.Text("ConfirmDeath")=="1";
            if(f.Name=="ExpectedPrefab")return false;
            return true;
        }
        static string LabelOf(string name){switch(name){case "Event":return "When to send";case "Audience":return "Who receives it";case "Display":return "Where to show it";case "Seconds":return "Display duration (seconds)";case "CaptureSeconds":return "Capture duration (seconds)";case "MinimumPresence":return "Minimum capturing participants";case "InteractionSeconds":return "Raise flag interaction (seconds)";case "HoldSeconds":return "Hold duration (seconds)";case "HoldMode":return "Hold timer mode";case "Name":return "Title / reference";case "Zone":return "Capture zone";case "Condition":return "Capture logic";case "Capturers":return "Capturing subjects";case "Opposition":return "Opposing presence";case "Count":return "Required count";case "ResultSubject":return "Task result for";case "AnyPrerequisite":return "Any prerequisite is enough";case "PauseWhenInterrupted":return "Keep progress while contested";case "Amount":return "Required targets";case "EntityName":return "Placed character, vehicle or item";case "Member":return "Place in group";default:return ObjectivesData.Words(name);}}
        EditorCombo Choose(IEnumerable<TaskChoice> source,string key,Action<string> change,string empty="Choose…")
        {
            var values=source.ToList();var alias=values.FirstOrDefault(x=>x.Alias==key&&!String.IsNullOrEmpty(key));if(alias!=null&&!values.Any(x=>x.Key==key))values.Add(new TaskChoice{Key=key,Label=alias.Label});values.Insert(0,new TaskChoice{Key="",Label=empty});if(!String.IsNullOrEmpty(key)&&!values.Any(x=>x.Key==key))values.Add(new TaskChoice{Key=key,Label=key.StartsWith("@new:")?"Create new zone / flag on Apply":key+" (unresolved)"});
            var combo=new EditorCombo();combo.Items.AddRange(values.ToArray());combo.SelectedIndex=values.FindIndex(x=>x.Key==(key??""));combo.SelectedIndexChanged+=(s,e)=>{var choice=combo.SelectedItem as TaskChoice;if(choice!=null){change(choice.Key);Touch();}};return combo;
        }
        IEnumerable<TaskChoice> Subjects(IEnumerable<string> selected=null,IEnumerable<string> allowed=null){return SubjectOptions.Visible(data.Subjects,selected,allowed).Select(s=>new TaskChoice{Key=s.Key,Label=s.Name});}
        void SubjectField(TableLayoutPanel parent,TaskField field)
        {
            if(field.Kind=="objects")
            {
                var p=EditorTheme.Stack();EditorTheme.Add(p,EditorTheme.Label(LabelOf(field.Name)));var grid=new TableLayoutPanel{ColumnCount=2,AutoSize=true,Dock=DockStyle.Top};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));EditorTheme.Add(p,grid);foreach(var choice in Subjects(field.Nodes.Select(ObjectivesData.SubjectKey)))
                {string key=choice.Key;var check=new ObjectiveCheck{Text=choice.Label,AutoSize=false,Height=30,Checked=field.Nodes.Any(n=>SubjectOptions.Same(data.Subjects,ObjectivesData.SubjectKey(n),key))};check.CheckedChanged+=(s,e)=>{field.Nodes.RemoveAll(n=>SubjectOptions.Same(data.Subjects,ObjectivesData.SubjectKey(n),key));if(check.Checked)field.Nodes.Add(data.Subject(key));Touch();};grid.Controls.Add(check);}foreach(var n in field.Nodes.Where(n=>!data.Subjects.Any(x=>x.Key==ObjectivesData.SubjectKey(n))))EditorTheme.Add(p,EditorTheme.Label("Unresolved subject: "+ObjectivesData.SubjectKey(n),9,EditorTheme.Amber));p.Margin=new Padding(0,0,0,16);EditorTheme.Add(parent,p);
            }
            else EditorTheme.Add(parent,EditorTheme.Field(LabelOf(field.Name),Choose(Subjects(field.Nodes.Select(ObjectivesData.SubjectKey)),field.Nodes.Count==0?"":ObjectivesData.SubjectKey(field.Nodes[0]),key=>{field.Nodes.Clear();if(key!="")field.Nodes.Add(data.Subject(key));},field.Name=="InitialOwner"?"Uncontrolled":"Choose subject…")));
        }
        string CanonicalGroup(string key){var choice=data.Choices.FirstOrDefault(x=>x.Kind==2&&(x.Key==key||x.Alias==key));return choice==null?key:choice.Key;}
        void Field(TableLayoutPanel parent,TaskNode owner,TaskField field)
        {
            if(field==null)return;string name=field.Name,label=LabelOf(name),help=ObjectivesData.Help(name);if(field.Base=="tvtLobby_SelectSubject"){SubjectField(parent,field);return;}
            if(name=="Superiority"&&owner.TypeName=="tvtLobby_ZoneCaptureModule")
            {
                bool ratio=owner.Text("Condition")=="1";
                label=ratio?"Required multiplier (N)":"Required extra participants (N)";
                help=ratio?"Example: N = 3 requires at least 9 attackers against 3 defenders.":"Example: N = 3 requires at least 6 attackers against 3 defenders.";
            }
			if(owner.TypeName=="tvtLobby_RaiseFlagModule"&&name=="Flag"){Flags(parent,owner);return;}
			if(name=="HoldSeconds")help=owner.TypeName=="tvtLobby_RaiseFlagModule"?"Own the required number of flags at the same time. The timer follows the mode below; raising a flag is a separate interaction.":owner.TypeName=="tvtLobby_RouteCompletionTask"?"Each target must remain at each waypoint for this duration, in route order.":"Simultaneous mode: keep the required number of targets qualified for this duration. Cumulative mode: each target is accepted separately after this duration.";
			if(owner.TypeName=="tvtLobby_RaiseFlagModule"&&name=="Amount")label="Required flags";
			if(owner.TypeName=="tvtLobby_RaiseFlagModule"&&name=="Count")help="Number of flags that must be controlled simultaneously. Cannot exceed the selected flags.";
			if(owner.TypeName=="tvtLobby_RaiseFlagModule"&&name=="AllowRecapture")help="Allow another capturing side to take a raised flag. When disabled, each pole can be raised only once.";
			if(owner.TypeName=="tvtLobby_RaiseFlagModule"&&name=="InteractionSeconds")help="How long a player holds the action at a pole. This is separate from the objective's hold timer; 0 means instant.";
			if(owner.TypeName=="tvtLobby_PresenceCondition"&&name=="Count")help="Compare the number of eligible participants currently inside the zone against this number.";
            if(name=="CaptureExpression"){ExpressionField(parent,owner,field);return;}
            if(name=="Notifications"){Notifications(parent,field);return;}
            if(field.Kind=="object"||field.Kind=="objects"){Objects(parent,owner,field);return;}
            if(field.Kind=="strings"){StringList(parent,owner,field);return;}
            Control input;
            if(field.Kind=="bool")
            {var check=new EditorToggle{Text=label,Checked=field.Value=="1"||field.Value=="true",Height=34};check.CheckedChanged+=(s,e)=>{field.Value=check.Checked?"1":"0";Touch(name=="ConfirmDeath");};EditorTheme.Add(parent,check);if(help!="")EditorTheme.Add(parent,EditorTheme.Label(help,8.5f,EditorTheme.Muted));return;}
            if(field.EnumValues.Count>0)
            {
                var choices=field.EnumValues.Select((v,i)=>new TaskChoice{Key=v.ToString(),Label=OptionLabel(name,v,field.EnumNames[i])}).ToList();
				if(name=="Amount"&&owner.TypeName=="tvtLobby_RaiseFlagModule")foreach(var choice in choices)choice.Label=choice.Label.Replace("targets","flags").Replace("target","flag");
                if(name=="Event")choices=choices.Where(c=>EventAllowed(Int32.Parse(c.Key))).ToList();
				if(name=="Audience"&&owner.TypeName=="tvtLobby_NotificationRule")choices=choices.Where(c=>AudienceAllowed(owner,Int32.Parse(c.Key))).ToList();
                input=Choose(choices,field.Value,v=>{if(v!=""){field.Value=v;if(new[]{"Condition","Amount","Audience","Display","Lifetime","Repeat","Event"}.Contains(name))Touch(true);}},"Choose option…");
            }
            else if(name=="Member"&&owner.TypeName=="tvtLobby_GroupMemberTarget")
            {input=Choose(data.Choices.Where(x=>x.Kind==100&&x.Group==CanonicalGroup(owner.Text("Group"))),field.Value,v=>{var slot=data.Choices.FirstOrDefault(x=>x.Kind==100&&x.Group==owner.Text("Group")&&x.Key==v);if(slot!=null){field.Value=v;owner.Set("ExpectedPrefab",slot.Prefab);}});}
            else if(ReferenceKind(owner.TypeName,name)!=0)
            {input=Choose(References(owner.TypeName,name),field.Value,v=>{field.Value=v;if(name=="Group"&&owner.TypeName=="tvtLobby_GroupMemberTarget"){owner.Set("Member","1");var first=data.Choices.FirstOrDefault(x=>x.Kind==100&&x.Group==v&&x.Member==1);owner.Set("ExpectedPrefab",first==null?"":first.Prefab);Touch(true);}});}
            else if(field.Kind=="int"||field.Kind=="float")
            {decimal value;Decimal.TryParse(field.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value);var number=new NumericUpDown{Minimum=0,Maximum=name=="Percentage"?100:1000000,DecimalPlaces=field.Kind=="float"?2:0,ThousandsSeparator=true};number.Value=Math.Max(number.Minimum,Math.Min(number.Maximum,value));number.ValueChanged+=(s,e)=>{field.Value=number.Value.ToString(CultureInfo.InvariantCulture);Touch();};input=number;}
            else
            {bool multi=name=="Text"||name=="AuthorNote";var text=new TextBox{Text=field.Value??"",Multiline=multi,Height=multi?90:30,MaxLength=multi?2400:512,ScrollBars=multi?ScrollBars.Vertical:ScrollBars.None};text.TextChanged+=(s,e)=>{field.Value=text.Text;Touch();};input=text;}
            input.Name="Objective_"+owner.TypeName+"_"+name;input.AccessibleName=label;EditorTheme.Add(parent,EditorTheme.Field(label,input,help));
        }
        public static int ReferenceKind(string type,string field)
        {if(new[]{"Zone","Zones","SafeZone","Waypoints"}.Contains(field))return 3;if(field=="Flag"||field=="AdditionalFlags")return 4;if(field=="EntityName")return 1;if(field=="Group")return 2;if(field=="Objective")return 5;if(field=="Set")return 7;if(field=="Tag")return 8;return 0;}
        IEnumerable<TaskChoice> References(string type,string field)
        {
            int kind=ReferenceKind(type,field);var result=data.Choices.Where(x=>x.Kind==kind).GroupBy(x=>x.Key).Select(x=>x.First()).ToList();
            if(kind==5)result=result.Where(x=>x.Key!=edit.Definition.Text("Name")).ToList();if(kind==7)result=result.Where(x=>edit.Definition.TypeName!="tvtLobby_TargetSet"||x.Key!=edit.Definition.Text("Name")).ToList();
            if(kind==3||kind==4)result.Add(new TaskChoice{Key="@new:"+Guid.NewGuid().ToString("N"),Label=kind==3?"+ Create a new zone on Apply":"+ Create a new flag on Apply"});return result;
        }
        void StringList(TableLayoutPanel parent,TaskNode owner,TaskField field)
        {
            var p=Card(LabelOf(field.Name));EditorTheme.Add(p,EditorTheme.Label(ObjectivesData.Help(field.Name),8.5f,EditorTheme.Muted));
            for(int i=0;i<field.Values.Count;i++){int at=i;EditorTheme.Add(p,Choose(References(owner.TypeName,field.Name),field.Values[i],v=>field.Values[at]=v));var buttons=EditorTheme.Stack();EditorTheme.Add(buttons,EditorTheme.Pair(EditorTheme.Button("↑ Up",()=>{if(at>0){string value=field.Values[at];field.Values.RemoveAt(at);field.Values.Insert(at-1,value);Touch(true);}}),EditorTheme.Button("Remove",()=>{field.Values.RemoveAt(at);Touch(true);})));EditorTheme.Add(p,buttons);}
            EditorTheme.Add(p,EditorTheme.Button("+ Add zone",()=>{field.Values.Add("");Touch(true);}));EditorTheme.Add(parent,p);
        }
		void Flags(TableLayoutPanel parent,TaskNode owner)
		{
			var flags=new List<string>{owner.Text("Flag")};
			var additional=owner.Field("AdditionalFlags");if(additional!=null)flags.AddRange(additional.Values);
			Action save=()=>{owner.Set("Flag",flags.Count==0?"":flags[0]);if(additional!=null)additional.Values=flags.Skip(1).ToList();};
			var card=Card("Flags");EditorTheme.Add(card,EditorTheme.Label("Select existing poles or create configured flags on Apply. Every pole has its own owner. A pole belongs to one Flag Control objective.",9,EditorTheme.Muted));
			for(int i=0;i<flags.Count;i++)
			{
				int at=i;var picker=Choose(References(owner.TypeName,"Flag"),flags[i],value=>{flags[at]=value;save();});picker.Name="Objective_Flag_"+i;
				EditorTheme.Add(card,EditorTheme.Pair(picker,EditorTheme.Button("Remove",()=>{flags.RemoveAt(at);save();Touch(true);}))); 
			}
			EditorTheme.Add(card,EditorTheme.Button("+ Add flag",()=>{flags.Add("");save();Touch(true);}));EditorTheme.Add(parent,card);
		}
        string Summary(TaskNode n){if(n.Text("Name")!="")return n.Text("Name");foreach(string key in new[]{"EntityName","Group","Objective","Set","Tag"})if(n.Text(key)!=""){var c=data.Choices.FirstOrDefault(x=>x.Key==n.Text(key));return (c==null?n.Text(key):c.Label)+(n.TypeName=="tvtLobby_GroupMemberTarget"?" · place "+n.Text("Member"):"");}return ObjectivesData.Title(n.TypeName);}
        void Objects(TableLayoutPanel parent,TaskNode owner,TaskField field)
        {
            var p=Card(LabelOf(field.Name));string help=ObjectivesData.Help(field.Name);if(help!="")EditorTheme.Add(p,EditorTheme.Label(help,8.5f,EditorTheme.Muted));
            for(int i=0;i<field.Nodes.Count;i++)
            {var node=field.Nodes[i];int index=i;var heading=EditorTheme.Button((expanded.Contains(node)?"▾ ":"▸ ")+Summary(node),()=>{if(!expanded.Add(node))expanded.Remove(node);Touch(true);});((EditorButton)heading).Navigation=true;EditorTheme.Add(p,heading);
                if(expanded.Contains(node))
                {var inside=EditorTheme.Stack(10);foreach(var f in node.Fields)if(Show(node,f))Field(inside,node,f);EditorTheme.Add(inside,EditorTheme.Pair(EditorTheme.Button("↑ Move up",()=>{if(index>0){field.Nodes.RemoveAt(index);field.Nodes.Insert(index-1,node);Touch(true);}}),EditorTheme.Button("Remove",()=>{field.Nodes.Remove(node);Touch(true);})));EditorTheme.Add(p,inside);}
            }
            if(field.Kind=="objects"||field.Nodes.Count==0)
            {
                string[] types=field.Base=="tvtLobby_TargetReference"?new[]{"tvtLobby_EntityTarget","tvtLobby_GroupTarget","tvtLobby_GroupMemberTarget","tvtLobby_TagTarget","tvtLobby_SetTarget"}:new[]{field.Base};
                if(types.Length==1)EditorTheme.Add(p,EditorTheme.Button("+ Add "+LabelOf(field.Name).ToLowerInvariant(),()=>AddObject(field,types[0])));
                else{var picker=Choose(types.Select(x=>new TaskChoice{Key=x,Label=ObjectivesData.Title(x)}),"",x=>{if(x!="")AddObject(field,x);},"+ Add target…");EditorTheme.Add(p,picker);}
            }
            if(field.Name=="Notifications")EditorTheme.Add(p,EditorTheme.Button("Add progress + result hints",()=>{Preset(field);Touch(true);}));
            EditorTheme.Add(parent,p);
        }
        void AddObject(TaskField field,string type){var n=data.Make(type);if(n==null)return;if(type=="tvtLobby_NotificationRule"){n.Set("Enabled","1");n.Set("Name","Notification "+(field.Nodes.Count+1));n.Set("Text","{objective}");}field.Nodes.Add(n);expanded.Add(n);Touch(true);}
        void Preset(TaskField field)
        {if(field==null)return;foreach(int ev in new[]{1,2})AddNotice(field,ev);}
        bool EventAllowed(int e){int type=Array.IndexOf(ObjectivesData.Types,edit.Definition.TypeName);if(e==0||e==2)return true;if(type==1)return new[]{1,5,6,8,9}.Contains(e);if(type==0)return new[]{1,5,6,7,8,9}.Contains(e);if(e==1||e==3||e==4)return true;if(e==10)return type==3;if(e==11)return type==6;return (e==8||e==9)&&type>=4;}
		bool AudienceAllowed(TaskNode rule,int audience)
		{
			int ev;Int32.TryParse(rule.Text("Event"),out ev);int type=Array.IndexOf(ObjectivesData.Types,edit.Definition.TypeName);
			if(audience>=4&&audience<=6)return ev==10||ev==11||ev>=15;
			if(audience==7||audience==8)return (type==0||type==1)&&(ev==5||ev==6);
			return true;
		}
        void Result(TableLayoutPanel p)
        {
            var toggle=new EditorToggle{Text="End mission when this objective succeeds",Checked=edit.EndMission!=0,Height=34};toggle.CheckedChanged+=(s,e)=>{edit.EndMission=toggle.Checked?1:0;Touch(true);};EditorTheme.Add(p,toggle);
            if(edit.EndMission==0){EditorTheme.Add(p,EditorTheme.Label("Progress objective. Other tasks may depend on it. Combined outcomes remain available in Mission End.",9,EditorTheme.Muted));return;}
            var allowed=new List<string>();var captures=edit.Definition.Field("Capturers");var subject=edit.Definition.Field("ResultSubject");if(captures!=null)allowed.AddRange(captures.Nodes.Select(ObjectivesData.SubjectKey));if(subject!=null)allowed.AddRange(subject.Nodes.Select(ObjectivesData.SubjectKey));
            EditorTheme.Add(p,EditorTheme.Field("Successful result for",Choose(Subjects(new[]{edit.EndSubject},allowed),edit.EndSubject,x=>edit.EndSubject=x)));
            EditorTheme.Add(p,EditorTheme.Field("Winner",Choose(Subjects(new[]{edit.Winner}),edit.Winner,x=>{edit.Winner=x;edit.Losers.RemoveAll(k=>data.Subjects.Any(a=>a.Key==k&&data.Subjects.Any(b=>b.Key==x&&a.Members.Intersect(b.Members).Any())));Touch(true);})));
            var automatic=new EditorToggle{Text="Other sides lose automatically",Checked=edit.AutoLosers!=0,Height=34};automatic.CheckedChanged+=(s,e)=>{edit.AutoLosers=automatic.Checked?1:0;if(automatic.Checked)edit.Losers.Clear();Touch(true);};EditorTheme.Add(p,automatic);
            if(edit.AutoLosers==0)
            {
                EditorTheme.Add(p,EditorTheme.Label("Losing sides · unselected sides remain undecided",9,EditorTheme.Muted));
                foreach(var side in SubjectOptions.Visible(data.Subjects,edit.Losers).Where(x=>!data.Subjects.Any(w=>w.Key==edit.Winner&&w.Members.Intersect(x.Members).Any())))
                {
                    var losing=new EditorToggle{Text=side.Name,Checked=edit.Losers.Any(k=>SubjectOptions.Same(data.Subjects,k,side.Key)),Height=34};losing.CheckedChanged+=(s,e)=>{edit.Losers.RemoveAll(k=>SubjectOptions.Same(data.Subjects,k,side.Key));if(losing.Checked)edit.Losers.Add(side.Key);Touch();};EditorTheme.Add(p,losing);
                }
            }
            var text=new TextBox{Multiline=true,Height=80,MaxLength=600,Text=edit.Message??""};text.TextChanged+=(s,e)=>{edit.Message=text.Text;Touch();};EditorTheme.Add(p,EditorTheme.Field("Result message",text,"Display and timing: Mission settings → Mission ending. This text also appears on Debriefing."));
        }
    }
    public sealed class ObjectiveCheck : CheckBox
    {
        public ObjectiveCheck(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;CheckedChanged+=(s,e)=>Invalidate();}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent.BackColor);var box=new Rectangle(1,(Height-14)/2,14,14);using(var b=new SolidBrush(Checked?EditorTheme.Amber:EditorTheme.Raised))e.Graphics.FillRectangle(b,box);using(var p=new Pen(Checked?EditorTheme.Amber:EditorTheme.Muted))e.Graphics.DrawRectangle(p,box);if(Checked)using(var p=new Pen(EditorTheme.Background,2)){e.Graphics.DrawLine(p,4,box.Y+7,7,box.Y+10);e.Graphics.DrawLine(p,7,box.Y+10,13,box.Y+4);}TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(24,0,Width-24,Height),EditorTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);}
    }
    public sealed class ObjectiveWizard : Form
    {
        readonly ObjectivesData data;readonly Func<string,string> unique;readonly Panel host=new Panel{Dock=DockStyle.Fill};readonly Label progress=EditorTheme.Label("",11,EditorTheme.Amber,true),error=EditorTheme.Label("",9,EditorTheme.Amber);
        readonly EditorButton back=new EditorButton{Text="Back"},next=new EditorButton{Text="Next",Primary=true};int step;
        public ObjectiveEdit Edit{get;private set;}
        public int Step{get{return step;}}
        public ObjectiveWizard(ObjectivesData source,Func<string,string> name)
        {
            data=source;unique=name;Text="Add objective · tvtLobby";Size=new Size(910,850);MinimumSize=new Size(710,600);StartPosition=FormStartPosition.CenterParent;BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;Font=EditorTheme.Font();
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(26)};frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,50));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,85));frame.Controls.Add(progress);frame.Controls.Add(host,0,1);var footer=EditorTheme.Stack();var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};buttons.Controls.AddRange(new Control[]{EditorTheme.Button("Cancel",()=>{DialogResult=DialogResult.Cancel;Close();}),back,next});EditorTheme.Add(footer,buttons);EditorTheme.Add(footer,error);frame.Controls.Add(footer,0,2);Controls.Add(frame);
            back.Click+=(s,e)=>{step=Math.Max(0,step-1);Render();};next.Click+=(s,e)=>Advance();Render();
        }
        public void ChooseType(string type){if(Edit==null||Edit.Definition.TypeName!=type){Edit=new ObjectiveEdit{Definition=data.Make(type)};Edit.Definition.Set("Name",unique(ObjectivesData.Title(type)));}step=1;Render();}
        public void Advance(){if(Edit==null)return;if(step==4){string issue=Edit.Validate();if(issue!=null){error.Text=issue;return;}DialogResult=DialogResult.OK;Close();return;}step++;Render();}
        void Render()
        {
            foreach(Control c in host.Controls.Cast<Control>().ToArray())c.Dispose();string[] titles={"Choose objective type","Participants & objects","Conditions","Activation & feedback","Result & review"};progress.Text=(step+1)+" / 5     "+titles[step];back.Enabled=step>0;next.Enabled=Edit!=null;next.Text=step==4?"Create objective":"Next";error.Text="";
            if(step==0){var scroll=new ObjectiveScroll();host.Controls.Add(scroll);string[] descriptions={"Capture and hold a zone with configurable presence rules.","Raise a flag; configure ownership and recapture.","Destroy selected vehicles or objects.","Kill selected characters; optional confirmation at the body.","Deliver objects, people or vehicles into selected zones.","Leave the origin zones and optionally reach a safe zone.","Move selected targets through an ordered route."};for(int i=0;i<ObjectivesData.Types.Length;i++){string type=ObjectivesData.Types[i];EditorTheme.Add(scroll.Body,new EditorButton{Text=ObjectivesData.Titles[i],Detail=descriptions[i],Height=95});scroll.Body.Controls[scroll.Body.Controls.Count-1].Click+=(s,e)=>ChooseType(type);}scroll.Fit();}else host.Controls.Add(new ObjectiveForm(data,Edit,()=>{},step));EditorTheme.Apply(this);
        }
    }
}
