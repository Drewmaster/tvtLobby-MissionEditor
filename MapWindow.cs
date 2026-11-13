using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class MapWindow : Form
    {
        private readonly Session session;
        private readonly MapCanvas canvas=new MapCanvas();
        private readonly ListBox objects=new ListBox();
        private readonly MapAppearanceEditor properties=new MapAppearanceEditor();
        private readonly TextBox entityName=new TextBox {Dock=DockStyle.Top};
        private readonly DataGridView vertices=new DataGridView();
        private readonly CheckBox ground=new EditorToggle {Text="Project vertices onto terrain on Apply",Checked=true,AutoSize=true};
        private readonly Label status=new EditorLabel(), coordinates=new EditorLabel(), description=new EditorLabel();
        private readonly Label layerName=new EditorLabel{AutoSize=true,MaximumSize=new Size(310,0)};
        private readonly Button moveLayer=new EditorButton{AutoSize=true,Visible=false};
        private readonly FlowLayoutPanel tools=new FlowLayoutPanel();
        private readonly TableLayoutPanel body=new TableLayoutPanel();
        private readonly Timer imageTimer=new Timer {Interval=2000};
        private MapReply view;
        private MapAssets assets;
        private MapTopology topology;
        private string topologyResource,topologyStatus="";
        private FreezeZoneSettings freezeDraft;
        private FreezeZoneSettings savedFreezeSettings;
        private bool freezeDirty;
        private readonly TextBox search=new TextBox();
        private readonly Button orderUp=new EditorButton{Text="↑ Up",AutoSize=true},orderDown=new EditorButton{Text="↓ Down",AutoSize=true};
        private Point dragOrigin;
        private string dragKey;
        private int dropIndex=-1;
        private bool dropAfter;
        private List<string> pendingOrder;
        private readonly EditorCombo factionFilter=new EditorCombo();
        private bool busy,dirty,loading,connectionFailed;
        private int imageRetries;
        public MapWindow(Session value)
        {
            session=value;Text="tvtLobby | Mission Editor — Map";Size=new Size(1440,900);MinimumSize=new Size(1060,680);
            StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",9);BackColor=Color.FromArgb(28,32,34);ForeColor=Color.Gainsboro;
            AutoScaleMode=AutoScaleMode.Dpi;
            var frame=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(10)};
            frame.RowCount=4;frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
            tools.Dock=DockStyle.Fill;tools.AutoSize=true;
            Button("Refresh",()=>Run("snapshot"));Button("Apply",ApplyDrafts);
            Button("Discard draft",()=>{if(DiscardAllowed())SelectMapRow(canvas.Draft==null?"":canvas.Draft.Key);return Task.FromResult(0);});
            Button("Undo",()=>Run("undo"));Button("Redo",()=>Run("redo"));Button("Save world",()=>Run("save"));
            Button("Select in World Editor",()=>Run("select"));Button("Fit map",()=>{canvas.Fit();return Task.FromResult(0);});
            Button("Focus object",()=>{canvas.FocusDraft();return Task.FromResult(0);});
            frame.Controls.Add(tools,0,0);
            var layers=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
            foreach(string title in new[]{"Map details","Groups","Vehicles","Automatic freeze zones","Player preview"}){string key=title;var check=new EditorToggle{Text=title,Checked=title!="Player preview"&&title!="Automatic freeze zones",AutoSize=true};check.CheckedChanged+=(s,e)=>{if(key=="Map details")canvas.ShowDetails=check.Checked;if(key=="Groups")canvas.ShowGroups=check.Checked;if(key=="Vehicles")canvas.ShowVehicles=check.Checked;if(key=="Automatic freeze zones")canvas.ShowAutomatic=check.Checked;if(key=="Player preview")canvas.PlayerPreview=check.Checked;canvas.Invalidate();};layers.Controls.Add(check);}
            factionFilter.Width=150;factionFilter.SelectedIndexChanged+=(s,e)=>{canvas.FactionFilter=factionFilter.SelectedIndex<=0?"":((EditorFaction)factionFilter.SelectedItem).Key;canvas.Invalidate();};layers.Controls.Add(factionFilter);
            var phase=new EditorCombo{Width=120};phase.Items.AddRange(new object[]{"Waiting","Lobby","Pre-brief","Briefing","Freeze","Game","Debriefing"});phase.SelectedIndex=3;phase.SelectedIndexChanged+=(s,e)=>{canvas.PreviewPhase=new[]{2,4,8,16,32,32,64}[phase.SelectedIndex];canvas.PreviewFreeze=phase.SelectedIndex==4;canvas.Invalidate();};layers.Controls.Add(phase);frame.Controls.Add(layers,0,1);
            body.Dock=DockStyle.Fill;body.ColumnCount=3;body.RowCount=1;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330));
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));left.RowStyles.Add(new RowStyle(SizeType.AutoSize));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.AutoSize));left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var create=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
            foreach(string kind in new[]{"Icon","Line","Area","Zone","FreezeCircle","Boundary"})
            {string type=kind;var add=new EditorButton{Text="+ "+(kind=="FreezeCircle"?"Freeze circle":kind=="Zone"?"Zone / freeze contour":kind=="Boundary"?"Battlefield boundary":kind)};add.Click+=(s,e)=>New(type);create.Controls.Add(add);}
            objects.Dock=DockStyle.Fill;objects.BackColor=Color.FromArgb(20,25,27);objects.ForeColor=Color.Gainsboro;objects.IntegralHeight=false;
            objects.DrawMode=DrawMode.OwnerDrawFixed;objects.ItemHeight=34;
            objects.DrawItem+=(s,e)=>{if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using(var brush=new SolidBrush(selected?Color.FromArgb(55,49,39):EditorTheme.Background))e.Graphics.FillRectangle(brush,e.Bounds);if(selected)using(var brush=new SolidBrush(EditorTheme.Amber))e.Graphics.FillRectangle(brush,e.Bounds.X,e.Bounds.Y,3,e.Bounds.Height);TextRenderer.DrawText(e.Graphics,objects.Items[e.Index].ToString(),objects.Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y,e.Bounds.Width-16,e.Bounds.Height),EditorTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);};
            objects.SelectedIndexChanged+=(s,e)=>{if(loading)return;if(!DiscardAllowed()){RestoreSelection();return;}LoadSelection();};
            objects.DoubleClick+=(s,e)=>canvas.FocusDraft();
            var orderTools=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
            orderTools.Controls.Add(orderUp);orderTools.Controls.Add(orderDown);
            orderTools.Controls.Add(new EditorLabel{Text="Top item appears in front · drag to reorder",AutoSize=true,MaximumSize=new Size(210,0)});
            orderUp.Click+=async(s,e)=>await MoveOrder(-1);orderDown.Click+=async(s,e)=>await MoveOrder(1);
            objects.AllowDrop=true;
            objects.MouseDown+=(s,e)=>{dragOrigin=e.Location;var row=objects.SelectedItem as MapRow;dragKey=e.Button==MouseButtons.Left&&row!=null&&MapOrder.IsMarker(row)&&!row.ReadOnly?row.Key:null;};
            objects.MouseMove+=(s,e)=>{if(e.Button!=MouseButtons.Left||dragKey==null||busy||dirty||freezeDirty)return;var threshold=new Rectangle(dragOrigin.X-SystemInformation.DragSize.Width/2,dragOrigin.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);if(threshold.Contains(e.Location))return;string key=dragKey;dragKey=null;objects.DoDragDrop(key,DragDropEffects.Move);dropIndex=-1;objects.Invalidate();};
            objects.DragOver+=(s,e)=>{var p=objects.PointToClient(new Point(e.X,e.Y));dropIndex=objects.IndexFromPoint(p);var row=dropIndex<0?null:objects.Items[dropIndex] as MapRow;dropAfter=row!=null&&p.Y>objects.GetItemRectangle(dropIndex).Top+objects.ItemHeight/2;e.Effect=!busy&&!dirty&&!freezeDirty&&e.Data.GetDataPresent(typeof(string))&&row!=null&&MapOrder.IsMarker(row)?DragDropEffects.Move:DragDropEffects.None;if(e.Effect==DragDropEffects.None)dropIndex=-1;else if(p.Y<objects.ItemHeight&&objects.TopIndex>0)objects.TopIndex--;else if(p.Y>objects.Height-objects.ItemHeight&&objects.TopIndex<objects.Items.Count-1)objects.TopIndex++;objects.Invalidate();};
            objects.DragLeave+=(s,e)=>{dropIndex=-1;objects.Invalidate();};
            objects.DragDrop+=async(s,e)=>{int at=dropIndex;bool after=dropAfter;dropIndex=-1;objects.Invalidate();if(at<0||at>=objects.Items.Count)return;await Reorder(e.Data.GetData(typeof(string)) as string,((MapRow)objects.Items[at]).Key,after);};
            objects.DrawItem+=(s,e)=>{if(e.Index!=dropIndex)return;using(var pen=new Pen(EditorTheme.Amber,2)){int y=dropAfter?e.Bounds.Bottom-1:e.Bounds.Top+1;e.Graphics.DrawLine(pen,e.Bounds.Left,y,e.Bounds.Right,y);}};
            var help=new EditorLabel{AutoSize=true,MaximumSize=new Size(210,0),Text="Wheel: zoom\nRight / middle drag: pan\nClick: place / select vertex\nDrag vertex: move it\nShift + drag: move shape\nCtrl + click edge: add vertex\nDelete: remove vertex\nEnter / double click: finish\nEsc: stop drawing\n\nNew markers go to tvtLobby_Markers; zones go to tvtLobby_Zones. Save world is explicit.",Padding=new Padding(4,12,4,4)};
            search.Dock=DockStyle.Fill;search.TextChanged+=(s,e)=>FilterObjects();
            left.Controls.Add(create,0,0);left.Controls.Add(search,0,1);left.Controls.Add(objects,0,2);left.Controls.Add(orderTools,0,3);left.Controls.Add(help,0,4);body.Controls.Add(left,0,0);
            canvas.Dock=DockStyle.Fill;canvas.Changed+=()=>{dirty=true;Text="tvtLobby | Mission Editor — Map *";if(canvas.Draft!=null&&vertices.Rows.Count!=canvas.Draft.Points.Count){loading=true;vertices.DataSource=new System.ComponentModel.BindingList<MapPoint>(canvas.Draft.Points);loading=false;}vertices.Refresh();};
            canvas.Picked+=key=>{if(DiscardAllowed())SelectMapRow(key);};
            canvas.PositionChanged+=text=>coordinates.Text=text;body.Controls.Add(canvas,1,0);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,80));right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,20));right.RowStyles.Add(new RowStyle(SizeType.Absolute,25));
            description.AutoSize=true;description.MaximumSize=new Size(316,0);description.Padding=new Padding(4);description.Text="Choose an object or draw a new one.";
            properties.Dock=DockStyle.Fill;properties.PropertyValueChanged+=(s,e)=>{dirty=true;canvas.Invalidate();};
            properties.FreezeDraftChanged+=settings=>{freezeDraft=settings;freezeDirty=true;Text="tvtLobby | Mission Editor — Map *";};
            properties.FreezeChanged+=async settings=>{freezeDraft=settings;freezeDirty=true;await ApplyDrafts();};
            vertices.Dock=DockStyle.Fill;vertices.AutoGenerateColumns=false;vertices.AllowUserToAddRows=false;vertices.AllowUserToDeleteRows=false;vertices.RowHeadersVisible=false;
            foreach(string axis in new[]{"X","Y","Z"})vertices.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=axis,HeaderText=axis,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,DefaultCellStyle=new DataGridViewCellStyle{Format="0.000"}});
            vertices.CellValueChanged+=(s,e)=>{if(!loading){dirty=true;canvas.Invalidate();}};
            vertices.DataError+=(s,e)=>{status.Text="Coordinates must be finite numbers.";e.ThrowException=false;};
            var identity=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
            identity.Controls.Add(description);identity.Controls.Add(new EditorLabel {Text="Entity name (blank = automatic)",AutoSize=true});entityName.Width=310;identity.Controls.Add(entityName);
            identity.Controls.Add(layerName);identity.Controls.Add(moveLayer);moveLayer.Click+=async(s,e)=>await Run("layer");
            entityName.TextChanged+=(s,e)=>{if(!loading&&canvas.Draft!=null){canvas.Draft.Name=entityName.Text.Trim();dirty=true;}};
            coordinates.Dock=DockStyle.Fill;right.Controls.Add(identity,0,0);right.Controls.Add(properties,0,1);right.Controls.Add(ground,0,2);right.Controls.Add(vertices,0,3);right.Controls.Add(coordinates,0,4);body.Controls.Add(right,2,0);
            frame.Controls.Add(body,0,2);status.Dock=DockStyle.Fill;status.Padding=new Padding(4,6,4,0);frame.Controls.Add(status,0,3);Controls.Add(frame);
            EditorTheme.Apply(this);status.Text="Connecting to Workbench…";UpdateConnection();Shown+=async(s,e)=>await Run("snapshot");
            FormClosing+=(s,e)=>{if(busy){e.Cancel=true;return;}if(!DiscardAllowed())e.Cancel=true;};
            imageTimer.Tick+=async(s,e)=>{if(!busy&&!dirty&&!freezeDirty&&imageRetries++<5)await Run("snapshot",true);else imageTimer.Stop();};
        }
        private void Button(string title,Func<Task> run)
        {var b=new EditorButton{Text=title,Primary=title=="Apply"};b.Click+=async(s,e)=>await run();tools.Controls.Add(b);}
        private void UpdateConnection()
        {
            bool connected=view!=null&&!busy&&!connectionFailed;body.Enabled=connected;
            foreach(Control button in tools.Controls)button.Enabled=!busy&&(button.Text=="Refresh"||connected);
            UpdateOrderButtons();
        }
        private bool DiscardAllowed()
        {
            if(!dirty&&!freezeDirty)return true;
            if(MessageBox.Show(this,"Discard the unapplied map / freeze draft? Applied edits remain in Workbench and can be undone there.","Map draft",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return false;
            dirty=false;freezeDirty=false;freezeDraft=null;if(view!=null)view.FreezeZones=savedFreezeSettings;return true;
        }
        private async Task ApplyDrafts()
        {
            if(freezeDirty){string error=freezeDraft.Validate();if(error!=null){status.Text=error;return;}}
            if(dirty && !await Run(canvas.Draft!=null&&String.IsNullOrEmpty(canvas.Draft.Key)?"create":"update"))return;
            if(freezeDirty)await Run("freeze");
            else if(!dirty)status.Text="Applied changes are in Workbench. Save world writes them to disk.";
        }
        private void RestoreSelection()
        {loading=true;objects.SelectedItem=canvas.Draft==null?null:objects.Items.Cast<MapRow>().FirstOrDefault(r=>r.Key==canvas.Draft.Key);loading=false;}
        private void LoadSelection()
        {var row=objects.SelectedItem as MapRow;SetDraft(row==null?null:row.Copy(),false);}
        private void SelectMapRow(string key)
        {
            // Placed groups and vehicles can be picked on the map without being
            // entries in the author-created markers/zones list.
            var row=view==null?null:view.Rows.FirstOrDefault(r=>r.Key==key);
            loading=true;objects.SelectedItem=objects.Items.Cast<MapRow>().FirstOrDefault(r=>r.Key==key);loading=false;
            SetDraft(row==null?null:row.Copy(),false);
        }
        private void FilterObjects(string selectedKey=null)
        {
            if(view==null)return;
            string key=selectedKey??(canvas.Draft==null?"":canvas.Draft.Key);
            loading=true;
            var rows=MapOrder.FrontToBack(view.Rows.Where(r=>r.Kind!="Group"&&r.Kind!="Vehicle")).OrderByDescending(MapOrder.IsMarker).Where(r=>r.ToString().IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0).ToList();
            objects.DataSource=rows;objects.SelectedItem=rows.FirstOrDefault(r=>r.Key==key);
            loading=false;
            UpdateOrderButtons();
        }
        private void UpdateOrderButtons()
        {
            var selected=objects.SelectedItem as MapRow;
            var rows=objects.Items.Cast<MapRow>().Where(MapOrder.IsMarker).ToList();int index=rows.IndexOf(selected);
            bool editable=selected!=null&&!selected.ReadOnly&&!busy;
            orderUp.Enabled=editable&&index>0;orderDown.Enabled=editable&&index>=0&&index<rows.Count-1;
        }
        private Task MoveOrder(int direction)
        {
            var rows=objects.Items.Cast<MapRow>().Where(MapOrder.IsMarker).ToList();var selected=objects.SelectedItem as MapRow;int index=rows.IndexOf(selected),to=index+direction;
            if(index<0||to<0||to>=rows.Count)return Task.FromResult(false);
            return Reorder(selected.Key,rows[to].Key,direction>0);
        }
        private async Task Reorder(string key,string target,bool after)
        {
            if(busy||view==null||connectionFailed||String.IsNullOrEmpty(key)||key==target)return;
            if(dirty||freezeDirty){status.Text="Apply or discard the current draft before changing layer order.";return;}
            var row=view.Rows.FirstOrDefault(r=>r.Key==key);if(row==null||row.ReadOnly)return;
            pendingOrder=MapOrder.Move(view.Rows,key,target,after);
            if(pendingOrder.SequenceEqual(MapOrder.FrontToBack(view.Rows.Where(MapOrder.IsMarker)).Select(r=>r.Key))){pendingOrder=null;return;}
            SelectMapRow(key);try{await Run("reorder");}finally{pendingOrder=null;UpdateOrderButtons();}
        }
        private void SetDraft(MapRow row,bool drawing)
        {
            loading=true;canvas.Draft=row;canvas.Drawing=drawing;canvas.SelectedVertex=-1;dirty=drawing;
            canvas.Locked=row!=null&&(row.ReadOnly||!String.IsNullOrEmpty(row.Source));
            properties.ShowRow(row,view,assets);
            properties.Enabled=!canvas.Locked||(row!=null&&row.Kind=="Group");vertices.ReadOnly=canvas.Locked;ground.Checked=drawing;ground.Enabled=!canvas.Locked;
            entityName.Text=row==null?"":row.Name;entityName.ReadOnly=row==null||!String.IsNullOrEmpty(row.Key);
            string targetLayer=row==null?"":MapRow.DefaultLayer(row.Kind);
            layerName.Text=row==null?"":String.IsNullOrEmpty(row.Key)?"Create in: "+targetLayer:"Layer: "+row.Layer;
            moveLayer.Text="Move to "+targetLayer;
            moveLayer.Visible=row!=null&&!String.IsNullOrEmpty(row.Key)&&row.CanMoveLayer&&!String.IsNullOrEmpty(targetLayer)&&row.Layer!=targetLayer;
            vertices.DataSource=row==null?null:new System.ComponentModel.BindingList<MapPoint>(row.Points);
            description.Text=row==null?"Choose an object or draw a new one.":row.Kind+" · "+(String.IsNullOrEmpty(row.Name)?"New object":row.Name)+"\n"+
                (row.Kind=="Group"||row.Kind=="Vehicle"?"Placed object. Select in World Editor to edit its position.":canvas.Locked?"Inherited / locked layer or linked geometry: edit its source in Workbench.":row.Kind=="FreezeCircle"?"Vanilla circle: drag the center; configure radii and group assignments below.":row.Kind=="Zone"||row.Kind=="Boundary"?"Authoring contour. Configure geometry here; use it in Objectives or freeze assignments.":"Appearance updates directly on this map. Player preview filters the initial visibility by phase and faction.");
            Text="tvtLobby | Mission Editor — Map"+(dirty||freezeDirty?" *":"");loading=false;UpdateOrderButtons();canvas.Invalidate();
        }
        private void New(string kind)
        {
            if(busy||view==null||!DiscardAllowed())return;
            loading=true;objects.ClearSelected();loading=false;
            var row=new MapRow{Kind=kind,Title="New "+kind,Enabled=kind!="Boundary"};
            if(MapOrder.IsMarker(row))row.Style=new MapStyle{ZOrder=view.Rows.Select(MapOrder.Value).DefaultIfEmpty(0).Max()+1,Tint=view.Colors.Count>0?view.Colors[0]:"1 1 1 1",LabelColor=view.Colors.Count>0?view.Colors[0]:"1 1 1 1"};
            SetDraft(row,true);
            status.Text="Click to draw "+kind+". Finish with Enter, then Apply. Destination: "+MapRow.DefaultLayer(kind)+".";canvas.Focus();
        }
        private string ValidateDraft()
        {
            var row=canvas.Draft;if(row==null)return "Choose or draw an object first.";
            if(canvas.Locked)return "This object is read only here.";
            if(row.Kind=="FreezeCircle"&&(!(row.Radius>=1&&row.Radius<=5000&&row.WarnRadius>=0&&row.WarnRadius<=row.Radius)))return "Use an outer radius of 1–5000m and a warning radius between 0 and the outer radius.";
            if(row.Points.Any(p=>Double.IsNaN(p.X)||Double.IsInfinity(p.X)||Double.IsNaN(p.Y)||Double.IsInfinity(p.Y)||Double.IsNaN(p.Z)||Double.IsInfinity(p.Z)))return "Coordinates must be finite.";
            if(row.Audience==4&&(row.Subjects==null||row.Subjects.Count==0))return "Select at least one faction or alliance for the audience.";
            return null;
        }
        private async Task<bool> Run(string operation,bool background=false)
        {
            if(busy)return false;
            if(!background&&(operation=="snapshot"||operation=="undo"||operation=="redo")&&!DiscardAllowed())return false;
            vertices.EndEdit();properties.Refresh();
            if(operation=="create"||operation=="update"){string error=ValidateDraft();if(error!=null){status.Text=error;return false;}}
            if(operation!="snapshot"&&view==null){status.Text="Refresh to connect first.";return false;}
            if((operation=="select"||operation=="layer")&&(canvas.Draft==null||String.IsNullOrEmpty(canvas.Draft.Key))){status.Text="Apply the object first.";return false;}
            if((operation=="select"||operation=="layer")&&(dirty||freezeDirty)){status.Text="Apply or discard the draft before working with its saved object in Workbench.";return false;}
            if(operation=="save"&&(dirty||freezeDirty)){status.Text="Apply or discard your map draft before saving the world.";return false;}
            var request=new Dictionary<string,object>{{"APIFunc","tvtLobby_MissionMapBridge"},{"Token",session.Token},{"Operation",operation},
                {"ExpectedWorld",view==null?"":view.WorldPath},{"ViewRevision",view==null?"":view.ViewRevision},
                {"Key",canvas.Draft==null?"":canvas.Draft.Key},{"Ground",ground.Checked}};
            if(operation=="create"||operation=="update")request["Row"]=canvas.Draft;
            if(operation=="freeze")request["FreezeZones"]=freezeDraft;
            if(operation=="reorder")request["Order"]=pendingOrder;
            busy=true;UpdateConnection();
            status.Text=operation=="snapshot"?"Reading map and world objects from Workbench…":"Applying map action in Workbench…";
            try
            {
                var json=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};
                // A full-terrain snapshot can exceed the short general-editor timeout.
                // Keep waiting bounded, without retrying a mutation automatically.
                MapReply reply=await Task.Run(()=>MapWire.Read(WorkbenchClient.Call(session.Port,request,15000)));
                if(IsDisposed)return false;
                if(reply==null||!reply.Ok){connectionFailed=reply==null||reply.Code=="NO_SESSION"||reply.Code=="WORLD_CHANGED"||reply.Code=="INCOMPATIBLE_EDITOR";status.Text=reply==null?"Empty map response.":reply.Message+" ["+reply.Code+"]";return false;}
                if(assets==null||view==null||view.WorldPath!=reply.WorldPath){if(assets!=null)assets.Dispose();assets=new MapAssets(reply);if(topology!=null)topology.Dispose();topology=null;canvas.Topology=null;topologyResource=null;}
                status.Text="Loading map symbols…";
                await Task.Run(()=>assets.Preload(reply));
                if(IsDisposed)return false;
                if(!String.IsNullOrEmpty(reply.Background)||!String.IsNullOrEmpty(reply.BackgroundResource))
                {
                    status.Text="Loading terrain image…";
                    try{reply.Background=await Task.Run(()=>MapTexture.DecodeReply(reply));reply.BackgroundStatus="Terrain map loaded";}
                    catch(Exception error){reply.Background="";reply.BackgroundStatus=error.Message+" Coordinate grid is available.";}
                }
                if(IsDisposed)return false;
                if(topologyResource!=reply.GeometryResource||topology==null)
                {
                    status.Text="Loading roads, buildings and walls…";
                    try
                    {
                        MapTopology next=await Task.Run(()=>MapTopology.Load(reply,assets));
                        if(IsDisposed){if(next!=null)next.Dispose();return false;}
                        if(topology!=null)topology.Dispose();topology=next;
                        topologyStatus=next==null?"Map geometry is not configured in this world.":next.Summary;
                    }
                    catch(Exception error){if(topology!=null)topology.Dispose();topology=null;topologyStatus="Map details unavailable: "+error.Message;}
                    topologyResource=reply.GeometryResource;
                }
                if(IsDisposed)return false;
                savedFreezeSettings=reply.FreezeZones;
                if(operation=="freeze"){freezeDirty=false;freezeDraft=null;}
                if(freezeDirty)reply.FreezeZones=freezeDraft;
                bool first=view==null||view.WorldPath!=reply.WorldPath;
                string key=String.IsNullOrEmpty(reply.SelectedKey)?canvas.Draft==null?"":canvas.Draft.Key:reply.SelectedKey;
                view=reply;connectionFailed=false;
                canvas.Assets=assets;canvas.Topology=topology;canvas.SetScene(reply,first);FilterObjects(key);
                string selectedFaction=canvas.FactionFilter;factionFilter.Items.Clear();factionFilter.Items.Add("All / unassigned");if(reply.Forces!=null)factionFilter.Items.AddRange(reply.Forces.Factions.Cast<object>().ToArray());factionFilter.SelectedItem=reply.Forces==null?null:reply.Forces.Factions.FirstOrDefault(f=>f.Key==selectedFaction);if(factionFilter.SelectedIndex<0)factionFilter.SelectedIndex=0;
                SelectMapRow(key);
                status.Text=(reply.Message??"")+"\n"+reply.WorldPath+" · "+reply.BackgroundStatus+" · "+topologyStatus;
                if(String.IsNullOrEmpty(reply.Background)&&reply.BackgroundStatus=="Preparing terrain image"&&imageRetries<5)imageTimer.Start();else imageTimer.Stop();
                return true;
            }
            catch(Exception error){status.Text=error.Message;connectionFailed=true;return false;}
            finally{busy=false;if(!IsDisposed)UpdateConnection();}
        }
        protected override void Dispose(bool disposing){if(disposing){imageTimer.Dispose();if(assets!=null)assets.Dispose();if(topology!=null)topology.Dispose();}base.Dispose(disposing);}
    }
}
