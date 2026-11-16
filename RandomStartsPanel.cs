using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    // Edits the map's one local draft. The containing map owns Apply and Save.
    public sealed class RandomStartsPanel : UserControl
    {
        sealed class Choice
        {
            public StartBundle Bundle;
            public StartObject Object;
            public string Name {get{return Bundle!=null?Bundle.Name:Object.Name;}}
            public string Kind {get{return Bundle!=null&&Bundle.Members.Count>1?"Together":new[]{"Group","Vehicle","Faction","Object"}[Bundle!=null?Bundle.Members[0].Kind:Object.Kind];}}
            public string Key {get{return Bundle!=null?Bundle.Id:Object.Identity;}}
        }
        readonly MapCanvas canvas;
        readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=16000000};
        readonly Stack<string> undo=new Stack<string>(),redo=new Stack<string>();
        readonly HashSet<string> selected=new HashSet<string>();
        readonly EditorCombo method=new EditorCombo{Dock=DockStyle.Fill};
        readonly EditorCombo kind=new EditorCombo{Width=95};
        readonly EditorToggle enabled=new EditorToggle{Text="Use random starts",AutoSize=true};
        readonly NumericUpDown seed=new NumericUpDown{Minimum=0,Maximum=2147483646,Width=76};
        readonly PropertyGrid placement=new PropertyGrid{ToolbarVisible=false,HelpVisible=false,PropertySort=PropertySort.Categorized,BackColor=EditorTheme.Background,ViewBackColor=EditorTheme.Input,ViewForeColor=EditorTheme.Text,CategoryForeColor=EditorTheme.Muted,LineColor=EditorTheme.Panel};
        readonly TextBox search=new TextBox{Dock=DockStyle.Fill};
        readonly ListView assignments=new ListView{View=View.Details,CheckBoxes=true,FullRowSelect=true,MultiSelect=true,HideSelection=false,Dock=DockStyle.Fill,BackColor=EditorTheme.Input,ForeColor=EditorTheme.Text,BorderStyle=BorderStyle.None};
        readonly Label selection=EditorTheme.Label("Select a start point",10,null,true);
        readonly Label explanation=EditorTheme.Label("",9,EditorTheme.Muted);
        readonly TableLayoutPanel frame=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=1,Padding=new Padding(5)};
        MapReply view;
        RandomStartData draft=new RandomStartData();
        string saved,last;
        bool loading,adding,dragging,rotating;
        PointF dragOrigin;
        Dictionary<string,MapPoint> dragPoints;
        Dictionary<string,string> preview;
        public bool Active {get;set;}
        public bool Dirty {get{return last!=saved;}}
        public bool CanUndo {get{return undo.Count>0;}}
        public bool CanRedo {get{return redo.Count>0;}}
        public bool Supported {get{return view!=null&&view.RandomStartsVersion>=3;}}
        public IEnumerable<string> SelectedIds {get{return selected;}}
        public Func<bool> CanActivate;
        public event Action Activated,SelectionChanged,Changed;
        public event Action<string> Message;
        public RandomStartsPanel(MapCanvas map)
        {
            canvas=map;Dock=DockStyle.Fill;AutoScroll=true;BackColor=EditorTheme.Background;
            method.Items.AddRange(new object[]{"Independent choice","Distribution without repeats"});
            Add(EditorTheme.Label("Random starts · whole mission",10,null,true),SizeType.AutoSize,0);
            Add(method,SizeType.AutoSize,0);
            var options=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2};options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,118));options.Controls.Add(enabled,0,0);
            var seedBox=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=Padding.Empty};var seedLabel=EditorTheme.Label("Seed",9);seedLabel.Margin=new Padding(0,4,2,0);seedBox.Controls.Add(seedLabel);seedBox.Controls.Add(seed);options.Controls.Add(seedBox,1,0);Add(options,SizeType.AutoSize,0);
            Add(selection,SizeType.AutoSize,0);Add(placement,SizeType.Absolute,145);
            Add(EditorTheme.Label("Who can start at the selected points",9,null,true),SizeType.AutoSize,0);
            var filter=new TableLayoutPanel{ColumnCount=2,Dock=DockStyle.Fill,AutoSize=true};filter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));filter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
            kind.Items.AddRange(new object[]{"All","Faction","Group","Vehicle","Object","Together"});kind.SelectedIndex=0;kind.Dock=DockStyle.Fill;filter.Controls.Add(search,0,0);filter.Controls.Add(kind,1,0);Add(filter,SizeType.AutoSize,0);
            assignments.Columns.Add("Group / object",190);assignments.Columns.Add("Points",55);Add(assignments,SizeType.Percent,100);
            var combine=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};combine.Controls.Add(EditorTheme.Button("Move together",Combine));combine.Controls.Add(EditorTheme.Button("Separate",Separate));Add(combine,SizeType.AutoSize,0);
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};actions.Controls.Add(EditorTheme.Button("Remove assignment",RemoveAssignments));actions.Controls.Add(EditorTheme.Button("Delete points",DeleteSelected));Add(actions,SizeType.AutoSize,0);
            var sample=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};sample.Controls.Add(EditorTheme.Button("Preview",Preview));sample.Controls.Add(EditorTheme.Button("Clear preview",()=>{preview=null;canvas.Invalidate();}));Add(sample,SizeType.AutoSize,0);
            explanation.MaximumSize=new Size(310,0);Add(explanation,SizeType.AutoSize,0);Controls.Add(frame);
            Resize+=(s,e)=>{frame.Height=Math.Max(710,ClientSize.Height);assignments.Columns[0].Width=Math.Max(130,assignments.ClientSize.Width-75);};
            method.SelectedIndexChanged+=(s,e)=>{if(!loading&&method.SelectedIndex>=0){draft.UseMissionMode(method.SelectedIndex);Commit();}};
            enabled.CheckedChanged+=(s,e)=>{if(!loading){draft.Enabled=enabled.Checked?1:0;Commit();}};
            seed.ValueChanged+=(s,e)=>{if(!loading){draft.Seed=(int)seed.Value;Commit();}};
            placement.PropertyValueChanged+=(s,e)=>{Commit();FillSelection();};search.TextChanged+=(s,e)=>FillChoices();kind.SelectedIndexChanged+=(s,e)=>FillChoices();
            assignments.ItemChecked+=(s,e)=>{if(!loading){Assign((Choice)e.Item.Tag,e.Item.Checked);if(IsHandleCreated)BeginInvoke((Action)FillChoices);}};
            assignments.SelectedIndexChanged+=(s,e)=>canvas.Invalidate();
            canvas.OverlayPaint+=Draw;canvas.OverlayMouseDown=MouseDownOnMap;canvas.OverlayMouseMove=MouseMoveOnMap;canvas.OverlayMouseUp=MouseUpOnMap;canvas.OverlayKeyDown=KeyOnMap;
            canvas.MouseCaptureChanged+=(s,e)=>{if(!canvas.Capture&&(dragging||rotating)){dragging=rotating=false;Commit();}};
            saved=last=json.Serialize(draft);frame.Height=710;
        }
        void Add(Control control,SizeType type,float height){int row=frame.RowCount++;frame.RowStyles.Add(new RowStyle(type,height));control.Dock=DockStyle.Fill;frame.Controls.Add(control,0,row);}
        void Tell(string text){if(Message!=null)Message(text);}
        public void SetScene(MapReply scene,bool reset)
        {
            bool newWorld=view==null||view.WorldPath!=scene.WorldPath;view=scene;
            if(reset||newWorld||!Dirty)
            {
                draft=(scene.RandomStarts??new RandomStartData()).Copy();string original=json.Serialize(draft);
                if(draft.Version==1)
                {
                    int mode=draft.Bundles.Count==0||draft.Bundles.All(b=>b.Mode==0)?0:draft.Bundles.All(b=>b.Mode==1)?1:-1;
                    draft.UseMissionMode(mode);
                }
                bool migrate=draft.WorldPoints==0&&draft.Points.Count>0;draft.WorldPoints=1;
                last=json.Serialize(draft);saved=migrate||(scene.RandomStarts!=null&&scene.RandomStarts.Version==1&&draft.Bundles.Count>0)?original:last;
                undo.Clear();redo.Clear();preview=null;
            }
            selected.RemoveWhere(id=>!draft.Points.Any(p=>p.Id==id));Fill();
        }
        public RandomStartData Data {get{return draft;}}
        public string ValidateDraft(){if(!Supported)return "Update the addon and reopen Workbench to edit random starts.";if(!String.IsNullOrEmpty(view.RandomStartsProblem))return view.RandomStartsProblem;return draft.Validate(view.StartObjects??new List<StartObject>());}
        public IEnumerable<MapRow> Rows(){return draft.Points.Select(p=>new MapRow{Key="start:"+p.Id,Kind="Random start",Title=p.Name,Name=p.Name,Points=new List<MapPoint>{new MapPoint{X=p.X,Y=p.Y,Z=p.Z}}});}
        public bool Select(IEnumerable<string> ids)
        {
            if(CanActivate!=null&&!CanActivate())return false;
            var next=ids.ToArray();selected.Clear();foreach(string id in next)if(draft.Points.Any(p=>p.Id==id))selected.Add(id);
            Active=true;if(Activated!=null)Activated();FillSelection();if(SelectionChanged!=null)SelectionChanged();Tell("Select start points, then tick what may start there in the sidebar. Apply saves all random-start edits together.");canvas.Invalidate();return true;
        }
        public void StartAdding()
        {
            if(!Supported){Tell("Update the addon and reopen Workbench to add random starts.");return;}
            if(!String.IsNullOrEmpty(view.RandomStartsProblem)){Tell(view.RandomStartsProblem);return;}
            if(!Select(new string[0]))return;adding=true;canvas.Focus();Tell("Click to place start points. Esc stops placing. Select points, then tick what may start there; Apply saves them together.");
        }
        public void Deactivate(){Active=false;adding=false;}
        public void FocusSelection()
        {
            var points=draft.Points.Where(p=>selected.Contains(p.Id)).ToList();if(points.Count==0)return;
            canvas.Projection.Fit(points.Min(p=>p.X)-60,points.Min(p=>p.Z)-60,points.Max(p=>p.X)+60,points.Max(p=>p.Z)+60);canvas.Invalidate();
        }
        void Fill()
        {
            loading=true;method.SelectedIndex=draft.SelectionMode;enabled.Checked=draft.Enabled!=0;seed.Value=draft.Seed;loading=false;Enabled=Supported;FillSelection();
        }
        void FillSelection()
        {
            var points=draft.Points.Where(p=>selected.Contains(p.Id)).ToList();
            selection.Text=points.Count==0?"Select or place a start point":points.Count==1?points[0].Name:points.Count+" start points selected";
            placement.SelectedObjects=points.Select(p=>(object)new StartPointProperties(p)).ToArray();
            placement.Enabled=points.All(p=>p.ReadOnly==0);
            explanation.Text=!String.IsNullOrEmpty(view==null?null:view.RandomStartsProblem)?view.RandomStartsProblem:draft.SelectionMode<0?"Older per-set rules: choose one method for the mission before Apply. Existing points and assignments are retained.":"Ctrl-click points: select several. Tick an item to allow it at all selected points. Linked vehicles always follow their group. Seed 0 gives a new draw each mission.";
            FillChoices();
        }
        IEnumerable<Choice> Choices()
        {
            var catalog=view==null?new List<StartObject>():view.StartObjects??new List<StartObject>();
            foreach(var b in draft.Bundles)yield return new Choice{Bundle=b};
            var used=new HashSet<string>(draft.Bundles.SelectMany(b=>RandomStartData.Expand(b,catalog)).Select(o=>o.Identity));
            foreach(var group in catalog.Where(o=>o.Kind==0&&!String.IsNullOrEmpty(o.EngineKey)).GroupBy(o=>o.EngineKey))
            {
                if(draft.Bundles.Any(b=>b.Members.Any(m=>m.Kind==2&&m.Key==group.Key)))continue;
                var faction=view.Forces==null?null:view.Forces.Factions.FirstOrDefault(f=>f.Key==group.Key);
                yield return new Choice{Object=new StartObject{Kind=2,Key=group.Key,Name=faction==null?group.Key:faction.ToString()}};
            }
            foreach(var o in catalog)if(!used.Contains(o.Identity))yield return new Choice{Object=o};
        }
        void FillChoices()
        {
            if(loading)return;loading=true;var highlighted=new HashSet<string>(assignments.SelectedItems.Cast<ListViewItem>().Select(i=>((Choice)i.Tag).Key));assignments.BeginUpdate();assignments.Items.Clear();
            foreach(var c in Choices().Where(c=>(kind.SelectedIndex<=0||c.Kind==(string)kind.SelectedItem)&&c.Name.IndexOf(search.Text,StringComparison.CurrentCultureIgnoreCase)>=0).OrderBy(c=>c.Kind=="Faction"?0:c.Kind=="Together"?1:2).ThenBy(c=>c.Name))
            {
                int count=c.Bundle==null?0:c.Bundle.Points.Count(selected.Contains);var item=new ListViewItem(c.Name){Tag=c,Checked=selected.Count>0&&count==selected.Count,Selected=highlighted.Contains(c.Key)};
                item.SubItems.Add(count+" / "+selected.Count);item.ToolTipText=c.Kind+" · "+c.Name;assignments.Items.Add(item);
            }
            assignments.ShowItemToolTips=true;assignments.Enabled=selected.Count>0;assignments.EndUpdate();loading=false;
        }
        StartBundle CreateBundle(IEnumerable<StartMember> members,string name)
        {
            var b=new StartBundle{Name=name,Members=members.GroupBy(m=>m.Identity).Select(g=>g.First()).ToList(),Yaw=0};
            // A group already owns its transport, and a faction owns its groups.
            // Keep these as implicit members so Separate cannot assign them twice.
            foreach(var member in b.Members.ToList().AsEnumerable().Reverse())
            {
                var alone=RandomStartData.Expand(new StartBundle{Members=new List<StartMember>{member}},view.StartObjects);
                var rest=RandomStartData.Expand(new StartBundle{Members=b.Members.Where(m=>m!=member).ToList()},view.StartObjects);
                if(alone.Count>0&&alone.All(o=>rest.Any(other=>other.EntityId==o.EntityId)))b.Members.Remove(member);
            }
            var expanded=RandomStartData.Expand(b,view.StartObjects);if(expanded.Count>0){b.X=expanded.Average(o=>o.X);b.Y=expanded.Average(o=>o.Y);b.Z=expanded.Average(o=>o.Z);}
            return b;
        }
        void Assign(Choice choice,bool allow)
        {
            if(selected.Count==0||!Supported)return;var b=choice.Bundle;
            if(b==null&&allow)
            {
                b=CreateBundle(new[]{new StartMember{Kind=choice.Object.Kind,Key=choice.Object.Key}},choice.Name);
                var occupied=new HashSet<string>(draft.Bundles.SelectMany(x=>RandomStartData.Expand(x,view.StartObjects)).Select(o=>o.EntityId));
                var shared=RandomStartData.Expand(b,view.StartObjects).FirstOrDefault(o=>occupied.Contains(o.EntityId));
                if(shared!=null){Tell(shared.Name+" already has a separate assignment. Remove that assignment before assigning the whole faction.");return;}
                draft.Bundles.Add(b);
            }
            if(b==null)return;
            foreach(string id in selected){if(allow){if(!b.Points.Contains(id))b.Points.Add(id);}else b.Points.Remove(id);}
            if(b.Points.Count==0)draft.Bundles.Remove(b);
            if(allow)draft.Enabled=1;else if(draft.Bundles.Count==0)draft.Enabled=0;
            Commit();loading=true;enabled.Checked=draft.Enabled!=0;loading=false;
        }
        void Combine()
        {
            var choices=assignments.SelectedItems.Cast<ListViewItem>().Select(i=>(Choice)i.Tag).ToList();
            if(choices.Count<2){Tell("Select two or more rows with Ctrl, then Move together. They will use one point and keep their arrangement.");return;}
            var existing=choices.Where(c=>c.Bundle!=null).Select(c=>c.Bundle).ToList();
            var allowed=existing.Count==0?selected.ToList():existing[0].Points.ToList();
            if(existing.Any(b=>!new HashSet<string>(b.Points).SetEquals(allowed))||choices.Any(c=>c.Bundle==null)&&!new HashSet<string>(allowed).SetEquals(selected))
            {Tell("Choose the same allowed points for these items before combining them.");return;}
            var members=choices.SelectMany(c=>c.Bundle!=null?c.Bundle.Members:new[]{new StartMember{Kind=c.Object.Kind,Key=c.Object.Key}}.ToList()).GroupBy(m=>m.Identity).Select(g=>g.First()).ToList();
            var together=CreateBundle(members,String.Join(" + ",choices.Select(c=>c.Name)));if(together.Name.Length>120)together.Name=together.Name.Substring(0,117)+"...";
            together.Points=allowed;foreach(var b in existing)draft.Bundles.Remove(b);draft.Bundles.Add(together);draft.Enabled=1;Commit();Fill();
        }
        void Separate()
        {
            foreach(var choice in assignments.SelectedItems.Cast<ListViewItem>().Select(i=>(Choice)i.Tag).ToList())
            {
                var b=choice.Bundle;if(b==null||b.Members.Count<2)continue;draft.Bundles.Remove(b);
                foreach(var m in b.Members){var o=view.StartObjects.FirstOrDefault(x=>x.Identity==m.Identity);var item=CreateBundle(new[]{m},o==null?m.Key:o.Name);item.Points=b.Points.ToList();draft.Bundles.Add(item);}
            }
            Commit();FillChoices();
        }
        void RemoveAssignments(){foreach(var c in assignments.SelectedItems.Cast<ListViewItem>().Select(i=>(Choice)i.Tag).ToList())Assign(c,false);FillChoices();}
        public void DeleteSelected()
        {
            if(draft.Points.Any(p=>selected.Contains(p.Id)&&p.ReadOnly!=0)){Tell("A selected point is inherited or locked in World Editor.");return;}
            if(selected.Count==0)return;draft.Points.RemoveAll(p=>selected.Contains(p.Id));foreach(var b in draft.Bundles)b.Points.RemoveAll(selected.Contains);draft.Bundles.RemoveAll(b=>b.Points.Count==0);if(draft.Bundles.Count==0)draft.Enabled=0;
            selected.Clear();Commit();Fill();if(SelectionChanged!=null)SelectionChanged();
        }
        void Commit()
        {
            string now=json.Serialize(draft);if(now==last)return;undo.Push(last);redo.Clear();last=now;preview=null;canvas.Invalidate();if(Changed!=null)Changed();
        }
        public void Undo(){Restore(undo,redo);}
        public void Redo(){Restore(redo,undo);}
        void Restore(Stack<string> source,Stack<string> target)
        {
            if(source.Count==0)return;target.Push(last);last=source.Pop();draft=json.Deserialize<RandomStartData>(last);selected.RemoveWhere(id=>!draft.Points.Any(p=>p.Id==id));preview=null;Fill();canvas.Invalidate();if(Changed!=null)Changed();
        }
        public void Discard(){SetScene(view,true);saved=last;undo.Clear();redo.Clear();adding=false;canvas.Invalidate();if(Changed!=null)Changed();}
        static double Distance(PointF a,PointF b){return Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));}
        PointF Tip(StartPoint p){var at=canvas.Projection.Screen(p.X,p.Z);double angle=p.Yaw*Math.PI/180;return new PointF(at.X+(float)Math.Sin(angle)*31,at.Y-(float)Math.Cos(angle)*31);}
        bool MouseDownOnMap(MouseEventArgs e)
        {
            if(e.Button!=MouseButtons.Left||!Supported||canvas.PlayerPreview||canvas.Drawing)return false;
            var world=canvas.Projection.World(e.Location);
            if(adding)
            {
                if(draft.Points.Count>=256){Tell("Maximum 256 start points.");return true;}
                int number=1;while(draft.Points.Any(p=>p.Name=="Start "+number))number++;
                var point=new StartPoint{Name="Start "+number,X=world.X,Z=world.Z};draft.Points.Add(point);Commit();Select(new[]{point.Id});return true;
            }
            var current=draft.Points.FirstOrDefault(p=>selected.Contains(p.Id));
            if(Active&&selected.Count==1&&current!=null&&current.ReadOnly==0&&Distance(Tip(current),e.Location)<11){rotating=true;canvas.Capture=true;return true;}
            var hit=draft.Points.FirstOrDefault(p=>Distance(canvas.Projection.Screen(p.X,p.Z),e.Location)<12);if(hit==null)return false;
            var ids=selected.ToList();bool control=(ModifierKeys&Keys.Control)!=0;
            if(control){if(!ids.Remove(hit.Id))ids.Add(hit.Id);}else if(!ids.Contains(hit.Id)){ids.Clear();ids.Add(hit.Id);}
            if(!Select(ids))return true;
            if(!control&&draft.Points.All(p=>!selected.Contains(p.Id)||p.ReadOnly==0)){dragging=true;dragOrigin=e.Location;dragPoints=draft.Points.Where(p=>selected.Contains(p.Id)).ToDictionary(p=>p.Id,p=>new MapPoint{X=p.X,Z=p.Z});canvas.Capture=true;}
            return true;
        }
        bool MouseMoveOnMap(MouseEventArgs e)
        {
            if(!dragging&&!rotating)return false;
            if(rotating){var p=draft.Points.First(x=>selected.Contains(x.Id));var at=canvas.Projection.World(e.Location);p.Yaw=(Math.Atan2(at.X-p.X,at.Z-p.Z)*180/Math.PI+360)%360;}
            else{double dx=(e.X-dragOrigin.X)/canvas.Projection.PixelsPerMetre,dz=-(e.Y-dragOrigin.Y)/canvas.Projection.PixelsPerMetre;foreach(var p in draft.Points.Where(p=>selected.Contains(p.Id))){p.X=dragPoints[p.Id].X+dx;p.Z=dragPoints[p.Id].Z+dz;}}
            preview=null;canvas.Invalidate();return true;
        }
        bool MouseUpOnMap(MouseEventArgs e){if(!dragging&&!rotating)return false;dragging=rotating=false;Commit();placement.Refresh();return true;}
        bool KeyOnMap(KeyEventArgs e)
        {
            if(!Active)return false;
            if(e.KeyCode==Keys.Escape){adding=false;if(dragging||rotating){draft=json.Deserialize<RandomStartData>(last);dragging=rotating=false;Fill();}canvas.Capture=false;canvas.Invalidate();return true;}
            if(e.KeyCode==Keys.Delete){DeleteSelected();return true;}return false;
        }
        void Preview(){string error=ValidateDraft();if(error!=null){Tell(error);return;}int drawSeed=draft.Seed==0?new Random().Next(1,2147483646):draft.Seed;preview=draft.Sample(drawSeed,out error);Tell(error??"Preview of one draw. Original objects remain in place in Workbench.");canvas.Invalidate();}
        void Draw(Graphics g,MapProjection projection)
        {
            if(canvas.PlayerPreview)return;
            foreach(var p in draft.Points)
            {
                var at=projection.Screen(p.X,p.Z);bool chosen=selected.Contains(p.Id)&&Active;Color color=chosen?Color.White:EditorTheme.Amber;
                using(var pen=new Pen(color,chosen?3:2)){g.DrawEllipse(pen,at.X-8,at.Y-8,16,16);pen.EndCap=LineCap.ArrowAnchor;g.DrawLine(pen,at,Tip(p));}
                Label(g,p.Name,at.X+12,at.Y+10,color);
            }
            if(!Active&&preview==null)return;
            var focus=assignments.SelectedItems.Cast<ListViewItem>().Select(i=>(Choice)i.Tag).Where(c=>c.Bundle!=null).Select(c=>c.Bundle).ToList();
            if(focus.Count==0)focus=draft.Bundles.Where(b=>b.Points.Any(selected.Contains)).ToList();
            foreach(var b in preview==null?focus:draft.Bundles)
            {
                var members=RandomStartData.Expand(b,view.StartObjects);foreach(var o in members){var at=projection.Screen(o.X,o.Z);g.DrawRectangle(Pens.Cyan,at.X-7,at.Y-7,14,14);}
                var destinations=preview==null?draft.Points.Where(p=>selected.Contains(p.Id)&&b.Points.Contains(p.Id)):draft.Points.Where(p=>preview.ContainsKey(b.Id)&&preview[b.Id]==p.Id);
                foreach(var p in destinations)using(var pen=new Pen(Color.FromArgb(135,EditorTheme.Amber),1){DashStyle=DashStyle.Dash})
                foreach(var o in members){var pose=RandomStartData.Transform(b,p,o);var at=projection.Screen(pose.X,pose.Z);g.DrawLine(pen,projection.Screen(o.X,o.Z),at);g.FillRectangle(Brushes.Gold,at.X-4,at.Y-4,8,8);}
            }
        }
        void Label(Graphics g,string text,float x,float y,Color color){var size=g.MeasureString(text,Font);using(var background=new SolidBrush(Color.FromArgb(200,16,22,25)))g.FillRectangle(background,x-2,y-1,size.Width+4,size.Height+2);using(var ink=new SolidBrush(color))g.DrawString(text,Font,ink,x,y);}
    }
}
