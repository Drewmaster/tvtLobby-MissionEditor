using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace TvtLobby.MissionEditor
{
    public sealed partial class ForcesPage
    {
        FlowLayoutPanel factionTabs;TextBox groupSearch;TableLayoutPanel groupBody;Panel groupInspector;EditorTabs groupViews;
        Point groupDragOrigin;string groupDragKey;int groupDropIndex=-1,groupScrollAt;bool groupDropAfter,groupMoving;
        const string GroupDragFormat="tvtLobby.MissionEditor.Group";
        void EnableGroupDragging()
        {
            grid.AllowDrop=true;
            grid.MouseDown+=(s,e)=>{
                groupDragKey=null;int at=grid.HitTest(e.X,e.Y).RowIndex;
                if(e.Button!=MouseButtons.Left||at<0||dirty||conflict||groupMoving)return;
                var row=grid.Rows[at].DataBoundItem as EditorGroup;
                if(row==null||row.Playable==0||row.Editable==0)return;
                groupDragOrigin=e.Location;groupDragKey=row.Key;
            };
            grid.MouseUp+=(s,e)=>groupDragKey=null;
            grid.MouseMove+=(s,e)=>{
                if(e.Button!=MouseButtons.Left||groupDragKey==null||selected!=groupDragKey||dirty||conflict||groupMoving)return;
                var threshold=new Rectangle(groupDragOrigin.X-SystemInformation.DragSize.Width/2,groupDragOrigin.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);
                if(threshold.Contains(e.Location))return;
                string key=groupDragKey;groupDragKey=null;
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
            grid.DragDrop+=async(s,e)=>{
                int at=groupDropIndex;bool after=groupDropAfter;groupDropIndex=-1;grid.Invalidate();
                if(at<0||at>=grid.Rows.Count)return;
                await DropGroup(e.Data.GetData(GroupDragFormat) as string,grid.Rows[at].DataBoundItem as EditorGroup,after);
            };
            grid.RowPostPaint+=(s,e)=>{if(e.RowIndex==groupDropIndex)using(var pen=new Pen(EditorTheme.Amber,3)){int y=groupDropAfter?e.RowBounds.Bottom-2:e.RowBounds.Top+1;e.Graphics.DrawLine(pen,e.RowBounds.Left,y,e.RowBounds.Right,y);}};
        }
        bool CanDropGroup(string key,EditorGroup target)
        {
            var source=data.Groups.FirstOrDefault(g=>g.Key==key);
            return Enabled&&!dirty&&!conflict&&!groupMoving&&source!=null&&target!=null&&source.Key!=target.Key&&source.Playable!=0&&target.Playable!=0&&source.Editable!=0&&source.EngineKey==target.EngineKey;
        }
        async Task DropGroup(string key,EditorGroup target,bool after)
        {
            if(!CanDropGroup(key,target))return;
            var rows=data.OrderedGroups().Where(g=>g.Playable!=0&&g.EngineKey==target.EngineKey).ToList();
            int from=rows.FindIndex(g=>g.Key==key),to=rows.IndexOf(target)+(after?1:0);
            if(from<to)to--;
            if(from<0||to==from)return;
            selected=key;await MoveGroup(to-from);
        }
        void ArrangeGroupPage(TableLayoutPanel old,FlowLayoutPanel top)
        {
            var controls=old.Controls.Cast<Control>().ToList();old.Controls.Clear();Controls.Remove(old);old.Dispose();BackColor=EditorTheme.Background;
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            factionTabs=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0,0,0,8)};frame.Controls.Add(factionTabs,0,0);
            groupBody=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};groupBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));groupBody.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,310));
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};left.RowStyles.Add(new RowStyle(SizeType.Absolute,48));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));groupSearch=new TextBox{Dock=DockStyle.Top,Margin=new Padding(0,6,12,10)};var searchField=EditorTheme.Field("Find a callsign or world group",groupSearch);left.RowStyles[0].Height=72;left.Controls.Add(searchField,0,0);groupSearch.TextChanged+=(s,e)=>{if(!filling)FillGrid();};
            groupViews=new EditorTabs{Dock=DockStyle.Fill};var groupsTab=new TabPage("Groups"){BackColor=EditorTheme.Background,Padding=new Padding(0,10,12,0)};var vehiclesTab=new TabPage("Available / assigned vehicles"){BackColor=EditorTheme.Background,Padding=new Padding(0,10,12,0)};groupViews.TabPages.Add(groupsTab);groupViews.TabPages.Add(vehiclesTab);grid.Dock=DockStyle.Fill;groupsTab.Controls.Add(grid);vehicles.Dock=DockStyle.Fill;vehiclesTab.Controls.Add(vehicles);left.Controls.Add(groupViews,0,1);groupBody.Controls.Add(left,0,0);
            groupInspector=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=EditorTheme.Panel,Padding=new Padding(18)};var inspector=EditorTheme.Stack();inspector.BackColor=EditorTheme.Panel;EditorTheme.Add(inspector,EditorTheme.Label("GROUP SETTINGS",8,EditorTheme.Muted));info.Font=EditorTheme.Font(13,FontStyle.Bold);info.MaximumSize=new Size(260,0);EditorTheme.Add(inspector,info);EditorTheme.Add(inspector,playable);EditorTheme.Add(inspector,assigned);
            foreach(var pair in new[]{Tuple.Create("Company",company),Tuple.Create("Platoon",platoon),Tuple.Create("Squad",squad)})EditorTheme.Add(inspector,EditorTheme.Field(pair.Item1,pair.Item2));preview.MaximumSize=new Size(260,0);preview.Padding=new Padding(10);preview.BackColor=Color.FromArgb(39,42,39);EditorTheme.Add(inspector,preview);EditorTheme.Add(inspector,EditorTheme.Label("Order within faction · drag to reorder",10,null,true));EditorTheme.Add(inspector,actions);notice.MaximumSize=new Size(260,0);notice.Font=EditorTheme.Font(8);EditorTheme.Add(inspector,notice);EditorTheme.Add(inspector,EditorTheme.Button("Assigned vehicles →",()=>groupViews.SelectedIndex=1));groupInspector.Controls.Add(inspector);groupBody.Controls.Add(groupInspector,1,0);frame.Controls.Add(groupBody,0,1);Controls.Add(frame);
            filter.Visible=false;Controls.Add(filter);top.Dispose();foreach(var c in controls)if(c.Parent==null&&c!=filter)c.Dispose();
            SizeChanged+=(s,e)=>ArrangeGroupColumns();groupViews.SelectedIndexChanged+=(s,e)=>ArrangeGroupColumns();
        }
        void ArrangeGroupColumns(){bool show=groupViews.SelectedIndex==0;groupInspector.Visible=show;groupBody.ColumnStyles[1].Width=show?(Width<850?260:310):0;var left=(TableLayoutPanel)groupViews.Parent;left.GetControlFromPosition(0,0).Visible=show;left.RowStyles[0].Height=show?72:0;}
        void RebuildFactionTabs(){if(factionTabs==null)return;while(factionTabs.Controls.Count>0)factionTabs.Controls[0].Dispose();for(int i=0;i<filter.Items.Count;i++){int index=i;var b=new EditorButton{Text=filter.Items[i].ToString(),Navigation=true,Selected=i==filter.SelectedIndex,AutoSize=true};b.Click+=(s,e)=>{filter.SelectedIndex=index;RebuildFactionTabs();};factionTabs.Controls.Add(b);}}
    }
}
