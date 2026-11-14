using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace TvtLobby.MissionEditor
{
    public sealed partial class ForcesPage
    {
        FlowLayoutPanel factionTabs;TextBox groupSearch;TableLayoutPanel groupBody;Panel groupInspector;EditorTabs groupViews;
        VehicleLinksPanel links;
        Button discardAll;
        Label batchStatus;
        const string BatchUpdateRequired="Restart Workbench with updated tvtLobby scripts to apply groups and vehicles together.";
        readonly List<string> pendingOrder=new List<string>();
        string orderBaseline="";
        bool orderConflict;
        bool HasOrderDraft {get{return pendingOrder.Count>0;}}
        // Reordering changes only the local draft. Bridge support and GameMode
        // permissions gate Apply, not the mouse gesture or the Move buttons.
        bool CanEditOrder {get{return !orderConflict&&!groupMoving;}}
        string OrderError {get{return !HasOrderDraft?null:orderConflict?"Group order changed in Workbench. Discard the order draft and reorder again.":data.GroupOrderVersion<1?"Restart Workbench with updated tvtLobby scripts to apply group order.":data.GroupOrderEditable==0?"GameMode is inherited or locked.":null;}}
        Point groupDragOrigin;string groupDragKey;int groupDropIndex=-1,groupScrollAt;bool groupDropAfter,groupMoving;
        const string GroupDragFormat="tvtLobby.MissionEditor.Group";
        void EnableGroupDragging()
        {
            grid.AllowDrop=true;
            grid.MouseDown+=(s,e)=>{
                groupDragKey=null;int at=grid.HitTest(e.X,e.Y).RowIndex;
                if(e.Button!=MouseButtons.Left||at<0||dirty||conflict||!CanEditOrder)return;
                var row=grid.Rows[at].DataBoundItem as EditorGroup;
                if(row==null||row.Playable==0||row.Editable==0)return;
                groupDragOrigin=e.Location;groupDragKey=row.Key;
            };
            grid.MouseUp+=(s,e)=>groupDragKey=null;
            grid.MouseMove+=(s,e)=>{
                string key=TakeGroupDrag(e);if(key==null)return;
                try{grid.DoDragDrop(new DataObject(GroupDragFormat,key),DragDropEffects.Move);}
                finally{groupDropIndex=-1;grid.Invalidate();}
            };
            grid.DragOver+=(s,e)=>{
                var point=grid.PointToClient(new Point(e.X,e.Y));int at=grid.HitTest(point.X,point.Y).RowIndex;
                string key=e.Data.GetData(GroupDragFormat) as string;
                var target=at<0?null:grid.Rows[at].DataBoundItem as EditorGroup;
                groupDropIndex=-1;e.Effect=DragDropEffects.None;
                if(CanDropGroup(key,target)){
                    groupDropIndex=at;groupDropAfter=point.Y>grid.GetRowDisplayRectangle(at,false).Top+grid.Rows[at].Height/2;e.Effect=DragDropEffects.Move;
                    int now=Environment.TickCount;
                    if(now-groupScrollAt>=120){int top=grid.FirstDisplayedScrollingRowIndex;if(top>=0){if(point.Y<grid.ColumnHeadersHeight+25&&top>0)grid.FirstDisplayedScrollingRowIndex=top-1;else if(point.Y>grid.Height-30&&top<grid.RowCount-1)grid.FirstDisplayedScrollingRowIndex=top+1;}groupScrollAt=now;}
                }
                grid.Invalidate();
            };
            grid.DragLeave+=(s,e)=>{groupDropIndex=-1;grid.Invalidate();};
            grid.DragDrop+=(s,e)=>{
                int at=groupDropIndex;bool after=groupDropAfter;groupDropIndex=-1;grid.Invalidate();
                if(at<0||at>=grid.Rows.Count)return;
                DropGroup(e.Data.GetData(GroupDragFormat) as string,grid.Rows[at].DataBoundItem as EditorGroup,after);
            };
            grid.RowPostPaint+=(s,e)=>{if(e.RowIndex==groupDropIndex)using(var pen=new Pen(EditorTheme.Amber,3)){int y=groupDropAfter?e.RowBounds.Bottom-2:e.RowBounds.Top+1;e.Graphics.DrawLine(pen,e.RowBounds.Left,y,e.RowBounds.Right,y);}};
        }
        string TakeGroupDrag(MouseEventArgs e)
        {
            if(!Enabled||e.Button!=MouseButtons.Left||groupDragKey==null||selected!=groupDragKey||dirty||conflict||!CanEditOrder)return null;
            var threshold=new Rectangle(groupDragOrigin.X-SystemInformation.DragSize.Width/2,groupDragOrigin.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);
            if(threshold.Contains(e.Location))return null;
            string key=groupDragKey;groupDragKey=null;return key;
        }
        bool CanDropGroup(string key,EditorGroup target)
        {
            var source=data.Groups.FirstOrDefault(g=>g.Key==key);
            return Enabled&&!dirty&&!conflict&&CanEditOrder&&source!=null&&target!=null&&source.Key!=target.Key&&source.Playable!=0&&target.Playable!=0&&source.Editable!=0&&source.EngineKey==target.EngineKey;
        }
        void DropGroup(string key,EditorGroup target,bool after)
        {
            if(!CanDropGroup(key,target))return;
            var rows=OrderedGroups().Where(g=>g.Playable!=0&&g.EngineKey==target.EngineKey).ToList();
            int from=rows.FindIndex(g=>g.Key==key),to=rows.IndexOf(target)+(after?1:0);
            if(from<to)to--;
            if(from<0||to==from)return;
            selected=key;MoveGroup(to-from);
        }
        List<EditorGroup> OrderedGroups()
        {
            var rows=data.OrderedGroups();
            if(!HasOrderDraft)return rows;
            var byKey=rows.Where(g=>g.Playable!=0).ToDictionary(g=>g.Key);
            var ordered=pendingOrder.Where(byKey.ContainsKey).Select(k=>byKey[k]).ToList();
            ordered.AddRange(rows.Where(g=>g.Playable!=0&&!pendingOrder.Contains(g.Key)));
            int index=0;
            return rows.Select(g=>g.Playable!=0?ordered[index++]:g).ToList();
        }
        static string OrderStamp(ForcesView view)
        {
            return EditorJson.Serialize(new {view.Order,Groups=view.Groups.OrderBy(g=>g.Key,StringComparer.Ordinal).Select(g=>new {g.Key,g.Id,g.EngineKey,g.Playable,g.Editable}).ToArray()});
        }
        void MoveGroup(int delta)
        {
            var group=data.Groups.FirstOrDefault(g=>g.Key==selected);
            if(!CanEditOrder||dirty||conflict||group==null||group.Editable==0||group.Playable==0||delta==0)return;
            var all=OrderedGroups().Where(g=>g.Playable!=0).ToList();
            var same=all.Where(g=>g.EngineKey==group.EngineKey).ToList();
            int from=same.IndexOf(group),to=from+delta;
            if(from<0||to<0||to>=same.Count)return;
            if(!HasOrderDraft)orderBaseline=OrderStamp(data);
            same.RemoveAt(from);same.Insert(to,group);
            int index=0;
            pendingOrder.Clear();
            pendingOrder.AddRange(all.Select(g=>g.EngineKey==group.EngineKey?same[index++].Key:g.Key));
            if(pendingOrder.SequenceEqual(data.OrderedGroups().Where(g=>g.Playable!=0).Select(g=>g.Key)))ClearOrder();
            FillGrid();UpdateActions();
            if(DraftChanged!=null)DraftChanged();
        }
        void ClearOrder(){pendingOrder.Clear();orderConflict=false;orderBaseline="";}
        void DiscardBatch()
        {
            DiscardDraft();FillGrid();UpdateActions();
            if(DraftChanged!=null)DraftChanged();
        }
        void UpdateBatchActions()
        {
            if(batchStatus==null)return;
            apply.Enabled=HasDraft&&DraftError==null&&!groupMoving;
            discardAll.Enabled=HasDraft&&!groupMoving;
            var pending=new List<string>();
            if(HasOrderDraft)pending.Add("group order");
            if(dirty)pending.Add("group settings");
            if(vehicles!=null&&vehicles.HasDraft)pending.Add(vehicles.PendingCount+" vehicle assignments");
            batchStatus.Text=DraftError??(HasDraft?"Pending: "+String.Join(", ",pending)+". Apply sends all factions and tabs together.":"Reorder groups and assign vehicles across all tabs, then Apply once.");
        }
        void RefreshLinks()
        {
            if(links!=null)links.Accept(vehicles.Preview,OrderedGroups(),(filter.SelectedItem as FilterItem??new FilterItem()).Key,dirty||conflict||groupMoving);
        }
        void ArrangeGroupPage(TableLayoutPanel old,FlowLayoutPanel top)
        {
            var controls=old.Controls.Cast<Control>().ToList();old.Controls.Clear();Controls.Remove(old);old.Dispose();BackColor=EditorTheme.Background;
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty};frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            factionTabs=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0,0,0,8)};frame.Controls.Add(factionTabs,0,0);
            groupBody=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};groupBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));groupBody.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,310));
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};left.RowStyles.Add(new RowStyle(SizeType.Absolute,48));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));groupSearch=new TextBox{Dock=DockStyle.Top,Margin=new Padding(0,6,12,10)};var searchField=EditorTheme.Field("Find a callsign or world group",groupSearch);left.RowStyles[0].Height=72;left.Controls.Add(searchField,0,0);groupSearch.TextChanged+=(s,e)=>{if(!filling)FillGrid();};
            groupViews=new EditorTabs{Dock=DockStyle.Fill,ItemSize=new Size(128,34)};var groupsTab=new TabPage("Groups"){BackColor=EditorTheme.Background,Padding=new Padding(0,10,12,0)};var vehiclesTab=new TabPage("Vehicles"){BackColor=EditorTheme.Background,Padding=new Padding(0,10,12,0)};groupViews.TabPages.Add(groupsTab);groupViews.TabPages.Add(vehiclesTab);
            grid.Dock=DockStyle.Fill;groupsTab.Controls.Add(grid);
            var batchActions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Margin=new Padding(0,8,0,0)};
            discardAll=EditorTheme.Button("Discard changes",DiscardBatch);
            batchActions.Controls.AddRange(new Control[]{apply,discardAll});frame.Controls.Add(batchActions,0,2);
            batchStatus=EditorTheme.Label("",9,EditorTheme.Muted);frame.Controls.Add(batchStatus,0,3);
            vehicles.Dock=DockStyle.Fill;vehiclesTab.Controls.Add(vehicles);
            var linksTab=new TabPage("Assignments"){BackColor=EditorTheme.Background,Padding=new Padding(0,10,12,0)};
            links=new VehicleLinksPanel(vehicles);linksTab.Controls.Add(links);groupViews.TabPages.Add(linksTab);
            left.Controls.Add(groupViews,0,1);groupBody.Controls.Add(left,0,0);
            groupInspector=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=EditorTheme.Panel,Padding=new Padding(18)};var inspector=EditorTheme.Stack();inspector.BackColor=EditorTheme.Panel;EditorTheme.Add(inspector,EditorTheme.Label("GROUP SETTINGS",8,EditorTheme.Muted));info.Font=EditorTheme.Font(13,FontStyle.Bold);info.MaximumSize=new Size(260,0);EditorTheme.Add(inspector,info);EditorTheme.Add(inspector,playable);EditorTheme.Add(inspector,assigned);
            foreach(var pair in new[]{Tuple.Create("Company",company),Tuple.Create("Platoon",platoon),Tuple.Create("Squad",squad)})EditorTheme.Add(inspector,EditorTheme.Field(pair.Item1,pair.Item2));preview.MaximumSize=new Size(260,0);preview.Padding=new Padding(10);preview.BackColor=Color.FromArgb(39,42,39);EditorTheme.Add(inspector,preview);EditorTheme.Add(inspector,EditorTheme.Label("Order within faction · drag to reorder",10,null,true));EditorTheme.Add(inspector,actions);notice.MaximumSize=new Size(260,0);notice.Font=EditorTheme.Font(8);EditorTheme.Add(inspector,notice);EditorTheme.Add(inspector,EditorTheme.Button("Assigned vehicles →",()=>groupViews.SelectedIndex=1));groupInspector.Controls.Add(inspector);groupBody.Controls.Add(groupInspector,1,0);frame.Controls.Add(groupBody,0,1);Controls.Add(frame);
            filter.Visible=false;Controls.Add(filter);top.Dispose();foreach(var c in controls)if(c.Parent==null&&c!=filter)c.Dispose();
            SizeChanged+=(s,e)=>ArrangeGroupColumns();groupViews.SelectedIndexChanged+=(s,e)=>ArrangeGroupColumns();
        }
        void ArrangeGroupColumns(){bool show=groupViews.SelectedIndex==0;groupInspector.Visible=show;groupBody.ColumnStyles[1].Width=show?(Width<850?260:310):0;var left=(TableLayoutPanel)groupViews.Parent;left.GetControlFromPosition(0,0).Visible=show;left.RowStyles[0].Height=show?72:0;}
        void RebuildFactionTabs(){if(factionTabs==null)return;while(factionTabs.Controls.Count>0)factionTabs.Controls[0].Dispose();for(int i=0;i<filter.Items.Count;i++){int index=i;var b=new EditorButton{Text=filter.Items[i].ToString(),Navigation=true,Selected=i==filter.SelectedIndex,AutoSize=true};b.Click+=(s,e)=>{filter.SelectedIndex=index;RebuildFactionTabs();};factionTabs.Controls.Add(b);}}
    }
}
