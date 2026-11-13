using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;

namespace TvtLobby.MissionEditor
{
    // TOPO stores terrain-relative X and a north-origin Y (opposite world Z).
    // Convert once to world X/Z using the same terrain bounds as the raster.
    // Never regenerate a full terrain or estimate buildings from AABBs.
    public sealed class MapTopology : IDisposable
    {
        sealed class Feature : IDisposable
        {
            public int Kind,Type; // 0 road mesh, 1 building/wall outline, 2 power line
            public RectangleF Bounds;
            public GraphicsPath Fill,Edges,Line;
            public void Dispose(){if(Fill!=null)Fill.Dispose();if(Edges!=null)Edges.Dispose();if(Line!=null)Line.Dispose();}
        }
        readonly List<Feature> features=new List<Feature>();
        readonly double minX,maxZ;
        MapTopology(double minX,double maxZ){this.minX=minX;this.maxZ=maxZ;}
        public int Roads {get;private set;}
        public int Buildings {get;private set;}
        public int Walls {get;private set;}
        public int PowerLines {get;private set;}
        public string Summary {get{return Roads+" roads · "+Buildings+" buildings · "+Walls+" walls · "+PowerLines+" power lines";}}
        public static MapTopology Load(MapReply scene,MapAssets assets)
        {
            if(String.IsNullOrEmpty(scene.GeometryResource))return null;
            return Read(MapArchive.Read(assets.Resolve(scene.GeometryResource)),scene.MinX,scene.MaxZ);
        }
        // The chunk sizes and absolute end offsets are both validated. Only known
        // chunk versions are decoded; unsupported detail does not break the map UI.
        public static MapTopology Read(byte[] bytes,double minX,double maxZ)
        {
            var result=new MapTopology(minX,maxZ);
            try
            {
                using(var input=new MemoryStream(bytes,false))
                using(var r=new BinaryReader(input,Encoding.UTF8))
                {
                    if(bytes.Length<24||Tag(r)!="TOPO"||r.ReadInt32()!=1)throw new InvalidDataException("Unsupported TOPO header.");
                    int length=r.ReadInt32(),end=r.ReadInt32(),reserved=r.ReadInt32();
                    if(length!=bytes.Length-24||end!=bytes.Length-4||reserved!=0)throw new InvalidDataException("Invalid TOPO bounds.");
                    var chunks=new HashSet<string>();
                    while(input.Position<end)
                    {
                        long start=input.Position;
                        if(end-start<20)throw new InvalidDataException("Truncated TOPO chunk.");
                        string tag=Tag(r);int version=r.ReadInt32(),size=r.ReadInt32(),stop=r.ReadInt32();r.ReadInt32();
                        if(size<0||stop!=start+20L+size||stop>end||!chunks.Add(tag))throw new InvalidDataException("Invalid TOPO chunk bounds.");
                        if(tag=="ROAD")result.ReadRoads(r,stop,version);
                        else if(tag=="BULD")result.ReadBuildings(r,stop,version);
                        else if(tag=="PWLN")result.ReadPower(r,stop,version);
                        else input.Position=stop; // Areas/water/relief are already present in the raster.
                        if(input.Position!=stop)throw new InvalidDataException("Unexpected "+tag+" payload size.");
                    }
                    if(Tag(r)!="_EOF")throw new InvalidDataException("Invalid TOPO footer.");
                }
                return result;
            }
            catch{result.Dispose();throw;}
        }
        static string Tag(BinaryReader r){return Encoding.ASCII.GetString(r.ReadBytes(4));}
        static int Count(BinaryReader r,long end,int maximum)
        {
            if(end-r.BaseStream.Position<4)throw new InvalidDataException("Truncated map geometry count.");
            int value=r.ReadInt32();if(value<0||value>maximum)throw new InvalidDataException("Map geometry count out of range.");return value;
        }
        static void Skip(BinaryReader r,long end,long bytes)
        {if(bytes<0||bytes>end-r.BaseStream.Position)throw new InvalidDataException("Truncated map geometry.");r.BaseStream.Position+=bytes;}
        PointF[] Points(BinaryReader r,long end,bool retain)
        {
            int count=Count(r,end,2000000);
            if(count*8L>end-r.BaseStream.Position)throw new InvalidDataException("Truncated map vertices.");
            if(!retain){Skip(r,end,count*8L);return null;}
            var points=new PointF[count];
            for(int i=0;i<count;i++)
            {
                float x=r.ReadSingle(),z=r.ReadSingle();
                if(!(x>=-1000000&&x<=1000000&&z>=-1000000&&z<=1000000))throw new InvalidDataException("Invalid map coordinates.");
                points[i]=new PointF((float)(minX+x),(float)(maxZ-z));
            }
            return points;
        }
        static RectangleF Bounds(PointF[] p)
        {
            float minX=p[0].X,maxX=minX,minZ=p[0].Y,maxZ=minZ;
            foreach(var q in p){minX=Math.Min(minX,q.X);maxX=Math.Max(maxX,q.X);minZ=Math.Min(minZ,q.Y);maxZ=Math.Max(maxZ,q.Y);}
            return RectangleF.FromLTRB(minX,minZ,maxX,maxZ);
        }
        void ReadRoads(BinaryReader r,long end,int version)
        {
            if(version!=2)throw new InvalidDataException("Unsupported ROAD version: "+version);
            int roadCount=0;for(int t=0;t<8;t++)roadCount+=Count(r,end,100000);
            int lods=Count(r,end,16);
            for(int lod=0;lod<lods;lod++)
            {
                int count=Count(r,end,100000);if(count!=roadCount)throw new InvalidDataException("Road counts differ between LODs.");
                for(int n=0;n<count;n++)
                {
                    if(r.BaseStream.Position>=end)throw new InvalidDataException("Missing road type.");
                    int type=r.ReadByte();if(type>7)throw new InvalidDataException("Unknown road type.");
                    var p=Points(r,end,lod==0);
                    int joins=Count(r,end,1000000);Skip(r,end,joins*4L);
                    if(lod!=0||p.Length==0)continue;
                    if(p.Length%4!=0)throw new InvalidDataException("Road mesh is not a quad list.");
                    var f=new Feature{Kind=0,Type=type,Bounds=Bounds(p),Fill=new GraphicsPath(FillMode.Winding),Edges=new GraphicsPath(),Line=new GraphicsPath()};features.Add(f);
                    PointF previous=PointF.Empty;
                    for(int i=0;i<p.Length;i+=4)
                    {
                        f.Fill.AddPolygon(new[]{p[i],p[i+1],p[i+2],p[i+3]});
                        f.Edges.StartFigure();f.Edges.AddLine(p[i],p[i+3]);f.Edges.StartFigure();f.Edges.AddLine(p[i+1],p[i+2]);
                        var a=new PointF((p[i].X+p[i+1].X)/2,(p[i].Y+p[i+1].Y)/2);
                        var b=new PointF((p[i+2].X+p[i+3].X)/2,(p[i+2].Y+p[i+3].Y)/2);
                        if(i==0||Math.Abs(previous.X-a.X)>.02||Math.Abs(previous.Y-a.Y)>.02)f.Line.StartFigure();
                        f.Line.AddLine(a,b);previous=b;
                    }
                    Roads++;
                }
            }
        }
        void ReadBuildings(BinaryReader r,long end,int version)
        {
            if(version!=1)throw new InvalidDataException("Unsupported BULD version: "+version);
            int types=Count(r,end,32);
            for(int type=0;type<types;type++)
            {
                int count=Count(r,end,500000);
                for(int n=0;n<count;n++)
                {
                    var p=Points(r,end,true);if(p.Length<3)throw new InvalidDataException("Invalid building outline.");
                    var f=new Feature{Kind=1,Type=type,Bounds=Bounds(p),Fill=new GraphicsPath()};features.Add(f);f.Fill.AddPolygon(p);
                    if(type==0||type==1)Buildings++;else Walls++;
                }
                // Static icon records are descriptor index, position and direction.
                int icons=Count(r,end,500000);Skip(r,end,icons*20L);
            }
        }
        void ReadPower(BinaryReader r,long end,int version)
        {
            if(version!=1)throw new InvalidDataException("Unsupported PWLN version: "+version);
            int lods=Count(r,end,16);
            for(int lod=0;lod<lods;lod++)
            {
                int count=Count(r,end,100000);
                for(int n=0;n<count;n++)
                {
                    int type=Count(r,end,32);var p=Points(r,end,lod==0);
                    if(lod!=0||p.Length<2)continue;
                    var f=new Feature{Kind=2,Type=type,Bounds=Bounds(p),Line=new GraphicsPath()};features.Add(f);f.Line.AddLines(p);PowerLines++;
                }
            }
        }
        public void Draw(Graphics g,MapProjection projection)
        {
            float scale=(float)projection.PixelsPerMetre;
            var north=projection.World(PointF.Empty);var south=projection.World(new PointF(projection.Viewport.Width,projection.Viewport.Height));
            var visible=RectangleF.FromLTRB((float)north.X,(float)south.Z,(float)south.X,(float)north.Z);visible.Inflate(20/scale,20/scale);
            var origin=projection.Screen(0,0);var saved=g.Save();
            try
            {
                using(var matrix=new Matrix(scale,0,0,-scale,origin.X,origin.Y))g.Transform=matrix;
                foreach(var f in features)
                {
                    if(!visible.IntersectsWith(f.Bounds))continue;
                    if(f.Kind==1)
                    {
                        bool wall=f.Type>=2;if(wall&&scale<.5||!wall&&scale<.055)continue;
                        using(var fill=new SolidBrush(wall?Color.FromArgb(52,52,52):Color.FromArgb(116,116,116)))g.FillPath(fill,f.Fill);
                        if(!wall)using(var edge=new Pen(Color.FromArgb(52,52,52),.8f/scale))g.DrawPath(edge,f.Fill);
                    }
                    else if(f.Kind==0)
                    {
                        if(f.Type==7)continue;
                        if(f.Type==5||f.Type==4)
                        {
                            if(scale<.18)continue;
                            using(var pen=new Pen(f.Type==4?Color.Firebrick:Color.FromArgb(41,41,41),.9f/scale)){pen.DashStyle=DashStyle.Dash;g.DrawPath(pen,f.Line);}
                        }
                        else
                        {
                            Color color=f.Type==1?Color.FromArgb(233,212,32):f.Type==2?Color.FromArgb(233,223,108):f.Type==3?Color.FromArgb(147,107,68):Color.FromArgb(200,200,200);
                            using(var fill=new SolidBrush(color))g.FillPath(fill,f.Fill);
                            using(var pen=new Pen(color,1.1f/scale))g.DrawPath(pen,f.Line);
                            if(scale>.15)using(var pen=new Pen(Color.FromArgb(97,97,97),.6f/scale))g.DrawPath(pen,f.Edges);
                        }
                    }
                    else if(scale>.25)using(var pen=new Pen(Color.FromArgb(70,70,70),.8f/scale)){pen.DashStyle=DashStyle.Dot;g.DrawPath(pen,f.Line);}
                }
            }
            finally{g.Restore(saved);}
        }
        public void Dispose(){foreach(var f in features)f.Dispose();features.Clear();}
    }
}
