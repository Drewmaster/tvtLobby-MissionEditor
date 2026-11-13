using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace TvtLobby.MissionEditor
{
    sealed class BriefingSectionButton : EditorButton
    {
        public int DropEdge;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if(DropEdge!=0)using(var pen=new Pen(EditorTheme.Amber,3))
                e.Graphics.DrawLine(pen,0,DropEdge<0?2:Height-3,Width,DropEdge<0?2:Height-3);
        }
    }
    public sealed class BriefingPage : UserControl
    {
        readonly Func<string,BriefingEdit,Task<FoundationReply>> send;
        readonly Panel listHost=new Panel(),detail=new Panel();
        readonly Label status=EditorTheme.Label("",9,EditorTheme.Muted);
        readonly EditorButton add=new EditorButton{Text="+ Add section",Primary=true},apply=new EditorButton{Text="Apply section",Primary=true},reload=new EditorButton{Text="Reload fields"},remove=new EditorButton{Text="Remove"},duplicate=new EditorButton{Text="Duplicate"},up=new EditorButton{Text="↑",Width=40},down=new EditorButton{Text="↓",Width=40};
        BriefingData data;BriefingEdit draft;string world="",baseline="",source="",root="";bool conflict,applying;
        const string SectionDragFormat="tvtLobby.MissionEditor.BriefingSection";
        Point dragOrigin;string dragId;bool suppressClick;int scrollAt;BriefingSectionButton dropButton;
        public event Action DraftChanged;
        public bool HasDraft{get{return draft!=null&&draft.Serialize()!=baseline;}}
        public string DraftError{get{return conflict?"Briefing changed in Workbench. Reload fields first.":draft==null?"Select a briefing section.":draft.Validate();}}
        public async Task<bool> ApplyPending(){if(DraftError!=null)return false;await Save("brief_apply");return !HasDraft;}
        public BriefingPage(Func<string,BriefingEdit,Task<FoundationReply>> action)
        {
            send=action;Dock=DockStyle.Fill;BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;Font=EditorTheme.Font();
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,45));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,85));
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};bar.Controls.Add(add);frame.Controls.Add(bar);
            var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,CellBorderStyle=TableLayoutPanelCellBorderStyle.Single,Margin=Padding.Empty};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,27));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,73));listHost.Dock=detail.Dock=DockStyle.Fill;listHost.Padding=new Padding(12);detail.Padding=new Padding(18,12,0,0);body.Controls.Add(listHost);body.Controls.Add(detail);frame.Controls.Add(body,0,1);
            var footer=EditorTheme.Stack();var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};buttons.Controls.AddRange(new Control[]{apply,reload,duplicate,remove,up,down});EditorTheme.Add(footer,buttons);EditorTheme.Add(footer,status);frame.Controls.Add(footer,0,2);Controls.Add(frame);
            add.Click+=(s,e)=>Create(false);duplicate.Click+=(s,e)=>Create(true);reload.Click+=(s,e)=>Select(draft==null?0:draft.Index);apply.Click+=async(s,e)=>await Save("brief_apply");remove.Click+=async(s,e)=>{if(MessageBox.Show(this,"Remove this section? Sections linked from another section must be unlinked first.","Remove section",MessageBoxButtons.OKCancel)==DialogResult.OK)await Save("brief_remove");};up.Click+=async(s,e)=>{if(draft!=null)await MoveSection(draft.Index,draft.Index-1);};down.Click+=async(s,e)=>{if(draft!=null)await MoveSection(draft.Index,draft.Index+1);};State();
        }
        public void Accept(FoundationReply reply,bool replace)
        {
            bool changedWorld=world!=reply.WorldSession;world=reply.WorldSession;root=reply.AddonRoot;var incoming=reply.Briefing;if(incoming!=null)incoming.Normalize();
            if(!replace&&!changedWorld&&HasDraft&&incoming!=null&&incoming.Serialize()!=source)conflict=true;
            data=incoming;
            if(replace||changedWorld||!HasDraft){string id=draft==null?"":draft.Definition.Text("Id");int index=data==null?-1:data.Sections.FindIndex(n=>n.Text("Id")==id);Select(index>=0?index:0);}else State();
        }
        public BriefingEdit ReadFields(){return draft==null?null:draft.Clone();}
        public bool CanLeave(){return !HasDraft||MessageBox.Show(this,"Discard unapplied briefing changes?","Unapplied briefing",MessageBoxButtons.OKCancel)==DialogResult.OK;}
        public void Applied(){if(draft!=null)baseline=draft.Serialize();conflict=false;}
        void Select(int index){draft=data==null||index<0||index>=data.Sections.Count?null:new BriefingEdit{Index=index,Definition=data.Sections[index].Clone()};baseline=draft==null?"":draft.Serialize();source=data==null?"":data.Serialize();conflict=false;Display();Changed();}
        void Changed(){State();if(DraftChanged!=null)DraftChanged();}
        void State()
        {
            bool ready=data!=null&&data.Version==1&&String.IsNullOrEmpty(data.Problem)&&!applying;
            add.Enabled=ready;apply.Enabled=ready&&HasDraft&&!conflict;reload.Enabled=draft!=null&&!applying;duplicate.Enabled=ready&&draft!=null&&!conflict;remove.Enabled=ready&&draft!=null&&draft.Index>=0&&!conflict&&!HasDraft;up.Enabled=remove.Enabled&&draft.Index>0;down.Enabled=remove.Enabled&&draft.Index<data.Sections.Count-1;
            status.Text=data==null?"Restart Workbench to load the Briefing editor.":!String.IsNullOrEmpty(data.Problem)?data.Problem:conflict?"Workbench changed. Draft retained; Reload fields before applying.":HasDraft?"Unapplied section. Apply or reload fields before reordering, then Save mission.":"Manual briefing · native Undo per edit · stable section links.";
        }
        void Display()
        {
            foreach(Control c in listHost.Controls.Cast<Control>().ToArray())c.Dispose();foreach(Control c in detail.Controls.Cast<Control>().ToArray())c.Dispose();
            if(data==null)return;var list=new ObjectiveScroll();listHost.Controls.Add(list);
            EditorTheme.Add(list.Body,EditorTheme.Label("Drag sections to reorder.\nTop section appears first in game.",9,EditorTheme.Muted));
            for(int i=0;i<data.Sections.Count;i++){int selected=i;var n=data.Sections[i];var b=new BriefingSectionButton{Name="BriefingSection:"+n.Text("Id"),Text=n.Text("Title"),Detail=PhaseLabel(n)+"\n"+(n.Text("Audience")=="0"?"All players":"Restricted audience"),Navigation=true,Selected=draft!=null&&draft.Index==i,Height=87};b.Click+=(s,e)=>{if(!suppressClick&&CanLeave())Select(selected);};EnableDragging(b,n.Text("Id"),list);EditorTheme.Add(list.Body,b);}
            if(data.Sections.Count==0)EditorTheme.Add(list.Body,EditorTheme.Label("No sections yet. Add only the information this mission needs.",9,EditorTheme.Muted));
            EditorTheme.Add(list.Body,EditorTheme.Label("Map and section links use saved references. Visibility is checked for each reader.",8.5f,EditorTheme.Muted));list.Fit();
            if(draft==null){var empty=new ObjectiveScroll();detail.Controls.Add(empty);EditorTheme.Add(empty.Body,EditorTheme.Label("Write the mission briefing",17,null,true));EditorTheme.Add(empty.Body,EditorTheme.Label("Add Overview, Situation, Mission, Execution or your own section. Rules are never generated into the text.",10,EditorTheme.Muted));empty.Fit();return;}
            detail.Controls.Add(new BriefingForm(data,draft,root,Changed,GoTo));EditorTheme.Apply(this);
        }
        static string PhaseLabel(TaskNode n){int phases;Int32.TryParse(n.Text("Phases"),out phases);var labels=new List<string>();if((phases&2)!=0)labels.Add("Waiting");if((phases&16)!=0)labels.Add("Briefing");if((phases&32)!=0){if(n.Text("DuringFreeze")=="1")labels.Add("Freeze");if(n.Text("AfterFreeze")=="1")labels.Add("Game");}return n.Text("Enabled")=="0"?"Disabled":String.Join(" · ",labels);}
        void GoTo(string id){if(CanLeave())Select(data.Sections.FindIndex(s=>s.Text("Id")==id));}
        bool CanReorder(){return Enabled&&data!=null&&data.Version==1&&String.IsNullOrEmpty(data.Problem)&&!applying&&!conflict&&!HasDraft;}
        void ClearDrop()
        {
            if(dropButton!=null&&!dropButton.IsDisposed){dropButton.DropEdge=0;dropButton.Invalidate();}dropButton=null;
        }
        void EnableDragging(BriefingSectionButton row,string id,ObjectiveScroll list)
        {
            row.AllowDrop=true;
            row.MouseDown+=(s,e)=>{suppressClick=false;dragId=null;if(e.Button==MouseButtons.Left&&CanReorder()){dragOrigin=e.Location;dragId=id;}};
            row.MouseUp+=(s,e)=>dragId=null;
            row.MouseMove+=(s,e)=>{
                if(e.Button!=MouseButtons.Left||dragId!=id||!CanReorder())return;
                var threshold=new Rectangle(dragOrigin.X-SystemInformation.DragSize.Width/2,dragOrigin.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);
                if(threshold.Contains(e.Location))return;
                dragId=null;suppressClick=true;
                try{row.DoDragDrop(new DataObject(SectionDragFormat,id),DragDropEffects.Move);}
                finally{ClearDrop();if(!IsDisposed&&IsHandleCreated)BeginInvoke(new Action(()=>suppressClick=false));}
            };
            DragEventHandler over=(s,e)=>{
                ClearDrop();e.Effect=DragDropEffects.None;
                string sourceId=e.Data.GetData(SectionDragFormat) as string;
                if(!CanReorder()||sourceId==id||!data.Sections.Any(n=>n.Text("Id")==sourceId))return;
                var point=row.PointToClient(new Point(e.X,e.Y));row.DropEdge=point.Y<row.Height/2?-1:1;dropButton=row;row.Invalidate();e.Effect=DragDropEffects.Move;
                int now=Environment.TickCount;if(unchecked(now-scrollAt)>=120){var p=list.PointToClient(new Point(e.X,e.Y));if(p.Y<30)list.ScrollY=Math.Max(0,list.ScrollY-35);else if(p.Y>list.Height-30)list.ScrollY+=35;scrollAt=now;}
            };
            row.DragEnter+=over;row.DragOver+=over;row.DragLeave+=(s,e)=>ClearDrop();
            row.DragDrop+=async(s,e)=>{
                string sourceId=e.Data.GetData(SectionDragFormat) as string;
                bool after=row.PointToClient(new Point(e.X,e.Y)).Y>=row.Height/2;ClearDrop();
                if(!CanReorder())return;
                int from=data.Sections.FindIndex(n=>n.Text("Id")==sourceId),target=data.Sections.FindIndex(n=>n.Text("Id")==id);
                if(from<0||target<0||from==target)return;int to=target+(after?1:0);if(from<to)to--;
                await MoveSection(from,to);
            };
        }
        async Task MoveSection(int from,int to)
        {
            if(!CanReorder()||from<0||to<0||from>=data.Sections.Count||to>=data.Sections.Count||from==to)return;
            await Save("brief_move",new BriefingEdit{Index=from,Destination=to,Definition=data.Sections[from].Clone()});
        }
        void Create(bool copy)
        {
            if(!CanLeave())return;var n=copy?draft.Definition.Clone():data.Make("tvtLobby_BriefSection");string old=n.Text("Id");n.Set("Id",Guid.NewGuid().ToString());n.Set("Title",copy?n.Text("Title")+" — copy":"New section");
            if(copy)foreach(var link in n.Field("Links").Nodes)if(link.Text("Section")==old)link.Set("Section",n.Text("Id"));
            draft=new BriefingEdit{Definition=n};baseline="";source=data.Serialize();conflict=false;Display();Changed();
        }
        async Task Save(string operation,BriefingEdit request=null)
        {
            request=request??draft;string error=request==null?"Select a section.":request.Validate();if(error!=null){status.Text=error;return;}applying=true;State();string message="";
            try{string id=request.Definition.Text("Id");var r=await send(operation,request.Clone());if(IsDisposed)return;message=r.Message;if(r.Ok==1){data=r.Briefing;data.Normalize();Select(data.Sections.FindIndex(n=>n.Text("Id")==id));}}
            finally{applying=false;if(!IsDisposed){State();if(!String.IsNullOrEmpty(message))status.Text=message;}}
        }
    }

    public sealed class BriefingForm : ObjectiveScroll
    {
        readonly BriefingData data;readonly BriefingEdit edit;readonly string root;readonly Action changed;readonly Action<string> navigate;
        BriefingTextEditor document;bool building,showPreview,showLegacy;readonly HashSet<string> expanded=new HashSet<string>();
        TaskNode Section{get{return edit.Definition;}}
        public BriefingForm(BriefingData data,BriefingEdit edit,string root,Action changed,Action<string> navigate){this.data=data;this.edit=edit;this.root=root;this.changed=changed;this.navigate=navigate;Render();}
        void Mark(){if(!building)changed();}
        TableLayoutPanel Card(string title,string help=""){var c=EditorTheme.Stack(16);c.BackColor=EditorTheme.Panel;c.Margin=new Padding(0,0,0,14);EditorTheme.Add(c,EditorTheme.Label(title,11,EditorTheme.Amber,true));if(help!="")EditorTheme.Add(c,EditorTheme.Label(help,9,EditorTheme.Muted));return c;}
        TextBox TextInput(TaskNode n,string field,int lines=1){var t=new TextBox{Text=n.Text(field),Multiline=lines>1,Height=lines>1?lines*22:30,MaxLength=field=="Caption"?1024:16384};t.TextChanged+=(s,e)=>{n.Set(field,t.Text);Mark();};return t;}
        NumericUpDown Number(TaskNode n,string field,decimal min,decimal max,int decimals=0){decimal value;Decimal.TryParse(n.Text(field),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value);var control=new NumericUpDown{Minimum=min,Maximum=max,DecimalPlaces=decimals,Value=Math.Max(min,Math.Min(max,value)),Height=30};control.ValueChanged+=(s,e)=>{n.Set(field,control.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));Mark();};return control;}
        ObjectiveCheck Check(string title,bool value,Action<bool> change){var c=new ObjectiveCheck{Text=title,Checked=value,AutoSize=false,Height=32};c.CheckedChanged+=(s,e)=>{change(c.Checked);Mark();};return c;}
        EditorCombo Combo(IEnumerable<TaskChoice> choices,string value,Action<string> change){var c=new EditorCombo();foreach(var choice in choices)c.Items.Add(choice);if(!String.IsNullOrEmpty(value)&&!c.Items.Cast<TaskChoice>().Any(x=>x.Key==value))c.Items.Add(new TaskChoice{Key=value,Label=value+" (missing)"});c.SelectedItem=c.Items.Cast<TaskChoice>().FirstOrDefault(x=>x.Key==value);c.SelectedIndexChanged+=(s,e)=>{var selected=c.SelectedItem as TaskChoice;if(selected!=null){change(selected.Key);Mark();}};return c;}
        static TaskChoice Option(string key,string label){return new TaskChoice{Key=key,Label=label};}
        void Render()
        {
            int scroll=ScrollY;building=true;
            try
            {
                while(Body.Controls.Count>0)Body.Controls[0].Dispose();Body.RowStyles.Clear();Body.RowCount=0;
                var identity=Card("Section");EditorTheme.Add(identity,EditorTheme.Field("Heading",TextInput(Section,"Title")));EditorTheme.Add(identity,Check("Section enabled",Section.Text("Enabled")=="1",v=>Section.Set("Enabled",v?"1":"0")));EditorTheme.Add(Body,identity);
                var text=Card("Briefing text","Write the briefing manually. Insert internal links and illustrations where they belong.");document=new BriefingTextEditor();document.LoadText(Section.Text("Text"));document.Changed+=()=>{Section.Set("Text",document.Read());Mark();};document.LinkRequested+=InsertLink;document.ImageRequested+=InsertImage;EditorTheme.Add(text,document);
                EditorTheme.Add(text,EditorTheme.Button(showPreview?"Hide player text preview":"Player text preview",()=>{showPreview=!showPreview;Render();}));if(showPreview)Preview(text);EditorTheme.Add(Body,text);
                Visibility();Assets();Legacy();EditorTheme.Apply(this);Fit();ScrollY=scroll;
            }finally{building=false;}
        }
        void Visibility()
        {
            var card=Card("Audience & visibility","Phase and audience checks always apply. Any matching Show event reveals the section; Hide wins when both occur together.");
            var audience=Combo(new[]{Option("0","All players"),Option("1","Players without a slot"),Option("4","Selected factions, sides or alliances"),Option("2","Faction IDs (existing)"),Option("3","Alliance IDs (existing)")},Section.Text("Audience"),v=>{Section.Set("Audience",v);Render();});EditorTheme.Add(card,EditorTheme.Field("Who can read this section",audience));
            int mode=Int32.Parse(Section.Text("Audience"));if(mode>=2)
            {
                var grid=new TableLayoutPanel{ColumnCount=2,AutoSize=true,Dock=DockStyle.Top};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
                foreach(var subject in SubjectOptions.Visible(data.Subjects,mode==4?Section.Field("Subjects").Nodes.Select(ObjectivesData.SubjectKey):null,data.Subjects.Where(s=>mode==4||mode==2&&s.Key.StartsWith("0:")||mode==3&&s.Key.StartsWith("2:")).Select(s=>s.Key)))
                {var item=subject;bool selected=mode==4?Section.Field("Subjects").Nodes.Any(n=>SubjectOptions.Same(data.Subjects,ObjectivesData.SubjectKey(n),item.Key)):Section.Field("AudienceIds").Values.Contains(item.Key.Substring(2));var c=Check(subject.Name,selected,v=>{if(mode==4){var nodes=Section.Field("Subjects").Nodes;nodes.RemoveAll(n=>SubjectOptions.Same(data.Subjects,ObjectivesData.SubjectKey(n),item.Key));if(v)nodes.Add(data.Subject(item.Key));}else{var ids=Section.Field("AudienceIds").Values;ids.Remove(item.Key.Substring(2));if(v)ids.Add(item.Key.Substring(2));}});c.Dock=DockStyle.Fill;grid.Controls.Add(c);}
                EditorTheme.Add(card,grid);
            }
            var phaseGrid=new TableLayoutPanel{ColumnCount=2,AutoSize=true,Dock=DockStyle.Top};phaseGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));phaseGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            Phase(phaseGrid,"Waiting",2);Phase(phaseGrid,"Briefing",16);
            int phases=Int32.Parse(Section.Text("Phases"));foreach(string field in new[]{"DuringFreeze","AfterFreeze"}){string flag=field;var c=Check(flag=="DuringFreeze"?"Freeze":"Game (after freeze)",(phases&32)!=0&&Section.Text(flag)=="1",v=>{Section.Set(flag,v?"1":"0");int mask=Int32.Parse(Section.Text("Phases"));if(Section.Text("DuringFreeze")=="1"||Section.Text("AfterFreeze")=="1")mask|=32;else mask&=~32;Section.Set("Phases",mask.ToString());});c.Dock=DockStyle.Fill;phaseGrid.Controls.Add(c);}EditorTheme.Add(card,EditorTheme.Label("Visible during",9,null,true));EditorTheme.Add(card,phaseGrid);
            EditorTheme.Add(card,Check("Initially visible",Section.Text("InitiallyVisible")=="1",v=>Section.Set("InitiallyVisible",v?"1":"0")));
            VisibilityEvents(card,"ShowOn","Show after objective event");VisibilityEvents(card,"HideOn","Hide after objective event");EditorTheme.Add(Body,card);
        }
        void Phase(TableLayoutPanel parent,string label,int bit){int mask=Int32.Parse(Section.Text("Phases"));var c=Check(label,(mask&bit)!=0,v=>{int flags=Int32.Parse(Section.Text("Phases"));Section.Set("Phases",(v?flags|bit:flags&~bit).ToString());});c.Dock=DockStyle.Fill;parent.Controls.Add(c);}
        void VisibilityEvents(TableLayoutPanel card,string field,string label)
        {
            var events=Section.Field(field).Nodes;EditorTheme.Add(card,EditorTheme.Label(label,10,null,true));
            foreach(var rule in events.ToArray())
            {
                var row=Card("Objective event");var task=Combo(data.Objectives.Select(x=>Option(x.Key,x.Name)),rule.Text("Objective"),v=>{rule.Set("Objective",v);rule.Field("Capturer").Nodes.Clear();Render();});var kind=Combo(new[]{Option("0","Activated"),Option("1","Completed / captured"),Option("2","Failed"),Option("3","Skipped")},rule.Text("Event"),v=>{rule.Set("Event",v);if(v!="1")rule.Field("Capturer").Nodes.Clear();Render();});EditorTheme.Add(row,EditorTheme.Pair(EditorTheme.Field("Objective",task),EditorTheme.Field("Event",kind)));
                var objective=data.Objectives.FirstOrDefault(x=>x.Key==rule.Text("Objective"));if(objective!=null&&objective.Control==1&&rule.Text("Event")=="1")
                {var opts=new List<TaskChoice>{Option("","Any capturing party")};opts.AddRange(objective.Owners.Select(k=>Option(k,data.Label(k))));var selected=rule.Field("Capturer").Nodes.FirstOrDefault();EditorTheme.Add(row,EditorTheme.Field("Capturing party",Combo(opts,ObjectivesData.SubjectKey(selected),v=>{var list=rule.Field("Capturer").Nodes;list.Clear();if(v!="")list.Add(data.Subject(v));})));}
                EditorTheme.Add(row,EditorTheme.Button("Remove event",()=>{events.Remove(rule);Mark();Render();}));EditorTheme.Add(card,row);
            }
            var add=EditorTheme.Button("+ Add event",()=>{var n=data.Make("tvtLobby_BriefVisibilityRule");n.Set("Objective",data.Objectives[0].Key);events.Add(n);Mark();Render();});add.Enabled=data.Objectives.Count>0&&events.Count<32;EditorTheme.Add(card,add);if(data.Objectives.Count==0)EditorTheme.Add(card,EditorTheme.Label("Add an objective before configuring an event.",8.5f,EditorTheme.Muted));
        }
        string Key(string field,string prefix){int i=1;while(Section.Field(field).Nodes.Any(n=>n.Text("Key")==prefix+i))i++;return prefix+i;}
        void InsertLink()
        {
            var choices=new List<TaskChoice>();choices.AddRange(data.Targets.Select(x=>Option("map:"+x.Key,x.Label)));choices.AddRange(data.Sections.Where(x=>x.Text("Id")!=Section.Text("Id")).Select(x=>Option("section:"+x.Text("Id"),x.Text("Title")+" · Briefing section")));
            if(choices.Count==0){MessageBox.Show(this,"Create a zone, marker or another saved section first.");return;}
            var destination=Combo(choices,choices[0].Key,v=>{});var label=new TextBox{Text=String.IsNullOrWhiteSpace(document.SelectedText)?"View location":document.SelectedText};
            string target,caption;
            using(var dialog=new BriefDialog("Insert internal link")){dialog.Add(EditorTheme.Field("Link text",label));dialog.Add(EditorTheme.Field("Destination",destination));dialog.Add(EditorTheme.Label("The reader must also have access to the destination. External URLs are not supported.",9,EditorTheme.Muted));if(dialog.ShowDialog(this)!=DialogResult.OK||String.IsNullOrWhiteSpace(label.Text)||destination.SelectedItem==null)return;target=((TaskChoice)destination.SelectedItem).Key;caption=label.Text;}
            var link=data.Make("tvtLobby_BriefLink");string key=Key("Links","link");link.Set("Key",key);if(target.StartsWith("section:"))link.Set("Section",target.Substring(8));else link.Set("Target",target.Substring(4));Section.Field("Links").Nodes.Add(link);document.InsertLink(key,caption);Section.Set("Text",document.Read());Mark();Render();
        }
        void InsertImage()
        {
            var image=data.Make("tvtLobby_BriefIllustration");string key=Key("Images","image");image.Set("Key",key);
            using(var dialog=new BriefDialog("Insert illustration")){var fields=ImageFields(image);dialog.Add(fields);if(dialog.ShowDialog(this)!=DialogResult.OK||String.IsNullOrWhiteSpace(image.Text("Texture")))return;}
            Section.Field("Images").Nodes.Add(image);document.InsertImage(key);Section.Set("Text",document.Read());Mark();Render();
        }
        TableLayoutPanel ImageFields(TaskNode image)
        {
            var fields=EditorTheme.Stack();var texture=TextInput(image,"Texture",2);var preview=new ResourcePreview{Height=160,Root=root,Resource=image.Text("Texture")};texture.TextChanged+=(s,e)=>preview.Resource=texture.Text;
            EditorTheme.Add(fields,preview);EditorTheme.Add(fields,EditorTheme.Field("Registered .edds resource",texture));EditorTheme.Add(fields,EditorTheme.Button("Choose .edds…",()=>PickResource(texture)));EditorTheme.Add(fields,EditorTheme.Pair(EditorTheme.Field("Width (%)",Number(image,"Width",10,100)),EditorTheme.Field("Caption",TextInput(image,"Caption"))));return fields;
        }
        void PickResource(TextBox target)
        {
            using(var picker=new OpenFileDialog{Title="Choose a registered illustration",Filter="Enfusion texture (*.edds)|*.edds"})
            {if(picker.ShowDialog(this)!=DialogResult.OK)return;string meta=picker.FileName+".meta";if(!File.Exists(meta)){MessageBox.Show(this,"Import/register the texture in Resource Browser first, or paste its resource name.");return;}var match=Regex.Match(File.ReadAllText(meta),"Name\\s+\"([^\"]+)\"");if(!match.Success){MessageBox.Show(this,"No registered resource name found in .meta.");return;}target.Text=match.Groups[1].Value;}
        }
        void Assets()
        {
            var links=Section.Field("Links").Nodes;var images=Section.Field("Images").Nodes;if(links.Count==0&&images.Count==0)return;
            var card=Card("Links & illustrations","Insert these from the text toolbar. Editing the destination keeps its text in place.");
            foreach(var link in links.ToArray())
            {
                string key=link.Text("Key");var row=Card("Link · "+key);var options=data.Targets.Select(x=>Option("map:"+x.Key,x.Label)).Concat(data.Sections.Where(x=>x.Text("Id")!=Section.Text("Id")).Select(x=>Option("section:"+x.Text("Id"),x.Text("Title")+" · Section"))).ToList();string target=link.Text("Section")==""?"map:"+link.Text("Target"):"section:"+link.Text("Section");EditorTheme.Add(row,Combo(options,target,v=>{link.Set("Target",v.StartsWith("map:")?v.Substring(4):"");link.Set("Section",v.StartsWith("section:")?v.Substring(8):"");Render();}));
                if(link.Text("Section")=="")EditorTheme.Add(row,EditorTheme.Field("Map zoom (0 = fit target)",Number(link,"Zoom",0,10,2)));
                EditorTheme.Add(row,EditorTheme.Button("Remove link, keep text",()=>{Section.Set("Text",Regex.Replace(Section.Text("Text"),@"\[link="+Regex.Escape(key)+@"\](.*?)\[/link\]","$1",RegexOptions.Singleline));links.Remove(link);Mark();Render();}));EditorTheme.Add(card,row);
            }
            foreach(var image in images.ToArray())
            {string key=image.Text("Key");var row=Card("Illustration · "+key);EditorTheme.Add(row,ImageFields(image));EditorTheme.Add(row,EditorTheme.Button("Remove illustration",()=>{Section.Set("Text",Section.Text("Text").Replace("[image="+key+"]",""));images.Remove(image);Mark();Render();}));EditorTheme.Add(card,row);}EditorTheme.Add(Body,card);
        }
        void Legacy()
        {
            var card=Card("Additional section settings");EditorTheme.Add(card,EditorTheme.Button(showLegacy?"Hide additional settings":"Show additional settings",()=>{showLegacy=!showLegacy;Render();}));
            if(showLegacy)
            {
                Phase(card,"Creating",1);Phase(card,"Lobby",4);Phase(card,"Pre-brief",8);Phase(card,"Debriefing",64);
                var image=TextInput(Section,"Image",2);EditorTheme.Add(card,EditorTheme.Field("Existing end-of-section illustration",image));EditorTheme.Add(card,EditorTheme.Button("Choose .edds…",()=>PickResource(image)));
                EditorTheme.Add(card,Check("Show on map button with fixed coordinates",Section.Text("HasMapTarget")=="1",v=>{Section.Set("HasMapTarget",v?"1":"0");Render();}));
                if(Section.Text("HasMapTarget")=="1"){EditorTheme.Add(card,EditorTheme.Field("World position · X Y Z",TextInput(Section,"MapTarget")));EditorTheme.Add(card,EditorTheme.Field("Map zoom",Number(Section,"MapZoom",0,10,2)));}
            }EditorTheme.Add(Body,card);
        }
        void Preview(TableLayoutPanel parent)
        {
            var card=Card("Player text preview","Approximate desktop rendering. In-game fonts and available links depend on the reader's access.");
            foreach(string part in Regex.Split(Section.Text("Text"),@"(\[image=[^\]]+\])"))
            {
                if(part.StartsWith("[image=")){string key=part.Substring(7,part.Length-8);var img=Section.Field("Images").Nodes.FirstOrDefault(x=>x.Text("Key")==key);if(img==null)continue;var pic=new ResourcePreview{Height=210,Root=root,Resource=img.Text("Texture")};var centered=new TableLayoutPanel{ColumnCount=3,Height=210,Dock=DockStyle.Top};float w=Int32.Parse(img.Text("Width"));centered.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,(100-w)/2));centered.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,w));centered.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,(100-w)/2));pic.Dock=DockStyle.Fill;centered.Controls.Add(pic,1,0);EditorTheme.Add(card,centered);EditorTheme.Add(card,EditorTheme.Label(img.Text("Caption"),9,EditorTheme.Muted));}
                else if(part!=""){var reader=new BriefingTextEditor();reader.LoadText(part);var rtf=new RichTextBox{Font=EditorTheme.Font(11),ReadOnly=true,DetectUrls=false,BorderStyle=BorderStyle.None,BackColor=EditorTheme.Panel,ForeColor=EditorTheme.Text,Height=Math.Max(65,Math.Min(300,part.Split('\n').Length*24+24)),ScrollBars=RichTextBoxScrollBars.Vertical};rtf.Rtf=reader.TextArea.Rtf;reader.Dispose();EditorTheme.Add(card,rtf);}
            }
            foreach(var link in Section.Field("Links").Nodes.Where(x=>x.Text("Section")!="")){string id=link.Text("Section");var target=data.Sections.FirstOrDefault(x=>x.Text("Id")==id);if(target!=null)EditorTheme.Add(card,EditorTheme.Button("Open section: "+target.Text("Title"),()=>navigate(id)));}
            EditorTheme.Add(parent,card);
        }
    }
    public sealed class BriefDialog : Form
    {
        readonly ObjectiveScroll content=new ObjectiveScroll();
        public BriefDialog(string title){Text=title;Size=new Size(590,600);MinimumSize=new Size(480,450);StartPosition=FormStartPosition.CenterParent;BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;Font=EditorTheme.Font();var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=50,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};var ok=new EditorButton{Text="Insert",Primary=true,DialogResult=DialogResult.OK};var cancel=new EditorButton{Text="Cancel",DialogResult=DialogResult.Cancel};actions.Controls.AddRange(new Control[]{ok,cancel});AcceptButton=ok;CancelButton=cancel;content.Padding=new Padding(18);Controls.Add(content);Controls.Add(actions);}
        public void Add(Control control){EditorTheme.Add(content.Body,control);EditorTheme.Apply(control);content.Fit();}
    }
}
