using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    // A second view of the vehicle draft; all validation and writes stay in VehiclesPanel.
    public sealed class VehicleLinksPanel : UserControl
    {
        readonly VehiclesPanel draft;
        readonly DataGridView groups=new DataGridView(),vehicles=new DataGridView();
        readonly TextBox groupSearch=new TextBox(),vehicleSearch=new TextBox();
        readonly LinkCanvas canvas=new LinkCanvas();
        readonly Label status;
        readonly Button remove;
        ForcesView view=new ForcesView();
        List<EditorGroup> ordered=new List<EditorGroup>();
        string faction="",fingerprint="",selectedGroup="",selectedVehicle="";
        bool blocked;
        Point dragOrigin;
        DragItem dragItem;
        int dropRow=-1,scrollAt;
        DataGridView dropGrid;
        const string GroupFormat="tvtLobby.MissionEditor.AssignGroup",VehicleFormat="tvtLobby.MissionEditor.AssignVehicle";

        sealed class DragItem {public string Key;public bool IsGroup;}
        sealed class Link {public string Group,Vehicle;public float Left,Right;public bool Automatic;}
        sealed class LinkCanvas : Control
        {
            public LinkCanvas(){DoubleBuffered=true;ResizeRedraw=true;Dock=DockStyle.Fill;Margin=Padding.Empty;}
        }

        public VehicleLinksPanel(VehiclesPanel draft)
        {
            this.draft=draft;Dock=DockStyle.Fill;BackColor=EditorTheme.Background;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var hint=EditorTheme.Label("Drag a group onto a vehicle, or a vehicle onto a group. Select a line to remove that link.",9,EditorTheme.Muted);
            layout.Controls.Add(hint,0,0);
            var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Margin=Padding.Empty};
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            columns.RowStyles.Add(new RowStyle(SizeType.Absolute,64));columns.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            groupSearch.Dock=vehicleSearch.Dock=DockStyle.Top;
            columns.Controls.Add(EditorTheme.Field("Find a group",groupSearch),0,0);columns.Controls.Add(EditorTheme.Field("Find a vehicle",vehicleSearch),2,0);
            SetupGrid(groups,"Groups",true);SetupGrid(vehicles,"Vehicles",false);
            columns.Controls.Add(groups,0,1);columns.Controls.Add(canvas,1,1);columns.Controls.Add(vehicles,2,1);layout.Controls.Add(columns,0,1);
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,Margin=Padding.Empty};
            remove=EditorTheme.Button("Remove selected link",RemoveLink);
            actions.Controls.Add(remove);layout.Controls.Add(actions,0,2);
            status=EditorTheme.Label("",9,EditorTheme.Muted);status.Dock=DockStyle.Top;layout.Controls.Add(status,0,3);
            var viewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true};layout.Dock=DockStyle.Top;layout.Height=340;
            viewport.Controls.Add(layout);Controls.Add(viewport);viewport.SizeChanged+=(s,e)=>layout.Height=Math.Max(340,viewport.ClientSize.Height);
            groupSearch.TextChanged+=(s,e)=>Rebuild();vehicleSearch.TextChanged+=(s,e)=>Rebuild();
            canvas.Paint+=(s,e)=>DrawLinks(e.Graphics);
            canvas.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left)SelectLink(e.Location);};
            canvas.MouseMove+=(s,e)=>canvas.Cursor=HitLink(e.Location)==null?Cursors.Default:Cursors.Hand;
            EditorTheme.Apply(this);
        }

        void SetupGrid(DataGridView grid,string heading,bool isGroup)
        {
            grid.Dock=DockStyle.Fill;grid.Margin=Padding.Empty;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;
            grid.AllowUserToResizeRows=false;grid.RowHeadersVisible=false;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns=false;grid.AllowDrop=true;grid.BorderStyle=BorderStyle.None;
            grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=heading,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,SortMode=DataGridViewColumnSortMode.NotSortable});
            grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText="Links",Width=50,SortMode=DataGridViewColumnSortMode.NotSortable});
            grid.Columns[0].DefaultCellStyle.WrapMode=DataGridViewTriState.True;
            grid.Columns[1].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;
            grid.CellMouseClick+=(s,e)=>{if(e.RowIndex<0||e.Button!=MouseButtons.Left)return;SelectRow(grid,e.RowIndex,isGroup);};
            grid.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Delete){RemoveLink();e.Handled=true;}};
            grid.KeyUp+=(s,e)=>{if((e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)&&grid.CurrentRow!=null)SelectRow(grid,grid.CurrentRow.Index,isGroup);};
            grid.Scroll+=(s,e)=>canvas.Invalidate();grid.SizeChanged+=(s,e)=>canvas.Invalidate();
            grid.MouseDown+=(s,e)=>{
                dragItem=null;int at=grid.HitTest(e.X,e.Y).RowIndex;if(e.Button!=MouseButtons.Left||at<0||blocked)return;
                dragOrigin=e.Location;dragItem=new DragItem{Key=RowKey(grid.Rows[at]),IsGroup=isGroup};
            };
            grid.MouseUp+=(s,e)=>dragItem=null;
            grid.MouseMove+=(s,e)=>{
                if(e.Button!=MouseButtons.Left||dragItem==null)return;
                var threshold=new Rectangle(dragOrigin.X-SystemInformation.DragSize.Width/2,dragOrigin.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);
                if(threshold.Contains(e.Location))return;
                var item=dragItem;dragItem=null;
                try{grid.DoDragDrop(new DataObject(item.IsGroup?GroupFormat:VehicleFormat,item.Key),DragDropEffects.Link);}finally{ClearDrop();}
            };
            grid.DragOver+=(s,e)=>{
                var item=ReadDrag(e.Data);
                var point=grid.PointToClient(new Point(e.X,e.Y));int at=grid.HitTest(point.X,point.Y).RowIndex;
                ClearDrop();e.Effect=DragDropEffects.None;
                if(item==null||item.IsGroup==isGroup||at<0||blocked)return;
                string target=RowKey(grid.Rows[at]);
                string group=isGroup?target:item.Key,vehicle=isGroup?item.Key:target;
                if(!draft.CanStageAssignment(vehicle,group,true))return;
                dropGrid=grid;dropRow=at;e.Effect=DragDropEffects.Link;grid.InvalidateRow(at);
                int now=Environment.TickCount;
                if(unchecked(now-scrollAt)>=120){int top=grid.FirstDisplayedScrollingRowIndex;if(top>=0){if(point.Y<grid.ColumnHeadersHeight+24&&top>0)grid.FirstDisplayedScrollingRowIndex=top-1;else if(point.Y>grid.Height-24&&top<grid.RowCount-1)grid.FirstDisplayedScrollingRowIndex=top+1;}scrollAt=now;}
            };
            grid.DragLeave+=(s,e)=>ClearDrop();
            grid.DragDrop+=(s,e)=>{
                var item=ReadDrag(e.Data);int at=dropGrid==grid?dropRow:-1;ClearDrop();
                if(item==null||item.IsGroup==isGroup||at<0||at>=grid.RowCount)return;
                string target=RowKey(grid.Rows[at]);StageLink(isGroup?target:item.Key,isGroup?item.Key:target);
            };
            grid.RowPostPaint+=(s,e)=>{if(dropGrid==grid&&dropRow==e.RowIndex)using(var pen=new Pen(EditorTheme.Amber,2)){var rect=e.RowBounds;rect.Inflate(-1,-1);e.Graphics.DrawRectangle(pen,rect);}};
        }

        static string RowKey(DataGridViewRow row)
        {
            var group=row.Tag as EditorGroup;if(group!=null)return group.Key;
            var vehicle=row.Tag as EditorVehicle;return vehicle==null?"":vehicle.Key;
        }
        static DragItem ReadDrag(IDataObject data)
        {
            string key=data.GetData(GroupFormat) as string;if(key!=null)return new DragItem{Key=key,IsGroup=true};
            key=data.GetData(VehicleFormat) as string;return key==null?null:new DragItem{Key=key};
        }
        static bool Matches(string text,string search){return text.IndexOf(search.Trim(),StringComparison.OrdinalIgnoreCase)>=0;}
        void ClearDrop(){if(dropGrid!=null)dropGrid.Invalidate();dropGrid=null;dropRow=-1;}

        public void Accept(ForcesView current,IEnumerable<EditorGroup> order,string factionKey,bool hasFieldsDraft)
        {
            view=current;ordered=order.ToList();faction=factionKey;blocked=hasFieldsDraft;
            string next=EditorJson.Serialize(new {view.Groups,view.Vehicles,Order=ordered.Select(g=>g.Key).ToArray(),faction,blocked});
            if(next!=fingerprint){fingerprint=next;Rebuild();}else UpdateActions();
        }
        void Rebuild()
        {
            int groupTop=groups.FirstDisplayedScrollingRowIndex,vehicleTop=vehicles.FirstDisplayedScrollingRowIndex;
            groups.Rows.Clear();vehicles.Rows.Clear();
            foreach(var group in ordered.Where(g=>g.Playable!=0&&(String.IsNullOrEmpty(faction)||g.EngineKey==faction)&&Matches((g.Name??"")+" "+g.Callsign,groupSearch.Text)))
            {
                int count=view.Vehicles.Count(v=>v.Groups.Contains(group.Key));
                int at=groups.Rows.Add((String.IsNullOrEmpty(group.Callsign)?group.Name:group.Callsign)+"\n"+group.Name,count);
                groups.Rows[at].Tag=group;groups.Rows[at].Height=56;
                groups.Rows[at].Cells[0].ToolTipText=group.Name+" · "+group.EngineKey+" · "+group.Slots+" slots";
            }
            foreach(var vehicle in view.Vehicles.Where(v=>(String.IsNullOrEmpty(faction)||v.EngineKey==faction)&&Matches((v.Name??"")+" "+v.Title+" "+v.Layer,vehicleSearch.Text)).OrderBy(v=>v.Name,StringComparer.OrdinalIgnoreCase))
            {
                string detail=vehicle.Title+(vehicle.IsChild?" · Automatic":vehicle.Editable==0?" · Locked":"");
                int at=vehicles.Rows.Add(vehicle.Name+"\n"+detail,vehicle.Groups.Count);
                vehicles.Rows[at].Tag=vehicle;vehicles.Rows[at].Height=56;
                vehicles.Rows[at].Cells[0].ToolTipText=vehicle.Name+" · "+detail+"\n"+vehicle.Layer+(String.IsNullOrEmpty(vehicle.Problem)?"":"\n"+vehicle.Problem);
            }
            groups.ClearSelection();vehicles.ClearSelection();
            if(groupTop>=0&&groups.RowCount>0)groups.FirstDisplayedScrollingRowIndex=Math.Min(groupTop,groups.RowCount-1);
            if(vehicleTop>=0&&vehicles.RowCount>0)vehicles.FirstDisplayedScrollingRowIndex=Math.Min(vehicleTop,vehicles.RowCount-1);
            if(!view.Groups.Any(g=>g.Key==selectedGroup))selectedGroup="";
            if(!view.Vehicles.Any(v=>v.Key==selectedVehicle))selectedVehicle="";
            groups.Columns[0].HeaderText="Groups ("+groups.RowCount+")";vehicles.Columns[0].HeaderText="Vehicles ("+vehicles.RowCount+")";
            RefreshSelection();
        }
        void SelectRow(DataGridView grid,int at,bool isGroup)
        {
            if(isGroup){selectedGroup=RowKey(grid.Rows[at]);selectedVehicle="";}
            else{selectedVehicle=RowKey(grid.Rows[at]);selectedGroup="";}
            RefreshSelection();
        }
        void StageLink(string group,string vehicle)
        {
            if(blocked||!draft.StageAssignment(vehicle,group,true))return;
            selectedGroup=group;selectedVehicle=vehicle;RefreshSelection();
        }
        bool HasSelectedLink(){return view.Vehicles.Any(v=>v.Key==selectedVehicle&&v.Groups.Contains(selectedGroup));}
        void RemoveLink()
        {
            if(blocked||!HasSelectedLink())return;
            if(draft.StageAssignment(selectedVehicle,selectedGroup,false))RefreshSelection();
        }
        void RefreshSelection()
        {
            var selected=view.Vehicles.FirstOrDefault(v=>v.Key==selectedVehicle);
            foreach(DataGridViewRow row in groups.Rows)
            {
                string key=RowKey(row);bool active=key==selectedGroup,linked=selected!=null&&selected.Groups.Contains(key);
                row.Selected=active;row.DefaultCellStyle.BackColor=linked?EditorTheme.Raised:EditorTheme.Panel;
                row.DefaultCellStyle.ForeColor=active||linked?EditorTheme.Text:EditorTheme.Muted;
            }
            foreach(DataGridViewRow row in vehicles.Rows)
            {
                var vehicle=(EditorVehicle)row.Tag;bool active=vehicle.Key==selectedVehicle,linked=vehicle.Groups.Contains(selectedGroup);
                row.Selected=active;row.DefaultCellStyle.BackColor=linked?EditorTheme.Raised:EditorTheme.Panel;
                row.DefaultCellStyle.ForeColor=active||linked?EditorTheme.Text:EditorTheme.Muted;
            }
            canvas.Invalidate();UpdateActions();
        }
        void UpdateActions()
        {
            if(remove==null)return;
            remove.Enabled=!blocked&&HasSelectedLink()&&draft.CanStageAssignment(selectedVehicle,selectedGroup,false);
            var vehicle=view.Vehicles.FirstOrDefault(v=>v.Key==selectedVehicle);
            status.Text=draft.DraftError??(blocked?"Apply or reload group fields before editing assignments.":view.VehicleAssignmentsVersion<2?VehiclesPanel.UpdateRequired:
                vehicle!=null&&vehicle.IsChild?"Automatic link: move the vehicle in World Editor to change its parent group.":
                vehicle!=null&&vehicle.Editable==0?"This vehicle is inherited or locked.":
                draft.HasDraft?draft.PendingCount+" pending assignment changes. Apply once when ready.":
                groups.RowCount==0||vehicles.RowCount==0?"No matching groups or vehicles. Check the faction and search filters.":
                "Vehicles can be shared by several groups. Selecting a row highlights its links.");
        }

        Dictionary<string,float> VisibleRows(DataGridView grid)
        {
            var result=new Dictionary<string,float>();if(!grid.IsHandleCreated||!canvas.IsHandleCreated)return result;
            foreach(DataGridViewRow row in grid.Rows)
            {
                if(!row.Displayed)continue;var rect=grid.GetRowDisplayRectangle(row.Index,false);int y=rect.Top+rect.Height/2;
                if(y<grid.ColumnHeadersHeight||y>=grid.ClientSize.Height)continue;
                result[RowKey(row)]=canvas.PointToClient(grid.PointToScreen(new Point(0,y))).Y;
            }
            return result;
        }
        List<Link> VisibleLinks()
        {
            var left=VisibleRows(groups);var right=VisibleRows(vehicles);var result=new List<Link>();
            foreach(var vehicle in view.Vehicles)
            {
                float end;if(!right.TryGetValue(vehicle.Key,out end))continue;
                foreach(string key in vehicle.Groups){float start;if(left.TryGetValue(key,out start))result.Add(new Link{Group=key,Vehicle=vehicle.Key,Left=start,Right=end,Automatic=vehicle.IsChild});}
            }
            return result.OrderBy(Highlighted).ToList();
        }
        bool Highlighted(Link link){return link.Group==selectedGroup||link.Vehicle==selectedVehicle;}
        GraphicsPath Path(Link link)
        {
            float width=Math.Max(1,canvas.Width-1);var path=new GraphicsPath();
            path.AddBezier(0,link.Left,width*.45f,link.Left,width*.55f,link.Right,width,link.Right);return path;
        }
        void DrawLinks(Graphics graphics)
        {
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            foreach(var link in VisibleLinks())using(var path=Path(link))using(var pen=new Pen(Highlighted(link)?EditorTheme.Amber:EditorTheme.Line,Highlighted(link)?2.5f:1.2f))
            {if(link.Automatic)pen.DashStyle=DashStyle.Dash;graphics.DrawPath(pen,path);}
        }
        Link HitLink(Point point)
        {
            var links=VisibleLinks();links.Reverse();
            using(var pen=new Pen(Color.White,10))foreach(var link in links)using(var path=Path(link))if(path.IsOutlineVisible(point,pen))return link;
            return null;
        }
        void SelectLink(Point point)
        {
            var link=HitLink(point);if(link==null)return;
            selectedGroup=link.Group;selectedVehicle=link.Vehicle;RefreshSelection();
        }
    }
}
