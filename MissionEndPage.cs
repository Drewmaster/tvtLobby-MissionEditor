using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class MissionEndPage : UserControl
    {
        readonly Func<MissionEndData,Task<FoundationReply>> send;
        readonly EditorTabs tabs=new EditorTabs();
        readonly Panel[] bodies=new Panel[3];
        readonly Label message=EditorTheme.Label("",9,EditorTheme.Muted);
        readonly EditorButton apply=new EditorButton{Text="Apply Mission End",Primary=true},reload=new EditorButton{Text="Reload fields"};
        MissionEndData current,draft; string baseline="",world=""; bool conflict,rendering;
        EndRule selected,selectedTime;
        readonly HashSet<EndRule> expanded=new HashSet<EndRule>();
        readonly Dictionary<string,EndRule> placeholderLosses=new Dictionary<string,EndRule>();
        readonly List<Action> fitForms=new List<Action>();
        public bool HasDraft{get{return draft!=null&&draft.EditableFingerprint()!=baseline;}}
        public string DraftError{get{return conflict?"Mission End changed in Workbench. Reload fields first.":draft==null?"Refresh Mission End first.":draft.Validate();}}
        public event Action DraftChanged;
        public MissionEndPage(Func<MissionEndData,Task<FoundationReply>> action)
        {
            send=action;Dock=DockStyle.Fill;Font=EditorTheme.Font();BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,78));
            tabs.Dock=DockStyle.Fill;tabs.ItemSize=new Size(170,38);frame.Controls.Add(tabs);
            string[] titles={"Objective outcomes","Critical losses","Time limit"};
            for(int i=0;i<3;i++){var tab=new TabPage(titles[i]){BackColor=EditorTheme.Background,Padding=Padding.Empty};bodies[i]=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,20,0,0)};tab.Controls.Add(bodies[i]);tabs.TabPages.Add(tab);}
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,12,0,0)};footer.Controls.AddRange(new Control[]{apply,reload,message});frame.Controls.Add(footer,0,1);Controls.Add(frame);
            SizeChanged+=(s,e)=>{tabs.ItemSize=new Size(Math.Min(170,Math.Max(110,Width/3-4)),38);message.MaximumSize=new Size(Math.Max(160,Width-330),0);};
            apply.Click+=async(s,e)=>{string error=draft.Validate();if(error!=null){message.Text=error;return;}var reply=await send(draft.Clone());if(reply.Ok!=1)message.Text=reply.Message;};
            reload.Click+=(s,e)=>LoadData(current);State();
        }
        public void Accept(FoundationReply reply,bool replace)
        {
            bool changed=world!=reply.WorldSession;world=reply.WorldSession;current=reply.MissionEnd;
            if(!replace&&!changed&&HasDraft&&current!=null&&current.EditableFingerprint()!=baseline)conflict=true;
            if(replace||changed||!HasDraft)LoadData(current);else State();
        }
        void LoadData(MissionEndData value){draft=value==null?null:value.Clone();baseline=draft==null?"":draft.EditableFingerprint();conflict=false;selected=selectedTime=null;expanded.Clear();placeholderLosses.Clear();Render();Changed();}
        public MissionEndData ReadFields(){if(draft==null)return null;var copy=draft.Clone();if(current!=null)copy.Profile=current.Clone().Profile;return copy;}
        public void Applied(){if(draft!=null)baseline=draft.EditableFingerprint();conflict=false;}
        public bool CanLeave(){return !HasDraft||MessageBox.Show(this,"Discard unapplied Mission End changes?","Unapplied settings",MessageBoxButtons.OKCancel)==DialogResult.OK;}
        void Changed(){if(rendering)return;State();if(DraftChanged!=null)DraftChanged();}
        void State(){bool available=draft!=null&&draft.Version==1;tabs.Enabled=available;apply.Enabled=available&&HasDraft&&!conflict&&String.IsNullOrEmpty(draft.Problem);reload.Enabled=available&&HasDraft;message.Text=!available?"Restart Workbench and reopen Mission Editor to load Mission End.":!String.IsNullOrEmpty(draft.Problem)?draft.Problem:conflict?"Workbench changed. Draft retained; Reload fields before applying.":HasDraft?"Unapplied changes. Apply, then Save mission.":"One native Undo restores the complete edit before Save.";}
        void Render(){rendering=true;try{fitForms.Clear();for(int i=0;i<3;i++){foreach(Control c in bodies[i].Controls.Cast<Control>().ToArray())c.Dispose();bodies[i].Controls.Clear();}if(draft==null)return;BuildOutcomes();BuildLosses();BuildTime();EditorTheme.Apply(this);foreach(var fit in fitForms)fit();}finally{rendering=false;State();}}
        TableLayoutPanel ScrollBody(Panel host)
        {
            var scroll=new Panel{Name="MissionEndScroll",Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(0,0,16,8)};
            var p=EditorTheme.Stack();p.Dock=DockStyle.None;p.Anchor=AnchorStyles.Top|AnchorStyles.Left;scroll.Controls.Add(p);host.Controls.Add(scroll);bool fitting=false;
            Action fit=()=>{if(fitting||scroll.IsDisposed)return;fitting=true;try{FitColumns(p,Math.Max(80,scroll.ClientSize.Width-32));p.PerformLayout();}finally{fitting=false;}};
            scroll.ClientSizeChanged+=(s,e)=>fit();fitForms.Add(fit);return p;
        }
        internal static void FitColumns(Control root,int width)
        {
            root.SuspendLayout();
            try
            {
                width=Math.Max(40,width);root.MaximumSize=Size.Empty;root.Width=width;
                var label=root as Label;
                if(label!=null){label.AutoSize=false;label.Height=TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(1,width-label.Padding.Horizontal),0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+label.Padding.Vertical+2;return;}
                var button=root as EditorButton;
                if(button!=null){button.AutoSize=false;button.Height=button.LayoutHeight>0?button.LayoutHeight:button.Detail.Length>0?95:38;return;}
                if(root is EditorToggle){root.Height=34;return;}
                var table=root as TableLayoutPanel;if(table==null)return;table.AutoSize=false;
                int inner=width-table.Padding.Horizontal;
                int count=Math.Max(table.RowCount,(table.Controls.Count+table.ColumnCount-1)/table.ColumnCount);int[] heights=new int[count];
                for(int i=0;i<table.Controls.Count;i++)
                {
                    Control c=table.Controls[i];int column=table.GetColumn(c);if(column<0)column=i%table.ColumnCount;int row=table.GetRow(c);if(row<0)row=i/table.ColumnCount;int cell=inner;
                    if(table.ColumnCount>1)cell=(int)(inner*table.ColumnStyles[column].Width/100);
                    FitColumns(c,cell-c.Margin.Horizontal);heights[row]=Math.Max(heights[row],c.Height+c.Margin.Vertical);
                }
                table.RowCount=count;while(table.RowStyles.Count<count)table.RowStyles.Add(new RowStyle());
                for(int i=0;i<count;i++){table.RowStyles[i].SizeType=SizeType.Absolute;table.RowStyles[i].Height=heights[i];}
                table.Height=heights.Sum()+table.Padding.Vertical;
            }
            finally{root.ResumeLayout(true);}
        }
        static TableLayoutPanel Card(string title){var p=EditorTheme.Stack(18);p.BackColor=EditorTheme.Panel;p.Margin=new Padding(0,0,0,18);EditorTheme.Add(p,EditorTheme.Label(title,10,EditorTheme.Amber,true));return p;}
        static void Note(TableLayoutPanel p,string text){var n=EditorTheme.Label(text,9,EditorTheme.Muted);n.Margin=new Padding(0,0,0,18);EditorTheme.Add(p,n);}
        TextBox TextInput(string value,Action<string> setter,bool multiline=false)
        {var c=new TextBox{Text=value??"",Multiline=multiline,Height=multiline?76:30,MaxLength=multiline?600:120};c.TextChanged+=(s,e)=>{setter(c.Text);Changed();};return c;}
        NumericUpDown Number(decimal value,decimal maximum,Action<decimal> setter,int decimals=0)
        {var n=new NumericUpDown{Maximum=maximum,DecimalPlaces=decimals,Value=Math.Min(maximum,Math.Max(0,value)),ThousandsSeparator=true};n.ValueChanged+=(s,e)=>{setter(n.Value);Changed();};return n;}
        EditorToggle Toggle(string title,int value,Action<int> setter)
        {var c=new EditorToggle{Text=title,Checked=value!=0,Height=34,Width=600,Margin=new Padding(0,0,0,12)};c.CheckedChanged+=(s,e)=>{setter(c.Checked?1:0);Changed();};return c;}
        EditorCombo Choose(IEnumerable<EndChoice> choices,string key,Action<string> setter,string empty=null)
        {
            var c=new EditorCombo();var list=choices.ToList();if(empty!=null)list.Insert(0,new EndChoice{Key="",Name=empty});
            if(!String.IsNullOrEmpty(key)&&!list.Any(x=>x.Key==key))list.Add(new EndChoice{Key=key,Name=key+" (missing)"});
            c.Items.AddRange(list.ToArray());c.SelectedIndex=list.FindIndex(x=>x.Key==(key??""));
            c.SelectedIndexChanged+=(s,e)=>{if(c.SelectedItem is EndChoice){setter(((EndChoice)c.SelectedItem).Key);Changed();}};return c;
        }
        EditorCombo Mode(string[] names,int index,Action<int> setter)
        {var c=new EditorCombo();c.Items.AddRange(names);c.SelectedIndex=Math.Max(0,Math.Min(names.Length-1,index));c.SelectedIndexChanged+=(s,e)=>{setter(c.SelectedIndex);Changed();};return c;}
        string LabelOf(string key){return SubjectOptions.Label(draft.Subjects,key);}
        int Slots(string key){var s=draft.Subjects.FirstOrDefault(x=>x.Key==key);return s==null?0:s.Slots;}
        string Unique(string prefix){string value=prefix;int i=2;while(draft.Rules.Any(x=>x.Name==value))value=prefix+" "+i++;return value;}
        EndRule NewRule(int type,string subject="",bool fallback=false)
        {var r=new EndRule{Type=type,Subject=subject,Fallback=fallback?1:0,Name=Unique(fallback?"Default timeout":type==0?"Objective result":type==1?LabelOf(subject)+" losses":"Conditional timeout"),Message=type==1?"Critical losses.":"Mission complete."};return r;}
        void AddRule(int type,bool fallback=false)
        {
            var rule=NewRule(type,"",fallback);draft.Rules.Add(rule);
            if(type==0){selected=rule;draft.ObjectivesEnabled=1;}else selectedTime=rule;
            Render();Changed();
        }
        void Remove(EndRule r){draft.Rules.Remove(r);if(selected==r)selected=null;if(selectedTime==r)selectedTime=null;Render();Changed();}
        void BuildOutcomes()
        {
            var host=bodies[0];var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,27));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,73));host.Controls.Add(layout);
            var left=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,0,16,0)};var right=new Panel{Dock=DockStyle.Fill};layout.Controls.Add(left);layout.Controls.Add(right);
            var list=ScrollBody(left);EditorTheme.Add(list,EditorTheme.Button("+ Add outcome rule",()=>AddRule(0)));
            var rules=draft.Rules.Where(x=>x.Type==0).ToList();if(selected==null||!rules.Contains(selected))selected=rules.FirstOrDefault();
            foreach(var r in rules){var row=new EditorButton{Text=r.Name,Detail=(r.Any==0?"All":"Any")+" · "+r.Requirements.Count+" conditions\n"+(r.Draw!=0?"Draw":LabelOf(r.Winner)),Navigation=true,Selected=r==selected,AutoSize=false,Height=95};row.Click+=(s,e)=>{selected=r;Render();};EditorTheme.Add(list,row);}
            var form=ScrollBody(right);EditorTheme.Add(form,Toggle("Enable objective outcomes",draft.ObjectivesEnabled,v=>draft.ObjectivesEnabled=v));
            if(selected!=null)RuleForm(form,selected);else{EditorTheme.Add(form,EditorTheme.Label("Several ways to win",15,null,true));Note(form,"Each rule can combine several objectives. Existing tasks from World Editor appear in the selectors; no technical IDs need to be typed.");EditorTheme.Add(form,EditorTheme.Button("+ Add outcome rule",()=>AddRule(0),true));}
            Note(form,"Simultaneous results: objectives → critical losses → time. Equal priority compares living participants, then freeze-end participants. Complete equality resolves to a draw.");
        }
        void RuleForm(TableLayoutPanel form,EndRule r)
        {
            EditorTheme.Add(form,EditorTheme.Field("Rule name",TextInput(r.Name,v=>r.Name=v)));
            EditorTheme.Add(form,Toggle("Enable this rule",r.Enabled,v=>r.Enabled=v));
            if(r.Fallback==0){Requirements(form,r);EditorTheme.Add(form,EditorTheme.Field("Combine requirements",Mode(new[]{"All conditions","Any condition"},r.Any,v=>r.Any=v)));}
            var result=Card("Final result");ResultFields(result,r);EditorTheme.Add(form,result);EditorTheme.Add(form,EditorTheme.Button("Remove rule",()=>Remove(r)));
        }
        void Requirements(TableLayoutPanel form,EndRule r)
        {
            var p=Card(r.Type==2?"Conditions at timeout":"Objective conditions");EditorTheme.Add(form,p);
            if(draft.Objectives.Count==0)Note(p,"No objectives exist yet. Add tasks in World Editor; the dedicated Objectives editor is the next implementation stage.");
            foreach(var req in r.Requirements)
            {
                var row=EditorTheme.Stack();row.Margin=new Padding(0,4,0,15);
                var objectives=Choose(draft.Objectives,req.Objective,k=>{req.Objective=k;var entry=draft.Objectives.FirstOrDefault(x=>x.Key==k);req.Fact=entry!=null&&entry.Control!=0?0:2;req.Subject=entry==null?"":entry.Owners.FirstOrDefault()??"";Render();},"Choose objective…");
                EditorTheme.Add(row,EditorTheme.Field("Objective",objectives));
                var known=draft.Objectives.FirstOrDefault(x=>x.Key==req.Objective);bool control=known!=null&&known.Control!=0;
                var fact=Mode(control?new[]{"Captured","Controls now"}:new[]{"Completed","Failed"},control?req.Fact:req.Fact-2,v=>{req.Fact=v+(control?0:2);Render();});
                var owners=SubjectOptions.Visible(draft.Subjects,new[]{req.Subject},draft.Subjects.Where(x=>known==null||known.Owners.Contains(x.Key)||req.Fact==1&&known.InitialOwner==x.Key).Select(x=>x.Key));
                EditorTheme.Add(row,EditorTheme.Pair(EditorTheme.Field("Required fact",fact),EditorTheme.Field("For this side",Choose(owners,req.Subject,k=>req.Subject=k,"Choose side…"))));
                var remove=EditorTheme.Button("Remove condition",()=>{r.Requirements.Remove(req);Render();Changed();});EditorTheme.Add(row,remove);EditorTheme.Add(p,row);
            }
            EditorTheme.Add(p,EditorTheme.Button("+ Add objective condition",()=>{r.Requirements.Add(new EndRequirement());Render();Changed();}));
        }
        void ResultFields(TableLayoutPanel p,EndRule r,Action beforeEdit=null)
        {
            Action mark=()=>{if(beforeEdit!=null)beforeEdit();};
            var options=SubjectOptions.Visible(draft.Subjects,new[]{r.Winner});options.Insert(0,new EndChoice{Key="draw",Name="Draw"});options.Insert(1,new EndChoice{Key="",Name="No assigned winner"});
            var winner=Choose(options,r.Draw!=0?"draw":r.Winner,k=>{mark();r.Draw=k=="draw"?1:0;r.Winner=k=="draw"?"":k;r.Losers.RemoveAll(loserKey=>draft.Subjects.Any(a=>a.Key==loserKey&&draft.Subjects.Any(b=>b.Key==r.Winner&&a.Members.Intersect(b.Members).Any())));if(r.Draw!=0)r.Losers.Clear();Render();});
            EditorTheme.Add(p,EditorTheme.Field("Result / winner",winner));
            if(r.Draw==0)
            {
                EditorTheme.Add(p,Toggle("Other sides lose automatically",r.AutoLosers,v=>{mark();r.AutoLosers=v;if(v!=0)r.Losers.Clear();Render();}));
            }
            if(r.Draw==0&&r.AutoLosers==0)
            {
                var losers=EditorTheme.Stack();EditorTheme.Add(losers,EditorTheme.Label("Explicit losing factions / sides / alliances",9));
                foreach(var subject in SubjectOptions.Visible(draft.Subjects,r.Losers).Where(x=>!draft.Subjects.Any(w=>w.Key==r.Winner&&w.Members.Intersect(x.Members).Any()))){var c=Toggle(subject.Name,r.Losers.Any(k=>SubjectOptions.Same(draft.Subjects,k,subject.Key))?1:0,v=>{mark();r.Losers.RemoveAll(k=>SubjectOptions.Same(draft.Subjects,k,subject.Key));if(v!=0)r.Losers.Add(subject.Key);});EditorTheme.Add(losers,c);}EditorTheme.Add(p,losers);
            }
            EditorTheme.Add(p,EditorTheme.Field("Result message",TextInput(r.Message,v=>{mark();r.Message=v;},true),"Uses the shared result message profile. Ordinary task notifications stay separate."));
            Note(p,"Display and timing: Mission settings → Mission ending. This text also appears on Debriefing.");
        }
        void BuildLosses()
        {
            var form=ScrollBody(bodies[1]);Note(form,"Preview uses current available slots. Runtime uses actual participants alive at the end of freeze. Unconscious participants remain alive; an empty side never triggers a result.");
            var master=Toggle("Enable critical loss outcomes",draft.LossesEnabled,v=>draft.LossesEnabled=v);EditorTheme.Add(form,master);
            var rows=draft.Rules.Where(x=>x.Type==1).ToList();foreach(var subject in SubjectOptions.Visible(draft.Subjects,rows.Select(x=>x.Subject)))if(!rows.Any(x=>SubjectOptions.Same(draft.Subjects,x.Subject,subject.Key))){EndRule r;if(!placeholderLosses.TryGetValue(subject.Key,out r)){r=NewRule(1,subject.Key);r.Enabled=0;placeholderLosses.Add(subject.Key,r);}rows.Add(r);}
            foreach(var r in rows)
            {
                var card=Card(LabelOf(r.Subject)+" · "+Slots(r.Subject)+" available slots");EditorTheme.Add(form,card);
                Action ensure=()=>{if(!draft.Rules.Contains(r))draft.Rules.Add(r);};
                EditorTheme.Add(card,Toggle("Enable this loss rule",r.Enabled,v=>{ensure();r.Enabled=v;if(v!=0)master.Checked=true;}));
                var estimate=EditorTheme.Label(r.Estimate(Slots(r.Subject)),10,EditorTheme.Green);estimate.Padding=new Padding(12);estimate.BackColor=Color.FromArgb(37,47,48);estimate.Margin=new Padding(0,0,0,16);
                var mode=Mode(new[]{"Lost percentage ≥","Alive count ≤","Lost count ≥","Alive percentage ≤"},r.ThresholdMode,v=>{ensure();r.ThresholdMode=v;estimate.Text=r.Estimate(Slots(r.Subject));});
                var amount=Number(r.Threshold,4096,v=>{ensure();r.Threshold=v;estimate.Text=r.Estimate(Slots(r.Subject));},2);
                EditorTheme.Add(card,EditorTheme.Pair(EditorTheme.Field("Threshold type",mode),EditorTheme.Field("Threshold",amount)));EditorTheme.Add(card,estimate);
                var results=SubjectOptions.Visible(draft.Subjects,new[]{r.Winner});results.Insert(0,new EndChoice{Key="draw",Name="Draw"});results.Insert(1,new EndChoice{Key="",Name="No assigned winner"});
                var winner=Choose(results,r.Draw!=0?"draw":r.Winner,k=>{ensure();r.Draw=k=="draw"?1:0;r.Winner=k=="draw"?"":k;r.Losers.RemoveAll(loserKey=>draft.Subjects.Any(a=>a.Key==loserKey&&draft.Subjects.Any(b=>b.Key==r.Winner&&a.Members.Intersect(b.Members).Any())));if(r.Draw!=0)r.Losers.Clear();if(expanded.Contains(r))Render();});
                EditorTheme.Add(card,EditorTheme.Pair(EditorTheme.Field("Result / winner",winner),EditorTheme.Field("Result message",TextInput(r.Message,v=>{ensure();r.Message=v;}))));
                EditorTheme.Add(card,EditorTheme.Button(expanded.Contains(r)?"Less result options":"More result options",()=>{if(!expanded.Add(r))expanded.Remove(r);Render();}));
                if(expanded.Contains(r))
                {
                    var extra=EditorTheme.Stack();ResultFields(extra,r,ensure);EditorTheme.Add(card,extra);
                }
                // Preserve and expose advanced result fields for authored multi-party rules.
                if(r.Index>=0||draft.Rules.Contains(r))
                {if(r.Losers.Count>0)Note(card,"Explicit losing subjects: "+String.Join(", ",r.Losers.Select(LabelOf)));EditorTheme.Add(card,EditorTheme.Button("Remove loss rule",()=>{placeholderLosses.Remove(r.Subject);Remove(r);}));}
            }
            if(rows.Count==0)Note(form,"Add mission factions first. Each faction and pooled alliance will have its own loss card.");
        }
        void BuildTime()
        {
            var form=ScrollBody(bodies[2]);EditorTheme.Add(form,EditorTheme.Pair(Toggle("Enable combat time limit",draft.TimeEnabled,v=>draft.TimeEnabled=v),EditorTheme.Field("Combat time (minutes)",Number(draft.TimeSeconds/60m,10080,v=>draft.TimeSeconds=Decimal.Round(v*60,3),5))));
            Note(form,"Starts after freeze. Technical pause stops the clock.");
            var fallback=draft.Rules.FirstOrDefault(x=>x.Type==2&&x.Fallback!=0);
            var defaults=Card("Default timeout result");EditorTheme.Add(form,defaults);
            if(fallback==null){Note(defaults,"Without an explicit default, timeout ends without an assigned winner. Choose a result and message here.");EditorTheme.Add(defaults,EditorTheme.Button("Configure default timeout result",()=>AddRule(2,true),true));}
            else {EditorTheme.Add(defaults,Toggle("Enable default timeout result",fallback.Enabled,v=>fallback.Enabled=v));ResultFields(defaults,fallback);EditorTheme.Add(defaults,EditorTheme.Button("Remove default timeout result",()=>Remove(fallback)));}
            EditorTheme.Add(form,EditorTheme.Label("Conditional timeout results",14,null,true));Note(form,"Matching conditions replace the default timeout result. Simultaneous matching results use the same priorities and tie rules.");
            EditorTheme.Add(form,EditorTheme.Button("+ Add condition",()=>AddRule(2)));
            var conditions=draft.Rules.Where(x=>x.Type==2&&x.Fallback==0).ToList();if(selectedTime==null||!conditions.Contains(selectedTime))selectedTime=conditions.FirstOrDefault();
            foreach(var r in conditions){var button=new EditorButton{Text=r.Name,Selected=r==selectedTime,Height=38,AutoSize=false};button.Click+=(s,e)=>{selectedTime=r;Render();};EditorTheme.Add(form,button);}
            if(selectedTime!=null)RuleForm(form,selectedTime);
        }
    }
}
