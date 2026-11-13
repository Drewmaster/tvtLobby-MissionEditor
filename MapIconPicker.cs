using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class MapIconGrid : Control
    {
        public List<MapIcon> Items=new List<MapIcon>();
        public Dictionary<string,Bitmap> Images=new Dictionary<string,Bitmap>();
        public MapIcon Selected;
        public Color Tint=Color.White;
        public bool LightBackground;
        public string EmptyText="Loading installed icon sets…";
        public event Action CapacityChanged,SelectionChanged,Accept;
        public event Action<int> PageRequested;
        readonly ToolTip tip=new ToolTip{AutoPopDelay=15000};
        int columns=1,rows=1,hover=-1;
        public int Capacity {get{return columns*rows;}}
        public MapIconGrid(){DoubleBuffered=true;TabStop=true;BackColor=EditorTheme.Background;SetStyle(ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);Font=EditorTheme.Font(9);}
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);int old=Capacity;
            float dpi=Math.Max(1f,Font.Height/15f);columns=Math.Max(1,(int)(ClientSize.Width/(160*dpi)));rows=Math.Max(1,(int)(ClientSize.Height/(140*dpi)));
            if(old!=Capacity&&CapacityChanged!=null)CapacityChanged();Invalidate();
        }
        public Rectangle CellBounds(int index)
        {
            int col=index%columns,row=index/columns;
            return Rectangle.FromLTRB(col*Width/columns+4,row*Height/rows+4,(col+1)*Width/columns-4,(row+1)*Height/rows-4);
        }
        int Hit(Point point){for(int i=0;i<Items.Count;i++)if(CellBounds(i).Contains(point))return i;return -1;}
        public void ResetHover(){hover=-1;tip.SetToolTip(this,"");}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            if(Items.Count==0){TextRenderer.DrawText(g,EmptyText,Font,ClientRectangle,EditorTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);return;}
            for(int i=0;i<Items.Count;i++)
            {
                var icon=Items[i];var rect=CellBounds(i);bool selected=Selected!=null&&MapIconCatalog.Key(Selected)==MapIconCatalog.Key(icon);
                Color background=LightBackground?Color.FromArgb(220,225,228):EditorTheme.Panel;
                using(var brush=new SolidBrush(background))g.FillRectangle(brush,rect);
                using(var pen=new Pen(selected?EditorTheme.Amber:i==hover?EditorTheme.Muted:EditorTheme.Line,selected?2:1))g.DrawRectangle(pen,rect);
                int caption=Math.Min(48,rect.Height/3);float size=Math.Max(16,Math.Min(rect.Width-28,rect.Height-caption-22));
                var center=new PointF(rect.Left+rect.Width/2f,rect.Top+(rect.Height-caption)/2f);
                Bitmap image;Images.TryGetValue(MapIconCatalog.Key(icon),out image);
                if(image!=null)MapAssets.Draw(g,image,center,size,0,Tint);
                else TextRenderer.DrawText(g,"…",Font,new Rectangle(rect.X,rect.Y,rect.Width,rect.Height-caption),LightBackground?EditorTheme.Panel:EditorTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g,icon.Name,Font,new Rectangle(rect.X+7,rect.Bottom-caption,rect.Width-14,caption-5),LightBackground?EditorTheme.Background:EditorTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int at=Hit(e.Location);if(at==hover)return;hover=at;tip.SetToolTip(this,at<0?"":Items[at].Name+"\n"+Items[at].IconResource+"\n"+Items[at].Quad);Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);ResetHover();Invalidate();}
        void Pick(int index){if(index<0||index>=Items.Count)return;Selected=Items[index];Invalidate();if(SelectionChanged!=null)SelectionChanged();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();Pick(Hit(e.Location));}
        protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(e.Button==MouseButtons.Left&&Hit(e.Location)>=0&&Accept!=null)Accept();}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||key==Keys.PageUp||key==Keys.PageDown||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);int index=Selected==null?-1:Items.FindIndex(i=>MapIconCatalog.Key(i)==MapIconCatalog.Key(Selected));
            if(e.KeyCode==Keys.PageUp||e.KeyCode==Keys.PageDown){if(PageRequested!=null)PageRequested(e.KeyCode==Keys.PageUp?-1:1);e.Handled=true;return;}
            if(e.KeyCode==Keys.Enter){if(Accept!=null)Accept();e.Handled=true;return;}
            int next=index;
            if(e.KeyCode==Keys.Left)next=index-1;else if(e.KeyCode==Keys.Right)next=index+1;else if(e.KeyCode==Keys.Up)next=index-columns;else if(e.KeyCode==Keys.Down)next=index+columns;else if(e.KeyCode==Keys.Home)next=0;else if(e.KeyCode==Keys.End)next=Items.Count-1;else return;
            Pick(Math.Max(0,Math.Min(Items.Count-1,next)));e.Handled=true;
        }
        protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
    }

    public sealed class MapIconPicker : Form
    {
        sealed class Navigation
        {
            public FlowLayoutPanel Row=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};
            public EditorButton PreviousSet=new EditorButton{Text="‹ Set"},NextSet=new EditorButton{Text="Set ›"},PreviousPage=new EditorButton{Text="‹ Page"},NextPage=new EditorButton{Text="Page ›"};
            public EditorCombo Set=new EditorCombo{Width=390,DropDownWidth=640};
            public NumericUpDown Page=new NumericUpDown{Minimum=1,Maximum=1,Value=1,Width=58,Margin=new Padding(0,5,4,0)};
            public EditorLabel Total=new EditorLabel{AutoSize=true,Text="/ 1",Margin=new Padding(2,9,12,0)};
            public Navigation(){Set.Margin=new Padding(0,3,8,0);Row.Controls.AddRange(new Control[]{PreviousSet,Set,NextSet,PreviousPage,Page,Total,NextPage});}
        }
        readonly MapReply scene;
        readonly MapAssets targetAssets,previewAssets;
        readonly string initialResource,initialQuad;
        readonly Color markerTint;
        readonly MapIconGrid grid=new MapIconGrid{Dock=DockStyle.Fill};
        readonly Navigation top=new Navigation(),bottom=new Navigation();
        readonly TextBox search=new TextBox{Width=320,Margin=new Padding(0,5,12,0)};
        readonly EditorToggle tint=new EditorToggle{Text="Preview marker color",AutoSize=true,Margin=new Padding(10,2,12,0)};
        readonly EditorToggle light=new EditorToggle{Text="Light background",AutoSize=true,Margin=new Padding(0,2,0,0)};
        readonly EditorLabel info=new EditorLabel{Dock=DockStyle.Fill,AutoSize=true,Margin=new Padding(6),Text="Loading installed icon sets…"};
        readonly EditorLabel selectedInfo=new EditorLabel{Dock=DockStyle.Fill,AutoSize=false,AutoEllipsis=true,Text="Choose a symbol. Double-click or press Enter to use it.",TextAlign=ContentAlignment.MiddleLeft};
        readonly EditorButton choose=new EditorButton{Text="Use selected symbol",Primary=true,Enabled=false};
        List<MapIconSet> sets=new List<MapIconSet>();
        List<MapIcon> filtered=new List<MapIcon>();
        Task initialization,pending;
        bool ready,sync,pumping;
        volatile bool closing;
        int setIndex,page;
        volatile int generation;
        public MapIcon Selected;
        public MapIconPicker(MapReply scene,MapAssets assets,string color="1 1 1 1",string resource="",string quad="")
        {
            this.scene=scene;targetAssets=assets;previewAssets=new MapAssets(scene);markerTint=MapCanvas.Rgba(color,Color.White);initialResource=resource;initialQuad=quad;
            Text="Map symbols · installed game catalog";StartPosition=FormStartPosition.CenterParent;MinimumSize=new Size(960,640);
            var screen=Screen.PrimaryScreen.WorkingArea;Size=new Size(Math.Min(1440,screen.Width-50),Math.Min(960,screen.Height-70));
            Font=EditorTheme.Font(9);BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;AutoScaleMode=AutoScaleMode.Dpi;
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(12)};
            frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,60));
            var find=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};find.Controls.Add(new EditorLabel{Text="Search this set",AutoSize=true,Margin=new Padding(0,9,10,0)});find.Controls.Add(search);find.Controls.Add(EditorTheme.Button("Clear",()=>search.Clear()));find.Controls.Add(tint);find.Controls.Add(light);
            frame.Controls.Add(find,0,0);frame.Controls.Add(top.Row,0,1);frame.Controls.Add(info,0,2);frame.Controls.Add(grid,0,3);frame.Controls.Add(bottom.Row,0,4);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var cancel=new EditorButton{Text="Cancel",DialogResult=DialogResult.Cancel,Anchor=AnchorStyles.Right};choose.Anchor=AnchorStyles.Right;
            footer.Controls.Add(selectedInfo,0,0);footer.Controls.Add(cancel,1,0);footer.Controls.Add(choose,2,0);frame.Controls.Add(footer,0,5);Controls.Add(frame);CancelButton=cancel;
            foreach(var nav in new[]{top,bottom})
            {
                Navigation bar=nav;bar.Set.SelectedIndexChanged+=(s,e)=>{if(!sync)ChangeSet(bar.Set.SelectedIndex);};
                bar.PreviousSet.Click+=(s,e)=>ChangeSet(setIndex-1);bar.NextSet.Click+=(s,e)=>ChangeSet(setIndex+1);
                bar.PreviousPage.Click+=(s,e)=>ChangePage(page-1);bar.NextPage.Click+=(s,e)=>ChangePage(page+1);
                bar.Page.ValueChanged+=(s,e)=>{if(!sync)ChangePage((int)bar.Page.Value-1);};
            }
            search.TextChanged+=(s,e)=>{if(ready&&!sync){page=0;grid.Selected=null;Filter();RenderPage();}};
            tint.CheckedChanged+=(s,e)=>{grid.Tint=tint.Checked?markerTint:Color.White;grid.Invalidate();};
            light.CheckedChanged+=(s,e)=>{grid.LightBackground=light.Checked;grid.Invalidate();};
            grid.CapacityChanged+=()=>{if(!ready)return;int at=grid.Selected==null?-1:filtered.FindIndex(i=>MapIconCatalog.Key(i)==MapIconCatalog.Key(grid.Selected));if(at>=0)page=at/grid.Capacity;RenderPage();};
            grid.SelectionChanged+=UpdateSelection;grid.Accept+=AcceptSelection;grid.PageRequested+=delta=>ChangePage(page+delta);choose.Click+=(s,e)=>AcceptSelection();
            EditorTheme.Apply(this);UpdateNavigation();Shown+=async(s,e)=>await InitializeAsync();
        }
        public Task InitializeAsync(){if(initialization==null){initialization=InitializeCore();pending=initialization;}return initialization;}
        async Task InitializeCore()
        {
            try
            {
                sets=await Task.Run(()=>MapIconCatalog.Read(scene,previewAssets,()=>closing));if(closing)return;
                sync=true;foreach(var nav in new[]{top,bottom})nav.Set.Items.AddRange(sets.Cast<object>().ToArray());sync=false;ready=true;
                int initial=String.IsNullOrEmpty(initialResource)?0:sets.FindIndex(s=>!String.IsNullOrEmpty(s.Resource)&&MapAssets.Relative(s.Resource).Equals(MapAssets.Relative(initialResource),StringComparison.OrdinalIgnoreCase));
                ChangeSet(Math.Max(0,initial));
                if(!String.IsNullOrEmpty(initialQuad)){grid.Selected=filtered.FirstOrDefault(i=>i.Quad==initialQuad);int at=filtered.IndexOf(grid.Selected);if(at>=0)page=at/grid.Capacity;RenderPage();}
            }
            catch(Exception error){if(!closing){info.Text="Cannot load icon catalog: "+error.Message;grid.EmptyText=info.Text;grid.Invalidate();}}
        }
        void ChangeSet(int index)
        {
            if(!ready||index<0||index>=sets.Count)return;
            setIndex=index;page=0;grid.Selected=null;sync=true;search.Clear();sync=false;Filter();RenderPage();
        }
        void Filter(){filtered=sets[setIndex].Icons.Where(i=>(i.Name??"").IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0||(i.Quad??"").IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0).ToList();}
        int Pages {get{return Math.Max(1,(filtered.Count+grid.Capacity-1)/grid.Capacity);}}
        void ChangePage(int value){if(!ready)return;int next=Math.Max(0,Math.Min(Pages-1,value));if(next==page)return;page=next;grid.Selected=null;RenderPage();}
        void UpdateNavigation()
        {
            sync=true;foreach(var nav in new[]{top,bottom})
            {
                nav.Set.Enabled=ready;nav.PreviousSet.Enabled=ready&&setIndex>0;nav.NextSet.Enabled=ready&&setIndex<sets.Count-1;
                nav.PreviousPage.Enabled=ready&&page>0;nav.NextPage.Enabled=ready&&page<Pages-1;
                nav.Page.Enabled=ready&&Pages>1;nav.Page.Maximum=Pages;nav.Page.Value=page+1;nav.Total.Text="/ "+Pages;
                if(ready)nav.Set.SelectedIndex=setIndex;
            }
            search.Enabled=ready;sync=false;
        }
        void RenderPage()
        {
            if(!ready||closing)return;page=Math.Max(0,Math.Min(Pages-1,page));
            grid.Items=filtered.Skip(page*grid.Capacity).Take(grid.Capacity).ToList();grid.ResetHover();
            grid.EmptyText=String.IsNullOrEmpty(sets[setIndex].Error)?"No symbols match your search.":"This imageset is unavailable.\n"+sets[setIndex].Error;
            if(grid.Selected!=null&&!grid.Items.Any(i=>MapIconCatalog.Key(i)==MapIconCatalog.Key(grid.Selected)))grid.Selected=null;
            info.Text=(String.IsNullOrEmpty(sets[setIndex].Resource)?"Map symbols configured for this mission":sets[setIndex].Resource)+"\n"+(filtered.Count==0?"0 symbols":(page*grid.Capacity+1)+"–"+(page*grid.Capacity+grid.Items.Count)+" of "+filtered.Count+" symbols");
            UpdateNavigation();UpdateSelection();grid.Invalidate();generation++;if(!pumping){pumping=true;pending=LoadPages();}
        }
        async Task LoadPages()
        {
            try
            {
                while(!closing)
                {
                    int version=generation;var pageIcons=grid.Items.ToArray();
                    var decoded=await Task.Run(()=>{
                        var result=new Dictionary<string,Bitmap>();
                        foreach(var icon in pageIcons){if(closing||version!=generation)break;result[MapIconCatalog.Key(icon)]=previewAssets.Get(icon.IconResource,icon.Quad);}
                        return result;
                    });
                    if(closing)return;
                    foreach(var image in decoded)grid.Images[image.Key]=image.Value;
                    if(version!=generation)continue;
                    UpdateSelection();grid.Invalidate();return;
                }
            }
            catch(Exception error){if(!closing)info.Text="Could not load symbol previews: "+error.Message;}
            finally{pumping=false;}
        }
        void UpdateSelection()
        {
            Bitmap image=null;if(grid.Selected!=null)grid.Images.TryGetValue(MapIconCatalog.Key(grid.Selected),out image);
            choose.Enabled=image!=null;
            selectedInfo.Text=grid.Selected==null?"Choose a symbol. Double-click or press Enter to use it.":grid.Selected.Quad+"\n"+grid.Selected.IconResource+(image==null?" · Preview unavailable or loading":"");
        }
        void AcceptSelection()
        {
            if(!choose.Enabled||grid.Selected==null)return;
            // The converter output is already cached by the preview. Populate the
            // owner's cache before returning so the selected marker appears at once.
            if(targetAssets!=null&&targetAssets.Get(grid.Selected.IconResource,grid.Selected.Quad)==null){info.Text=targetAssets.Error(grid.Selected.IconResource,grid.Selected.Quad);return;}
            Selected=grid.Selected;DialogResult=DialogResult.OK;Close();
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!closing){closing=true;generation++;if(pending!=null&&!pending.IsCompleted)pending.ContinueWith(t=>previewAssets.Dispose(),TaskScheduler.Default);else previewAssets.Dispose();}
            base.Dispose(disposing);
        }
    }
}
