using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class MapCanvas : Control
    {
        public readonly MapProjection Projection = new MapProjection();
        public MapReply Scene;
        public MapAssets Assets;
        public MapTopology Topology;
        public bool ShowGroups=true,ShowVehicles=true,ShowDetails=true,ShowAutomatic,PlayerPreview;
        public string FactionFilter="";
        public int PreviewPhase=16;
        public bool PreviewFreeze;
        public MapRow Draft;
        public bool Drawing;
        public bool Locked;
        public int SelectedVertex = -1;
        public event Action Changed;
        public event Action<string> Picked;
        public event Action<string> PositionChanged;
        private Bitmap terrain;
        private string terrainPath;
        private bool panning, moving;
        private int dragVertex = -1;
        private Point previous;
        private MapPoint lastWorld;
        public MapCanvas()
        {
            DoubleBuffered = true; BackColor = Color.FromArgb(23, 33, 32); TabStop = true;
            SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        }
        public void SetScene(MapReply scene, bool fit)
        {
            Scene = scene;
            if(String.IsNullOrEmpty(scene.Background))
            {if(terrain!=null)terrain.Dispose();terrain=null;terrainPath=null;}
            if (!String.IsNullOrEmpty(scene.Background) && scene.Background != terrainPath)
            {
                using (var loaded = Image.FromFile(scene.Background)) { var old = terrain; terrain = new Bitmap(loaded); if (old != null) old.Dispose(); }
                terrainPath = scene.Background;
            }
            if (fit) Fit(); Invalidate();
        }
        public void Fit()
        {
            Projection.Viewport = ClientSize;
            if (Scene != null) Projection.Fit(Scene.MinX, Scene.MinZ, Scene.MaxX, Scene.MaxZ);
            Invalidate();
        }
        public void FocusDraft()
        {
            if (Draft == null || Draft.Points.Count == 0) return;
            double x = Draft.Points.Average(p => p.X), z = Draft.Points.Average(p => p.Z);
            double span = Draft.Kind=="FreezeCircle" ? Math.Max(80,Draft.Radius*2.8) : Math.Max(80, Math.Max(Draft.Points.Max(p => p.X) - Draft.Points.Min(p => p.X), Draft.Points.Max(p => p.Z) - Draft.Points.Min(p => p.Z)) * 1.4);
            Projection.Fit(x - span / 2, z - span / 2, x + span / 2, z + span / 2); Invalidate();
        }
        public static Color Rgba(string text, Color fallback)
        {
            var bits = (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length != 4) return fallback;
            double[] c = new double[4];
            for (int i = 0; i < 4; i++) if (!Double.TryParse(bits[i], NumberStyles.Float, CultureInfo.InvariantCulture, out c[i]) || Double.IsNaN(c[i]) || c[i] < 0 || c[i] > 1) return fallback;
            return Color.FromArgb((int)(c[3]*255), (int)(c[0]*255), (int)(c[1]*255), (int)(c[2]*255));
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Projection.Viewport = ClientSize; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Scene != null && terrain != null)
            {
                PointF a = Projection.Screen(Scene.MinX, Scene.MaxZ), b = Projection.Screen(Scene.MaxX, Scene.MinZ);
                g.DrawImage(terrain, new RectangleF(a.X,a.Y,b.X-a.X,b.Y-a.Y));
            }
            if(ShowDetails&&Topology!=null)Topology.Draw(g,Projection);
            double step = Math.Pow(10, Math.Ceiling(Math.Log10(90 / Projection.PixelsPerMetre)));
            MapPoint nw = Projection.World(PointF.Empty), se = Projection.World(new PointF(Width, Height));
            using (var pen = new Pen(Color.FromArgb(65, 190, 220, 210)))
            {
                for (double x=Math.Ceiling(nw.X/step)*step; x<se.X; x+=step) { float sx=Projection.Screen(x,0).X; g.DrawLine(pen,sx,0,sx,Height); g.DrawString(x.ToString("0"),Font,Brushes.White,sx+2,4); }
                for (double z=Math.Ceiling(se.Z/step)*step; z<nw.Z; z+=step) { float sy=Projection.Screen(0,z).Y; g.DrawLine(pen,0,sy,Width,sy); g.DrawString(z.ToString("0"),Font,Brushes.White,4,sy+2); }
            }
            if(Scene!=null&&ShowAutomatic&&!PlayerPreview)DrawAutomatic(g);
            var drawRows=Scene==null?new System.Collections.Generic.List<MapRow>():Scene.Rows.Select(r=>Draft!=null&&r.Key==Draft.Key?Draft:r).ToList();
            if(Draft!=null&&String.IsNullOrEmpty(Draft.Key))drawRows.Add(Draft);
            foreach(var row in MapOrder.FrontToBack(drawRows).AsEnumerable().Reverse())DrawRow(g,row,row==Draft);
            g.DrawString("N ↑   ·   metres",Font,Brushes.White,12,Height-28);
        }
        bool VisibleRow(MapRow row)
        {
            if(row.Kind=="Group"||row.Kind=="Vehicle")return !PlayerPreview&&(row.Kind=="Group"?ShowGroups:ShowVehicles)&&(FactionFilter==""||row.EngineKey==FactionFilter);
            if(!PlayerPreview)return true;
            if(row.Kind=="FreezeCircle"||((row.Kind=="Zone"||row.Kind=="Boundary")&&!row.ShowMap))return false;
            if(!row.Enabled||!row.InitiallyVisible||(row.Phases&PreviewPhase)==0)return false;
            if(PreviewPhase==32&&!(PreviewFreeze?row.DuringFreeze:row.AfterFreeze))return false;
            if(row.Audience==0)return true;if(row.Audience==1)return FactionFilter=="";
            var ids=(row.AudienceIds??"").Split(',').Select(x=>x.Trim());
            if(row.Audience==2)return ids.Contains(FactionFilter);
            var faction=Scene.Forces==null?null:Scene.Forces.Factions.FirstOrDefault(f=>f.Key==FactionFilter);
            if(row.Audience==4)return faction!=null&&row.Subjects.Any(s=>s.Kind==0&&s.Id==faction.Key||s.Kind==1&&s.Id==faction.Side||s.Kind==2&&s.Id==faction.Alliance);
            return row.Audience==3&&faction!=null&&ids.Contains(faction.Alliance);
        }
        void DrawAutomatic(Graphics g)
        {
            var settings=Scene.FreezeZones;if(settings==null||(settings.Mode!=1&&settings.Mode!=3))return;
            foreach(var group in Scene.Rows.Where(r=>r.Kind=="Group"&&r.Points.Count>0&&(FactionFilter==""||r.EngineKey==FactionFilter)))
            {
                var meta=Scene.Forces==null?null:Scene.Forces.Groups.FirstOrDefault(r=>r.Key==group.EntityKey);if(meta==null||meta.Playable==0)continue;
                var coverage=settings.Coverage.FirstOrDefault(r=>r.Group==group.GroupRef||r.Group==group.Name||r.Group=="@"+meta.Key);
                string assigned=coverage==null?"":coverage.Zone;if(assigned=="-"||(settings.Mode==3&&!String.IsNullOrEmpty(assigned)))continue;
                double radius=(double)(coverage!=null&&coverage.Radius>0?coverage.Radius:settings.Radius);
                DrawBasicRow(g,new MapRow{Kind="FreezeCircle",Points=group.Points,Radius=radius,WarnRadius=Math.Max(0,radius-(double)settings.WarningBand),Title="",Stroke="0.4 0.8 1 0.6"},false);
            }
        }
        private void DrawRow(Graphics g,MapRow row,bool selected)
        {
            if(!VisibleRow(row)||row.Points.Count==0)return;
            if(row.Kind=="FreezeCircle"){DrawBasicRow(g,row,selected);return;}
            PointF[] points=row.Points.Select(p=>Projection.Screen(p.X,p.Z)).ToArray();
            var style=row.Style;bool icon=row.Kind=="Icon"||row.Kind=="Group"||row.Kind=="Vehicle";
            Color stroke=Rgba(row.Stroke,Color.Gold),fill=Rgba(row.Fill,Color.Transparent);
            PointF labelPoint=points[0];float iconHeight=0,iconWidth=0;
            if(icon)
            {
                var entry=Scene==null||row.Kind!="Icon"?null:Scene.Icons.FirstOrDefault(i=>i.Index==row.Icon);
                string resource=style!=null&&!String.IsNullOrEmpty(style.ImageSet)?style.ImageSet:entry==null?"":entry.IconResource;
                string quad=style!=null&&!String.IsNullOrEmpty(style.ImageSet)?style.Quad:entry==null?"":entry.Quad;
                Bitmap bitmap=Assets==null?null:Assets.Cached(resource,quad);
                float size=style==null?(float)(56*row.Scale):(float)(style.SizeMode==0?style.PixelSize:Math.Max(style.MinPixels,Math.Min(style.MaxPixels,style.WorldSize*Projection.PixelsPerMetre)));
                Color tint=style==null?(Scene!=null&&row.ColorIndex<Scene.Colors.Count?Rgba(Scene.Colors[row.ColorIndex],Color.White):Color.White):Rgba(style.Tint,Color.White);
                if(bitmap!=null){MapAssets.Draw(g,bitmap,points[0],size,row.Rotation,tint);iconHeight=size*bitmap.Height/Math.Max(bitmap.Width,bitmap.Height);iconWidth=size*bitmap.Width/Math.Max(bitmap.Width,bitmap.Height);}
                else if(row.Kind=="Group"||row.Kind=="Vehicle") {g.FillRectangle(Brushes.DarkSlateGray,points[0].X-10,points[0].Y-10,20,20);g.DrawRectangle(Pens.LightSkyBlue,points[0].X-10,points[0].Y-10,20,20);g.DrawString(row.Kind=="Group"?"G":"V",Font,Brushes.White,points[0].X-7,points[0].Y-8);iconHeight=20;}
                else {g.DrawRectangle(Pens.OrangeRed,points[0].X-8,points[0].Y-8,16,16);g.DrawString("?",Font,Brushes.OrangeRed,points[0].X-5,points[0].Y-8);iconHeight=16;}
            }
            else if(points.Length>1)
            {
                bool closed=row.Kind!="Line"&&points.Length>2;
                float appearanceScale=(float)MapStyle.ShapeScale(style,Projection.PixelsPerMetre);
                float strokeWidth=Math.Max(.01f,(float)row.Width*appearanceScale);
                using(var path=new GraphicsPath(FillMode.Alternate))
                {
                    if(closed)
                    {
                        if(style!=null&&style.FillOutside!=0)path.AddRectangle(new RectangleF(-1,-1,Width+2,Height+2));path.AddPolygon(points);
                        int pattern=style==null?1:style.FillPattern;
                        if(pattern==1)using(var brush=new SolidBrush(fill))g.FillPath(brush,path);
                        else if(pattern==2)
                        {
                            var saved=g.Save();g.SetClip(path);
                            float step=(float)Math.Max(1,appearanceScale/Math.Max(.001,style.TextureScale));
                            float start=style.ShapeSizeMode==0?0:points[0].Y-(float)Math.Floor(points[0].Y/step)*step;
                            using(var pen=new Pen(fill,Math.Max(.01f,appearanceScale)))for(float y=start;y<Height;y+=step)g.DrawLine(pen,0,y,Width,y);g.Restore(saved);
                        }
                        else if(pattern==3&&Assets!=null)
                        {
                            var bitmap=Assets.Tinted(style.FillTexture,fill);if(bitmap!=null)using(var brush=new TextureBrush(bitmap)){float scale=(float)(appearanceScale/(style.TextureScale*Math.Max(bitmap.Width,bitmap.Height)));brush.ScaleTransform(scale,scale);g.FillPath(brush,path);}
                        }
                    }
                    using(var pen=new Pen(stroke,strokeWidth))
                    {
                        if(style!=null){if(style.StrokePattern==1&&18*appearanceScale>=2)pen.DashPattern=new[]{12*appearanceScale/strokeWidth,6*appearanceScale/strokeWidth};if(style.StrokePattern==2&&7*appearanceScale>=2)pen.DashPattern=new[]{2*appearanceScale/strokeWidth,5*appearanceScale/strokeWidth};}
                        if(style!=null&&style.StrokePattern==3&&Assets!=null&&Assets.Cached(style.StrokeTexture)!=null)
                        {
                            var bitmap=Assets.Tinted(style.StrokeTexture,stroke);
                            using(var brush=new TextureBrush(bitmap))
                            {
                                float scale=(float)(appearanceScale/(style.TextureScale*Math.Max(bitmap.Width,bitmap.Height)));
                                brush.ScaleTransform(scale,scale);
                                using(var textured=new Pen(brush,strokeWidth)){if(closed)g.DrawPolygon(textured,points);else g.DrawLines(textured,points);}
                            }
                        }
                        else if(closed)g.DrawPolygon(pen,points);else g.DrawLines(pen,points);
                    }
                }
                labelPoint=new PointF((points.Min(p=>p.X)+points.Max(p=>p.X))/2,(points.Min(p=>p.Y)+points.Max(p=>p.Y))/2);
            }
            if(style==null||style.LabelVisible!=0)
            {
                string text=(style!=null&&!String.IsNullOrEmpty(style.LabelText)?style.LabelText:String.IsNullOrEmpty(row.Title)?row.Name:row.Title)??"";
                using(var font=new Font(Font.FontFamily,style==null?16:(float)style.LabelSize,FontStyle.Regular,GraphicsUnit.Pixel))
                {
                    SizeF size=g.MeasureString(text,font);float x=labelPoint.X-size.Width/2,y=labelPoint.Y-size.Height/2;
                    if(icon){int pos=style==null?1:style.LabelPosition;if(pos==0)y=labelPoint.Y-iconHeight/2-size.Height;if(pos==1)y=labelPoint.Y+iconHeight/2;if(pos==2)x=labelPoint.X-iconWidth/2-size.Width-4;if(pos==3)x=labelPoint.X+iconWidth/2+4;}
                    if(style!=null){x+=(float)style.LabelX;y+=(float)style.LabelY;}else if(icon)y+=2;
                    using(var brush=new SolidBrush(style==null?Color.White:Rgba(style.LabelColor,Color.White)))g.DrawString(text,font,brush,x,y);
                }
            }
            if(selected&&!PlayerPreview)foreach(var point in points){g.DrawRectangle(Pens.Gold,point.X-6,point.Y-6,12,12);}
        }
        private void DrawBasicRow(Graphics g, MapRow row, bool selected)
        {
            PointF[] pts=row.Points.Select(p=>Projection.Screen(p.X,p.Z)).ToArray(); if (pts.Length==0) return;
            Color color=selected?Color.FromArgb(255,235,185):Rgba(row.Stroke,Color.Gold);
            using (var pen=new Pen(color, selected?3:Math.Max(1, Math.Min(8,(float)row.Width))))
            {
                if (row.Kind=="FreezeCircle")
                {
                    float radius=(float)(Math.Max(0,Math.Min(5000,row.Radius))*Projection.PixelsPerMetre);
                    float warning=(float)(Math.Max(0,Math.Min(row.Radius,row.WarnRadius))*Projection.PixelsPerMetre);
                    if(radius>0) { using(var fill=new SolidBrush(Color.FromArgb(25,color)))g.FillEllipse(fill,pts[0].X-radius,pts[0].Y-radius,radius*2,radius*2);g.DrawEllipse(pen,pts[0].X-radius,pts[0].Y-radius,radius*2,radius*2); }
                    pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;
                    if(warning>0)g.DrawEllipse(pen,pts[0].X-warning,pts[0].Y-warning,warning*2,warning*2);
                }
                else if (row.Kind=="Icon")
                {
                    float r=10; g.DrawEllipse(pen,pts[0].X-r,pts[0].Y-r,r*2,r*2);g.DrawLine(pen,pts[0].X-r,pts[0].Y,pts[0].X+r,pts[0].Y);g.DrawLine(pen,pts[0].X,pts[0].Y-r,pts[0].X,pts[0].Y+r);
                }
                else if (pts.Length>1)
                {
                    if (row.Kind!="Line" && pts.Length>2)
                    {
                        using (var fill=new SolidBrush(Rgba(row.Fill,Color.FromArgb(35,220,160,70)))) g.FillPolygon(fill,pts);
                        g.DrawPolygon(pen,pts);
                    }
                    else g.DrawLines(pen,pts);
                }
            }
            string title=(String.IsNullOrEmpty(row.Title)?row.Name:row.Title)??"";
            SizeF size=g.MeasureString(title,Font); using (var bg=new SolidBrush(Color.FromArgb(190,15,20,20))) g.FillRectangle(bg,pts[0].X+12,pts[0].Y-10,size.Width+6,size.Height+4);
            g.DrawString(title,Font,Brushes.White,pts[0].X+15,pts[0].Y-8);
            if (selected) for (int i=0;i<pts.Length;i++)
            {
                g.FillRectangle(i==SelectedVertex?Brushes.White:Brushes.Goldenrod,pts[i].X-5,pts[i].Y-5,10,10);
                g.DrawString((i+1).ToString(),Font,Brushes.White,pts[i].X+7,pts[i].Y+4);
            }
        }
        private static double Distance(PointF a,PointF b) { return Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)); }
        private int VertexAt(Point point)
        {
            if (Draft==null) return -1;
            for (int i=0;i<Draft.Points.Count;i++) if (Distance(Projection.Screen(Draft.Points[i].X,Draft.Points[i].Z),point)<12) return i;
            return -1;
        }
        private void NotifyChange() { Invalidate(); if (Changed!=null) Changed(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);Focus();previous=e.Location;lastWorld=Projection.World(e.Location);
            if(e.Button==MouseButtons.Right||e.Button==MouseButtons.Middle) { panning=true;Capture=true;return; }
            if(e.Button!=MouseButtons.Left) return;
            if(Draft!=null&&!Locked)
            {
                if(Drawing)
                {
                    if(e.Clicks>1) { Drawing=false;NotifyChange();return; }
                    if(Draft.Points.Count>=128) return;
                    if(Draft.Kind=="Icon"||Draft.Kind=="FreezeCircle") {Draft.Points.Clear();Drawing=false;}
                    Draft.Points.Add(lastWorld);SelectedVertex=Draft.Points.Count-1;NotifyChange();return;
                }
                dragVertex=VertexAt(e.Location);SelectedVertex=dragVertex;
                if(dragVertex>=0) {Capture=true;Invalidate();return;}
                if((ModifierKeys&Keys.Shift)!=0) {moving=true;Capture=true;return;}
                if((ModifierKeys&Keys.Control)!=0&&Draft.Kind!="Icon"&&Draft.Kind!="FreezeCircle") {InsertVertex(e.Location);return;}
            }
            if(Scene!=null&&Picked!=null)
            {
                MapRow nearest=null;double distance=18;
                foreach(var row in MapOrder.FrontToBack(Scene.Rows.Where(VisibleRow))) foreach(var point in row.Points)
                {double d=Distance(Projection.Screen(point.X,point.Z),e.Location);if(d<distance){distance=d;nearest=row;}}
                if(nearest!=null) Picked(nearest.Key);
            }
        }
        private void InsertVertex(Point point)
        {
            int insert=-1;double best=20;MapPoint candidate=null;
            int edges=Draft.Kind=="Line"?Draft.Points.Count-1:Draft.Points.Count;
            for(int i=0;i<edges;i++)
            {
                MapPoint a=Draft.Points[i],b=Draft.Points[(i+1)%Draft.Points.Count];PointF sa=Projection.Screen(a.X,a.Z),sb=Projection.Screen(b.X,b.Z);
                double dx=sb.X-sa.X,dy=sb.Y-sa.Y,len=dx*dx+dy*dy;if(len<.01)continue;
                double t=Math.Max(0,Math.Min(1,((point.X-sa.X)*dx+(point.Y-sa.Y)*dy)/len));
                double d=Distance(point,new PointF((float)(sa.X+t*dx),(float)(sa.Y+t*dy)));
                if(d<best){best=d;insert=i+1;candidate=new MapPoint{X=a.X+t*(b.X-a.X),Y=a.Y+t*(b.Y-a.Y),Z=a.Z+t*(b.Z-a.Z)};}
            }
            if(insert>=0&&Draft.Points.Count<128){Draft.Points.Insert(insert,candidate);SelectedVertex=insert;NotifyChange();}
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);MapPoint world=Projection.World(e.Location);
            if(PositionChanged!=null) PositionChanged(String.Format(CultureInfo.InvariantCulture,"X {0:0.00} · Z {1:0.00}",world.X,world.Z));
            if(panning){Projection.CenterX-=(e.X-previous.X)/Projection.PixelsPerMetre;Projection.CenterZ+=(e.Y-previous.Y)/Projection.PixelsPerMetre;Invalidate();}
            else if(Draft!=null&&!Locked&&dragVertex>=0){Draft.Points[dragVertex].X=world.X;Draft.Points[dragVertex].Z=world.Z;NotifyChange();}
            else if(Draft!=null&&!Locked&&moving){foreach(var p in Draft.Points){p.X+=world.X-lastWorld.X;p.Z+=world.Z-lastWorld.Z;}NotifyChange();}
            previous=e.Location;lastWorld=world;
        }
        protected override void OnMouseUp(MouseEventArgs e) {base.OnMouseUp(e);panning=moving=false;dragVertex=-1;Capture=false;}
        protected override void OnMouseCaptureChanged(EventArgs e) {base.OnMouseCaptureChanged(e);if(!Capture){panning=moving=false;dragVertex=-1;}}
        protected override void OnMouseWheel(MouseEventArgs e) {Projection.Zoom(e.Location,Math.Pow(1.2,e.Delta/120.0));Invalidate();}
        protected override bool IsInputKey(Keys key) {return key==Keys.Delete||key==Keys.Enter||key==Keys.Escape||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(e.KeyCode==Keys.Enter){Drawing=false;Invalidate();}
            if(e.KeyCode==Keys.Escape){Drawing=false;panning=moving=false;dragVertex=-1;Capture=false;Invalidate();}
            if(e.KeyCode==Keys.Delete&&!Locked&&Draft!=null&&SelectedVertex>=0)
            {Draft.Points.RemoveAt(SelectedVertex);SelectedVertex=-1;NotifyChange();}
        }
        protected override void Dispose(bool disposing) {if(disposing&&terrain!=null)terrain.Dispose();base.Dispose(disposing);}
    }
}
