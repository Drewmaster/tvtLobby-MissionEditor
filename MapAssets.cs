using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TvtLobby.MissionEditor
{
    public sealed class MapIcon
    {
        public int Index {get;set;} public string Name {get;set;} public string IconResource {get;set;}
        public string Quad {get;set;} public string Glow {get;set;}
        public override string ToString(){return Name;}
    }
    // Reads the installed resources in Workbench's mount order. Never substitutes
    // an unrelated icon when a mod resource is unavailable.
    public sealed class MapAssets : IDisposable
    {
        readonly List<string> roots=new List<string>();
        readonly Dictionary<string,string> resolved=new Dictionary<string,string>();
        sealed class ImageSetInfo
        {
            public string Atlas;
            public int Width,Height;
            public readonly Dictionary<string,Rectangle> Sprites=new Dictionary<string,Rectangle>();
        }
        readonly Dictionary<string,ImageSetInfo> sets=new Dictionary<string,ImageSetInfo>();
        readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();
        readonly Dictionary<string,string> errors=new Dictionary<string,string>();
        public MapAssets(MapReply reply)
        {
            roots.AddRange(reply.AddonRoots??new List<string>());
            if(!String.IsNullOrEmpty(reply.AddonDirectory)&&Directory.Exists(reply.AddonDirectory))roots.AddRange(Directory.GetDirectories(reply.AddonDirectory));
            if(!String.IsNullOrEmpty(reply.WorkbenchExecutable))
            {
                var wb=new DirectoryInfo(Path.GetDirectoryName(reply.WorkbenchExecutable));
                string addons=Path.Combine(wb.Parent.Parent.FullName,"Arma Reforger","addons");
                if(Directory.Exists(addons))roots.AddRange(Directory.GetDirectories(addons));
            }
        }
        public static string Relative(string resource)
        {
            string value=Regex.Replace(resource??"",@"^\{[^}]+\}","").Replace('/',Path.DirectorySeparatorChar);
            if(Path.IsPathRooted(value)||value.Split(Path.DirectorySeparatorChar).Contains(".."))throw new InvalidDataException("Use a registered addon resource.");
            return value;
        }
        public string Resolve(string resource)
        {
            if(resolved.ContainsKey(resource))return resolved[resource];
            string relative=Relative(resource);
            foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {string full=Path.Combine(root,relative);try{MapArchive.Read(full);resolved[resource]=full;return full;}catch(FileNotFoundException){}}
            throw new FileNotFoundException("IconResource unavailable: "+resource);
        }
        public Bitmap Cached(string resource,string quad=""){lock(images){Bitmap image;return images.TryGetValue(resource+"|"+quad,out image)?image:null;}}
        public Bitmap Tinted(string resource,Color color)
        {
            lock(images)
            {
                var original=Cached(resource);if(original==null)return null;
                string key=resource+"|tint:"+color.ToArgb();Bitmap result;
                if(images.TryGetValue(key,out result))return result;
                result=new Bitmap(original.Width,original.Height,PixelFormat.Format32bppArgb);
                using(var g=Graphics.FromImage(result))Draw(g,original,new PointF(result.Width/2f,result.Height/2f),Math.Max(result.Width,result.Height),0,color);
                images[key]=result;return result;
            }
        }
        public Bitmap Get(string resource,string quad=""){lock(images)return Load(resource,quad);}
        ImageSetInfo ReadSet(string resource)
        {
            string full=Resolve(resource);ImageSetInfo result;
            if(sets.TryGetValue(full,out result))return result;
            string text=Encoding.UTF8.GetString(MapArchive.Read(full));
            var path=Regex.Match(text,@"\bpath\s+""([^""]+)""");
            var reference=Regex.Match(text,@"\bRefSize\s+(\d+)\s+(\d+)");
            if(!path.Success||!reference.Success)throw new InvalidDataException("Invalid imageset atlas: "+resource);
            result=new ImageSetInfo{Atlas=path.Groups[1].Value,Width=Int32.Parse(reference.Groups[1].Value),Height=Int32.Parse(reference.Groups[2].Value)};
            if(result.Width<=0||result.Height<=0)throw new InvalidDataException("Invalid imageset dimensions.");
            foreach(Match sprite in Regex.Matches(text,@"\bImageSetDefClass\s+(?:""([^""]+)""|([^\s{]+))\s*\{([^{}]*)\}"))
            {
                string body=sprite.Groups[3].Value;
                var named=Regex.Match(body,@"\bName\s+""([^""]+)""");
                string name=named.Success?named.Groups[1].Value:sprite.Groups[1].Success?sprite.Groups[1].Value:sprite.Groups[2].Value;
                var pos=Regex.Match(body,@"\bPos\s+(\d+)\s+(\d+)");
                var size=Regex.Match(body,@"\bSize\s+(\d+)\s+(\d+)");
                if(!pos.Success||!size.Success)continue;
                var rect=new Rectangle(Int32.Parse(pos.Groups[1].Value),Int32.Parse(pos.Groups[2].Value),Int32.Parse(size.Groups[1].Value),Int32.Parse(size.Groups[2].Value));
                if(rect.Width<=0||rect.Height<=0||rect.Right>result.Width||rect.Bottom>result.Height)continue;
                if(!result.Sprites.ContainsKey(name))result.Sprites.Add(name,rect);
            }
            if(result.Sprites.Count==0)throw new InvalidDataException("No symbols found in "+resource);
            sets[full]=result;return result;
        }
        public List<MapIcon> ReadIcons(string resource)
        {
            lock(images)return ReadSet(resource).Sprites.Keys.Select(name=>new MapIcon{Name=name,Quad=name,IconResource=resource}).ToList();
        }
        Bitmap Load(string resource,string quad)
        {
            if(String.IsNullOrEmpty(resource))return null;
            string key=resource+"|"+quad;
            if(images.ContainsKey(key))return images[key];if(errors.ContainsKey(key))return null;
            try
            {
                string full=Resolve(resource);Bitmap result;
                if(resource.EndsWith(".imageset",StringComparison.OrdinalIgnoreCase))
                {
                    var set=ReadSet(resource);Rectangle source;
                    if(!set.Sprites.TryGetValue(quad,out source))throw new InvalidDataException("Symbol is missing: "+quad);
                    Bitmap atlas=Get(set.Atlas);if(atlas==null)throw new InvalidDataException("Atlas unavailable.");
                    double sx=(double)atlas.Width/set.Width,sy=(double)atlas.Height/set.Height;
                    var rect=new Rectangle((int)Math.Round(source.X*sx),(int)Math.Round(source.Y*sy),(int)Math.Round(source.Width*sx),(int)Math.Round(source.Height*sy));
                    if(rect.Width<=0||rect.Height<=0||rect.Right>atlas.Width||rect.Bottom>atlas.Height)throw new InvalidDataException("Imageset rectangle exceeds atlas.");
                    result=atlas.Clone(rect,PixelFormat.Format32bppArgb);
                }
                else {using(var image=Image.FromFile(MapTexture.Decode(full)))result=new Bitmap(image);}
                images[key]=result;return result;
            }
            catch(Exception e){errors[key]=e.Message;return null;}
        }
        public string Error(string resource,string quad=""){string text;return errors.TryGetValue(resource+"|"+quad,out text)?text:"";}
        public void Preload(MapReply scene)
        {
            foreach(var icon in scene.Icons)Get(icon.IconResource,icon.Quad);
            foreach(var row in scene.Rows)if(row.Style!=null){Get(row.Style.ImageSet,row.Style.Quad);Get(row.Style.StrokeTexture);Get(row.Style.FillTexture);}
        }
        public static void Draw(Graphics g,Bitmap image,PointF center,float size,float rotation,Color color)
        {
            if(image==null)return;float scale=size/Math.Max(image.Width,image.Height);var saved=g.Save();g.TranslateTransform(center.X,center.Y);g.RotateTransform(rotation);
            using(var attrs=new ImageAttributes())
            {
                attrs.SetColorMatrix(new ColorMatrix(new[]{new[]{color.R/255f,0,0,0,0},new[]{0,color.G/255f,0,0,0},new[]{0,0,color.B/255f,0,0},new[]{0,0,0,color.A/255f,0},new[]{0f,0,0,0,1}}));
                var r=new Rectangle((int)(-image.Width*scale/2),(int)(-image.Height*scale/2),Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale)));
                g.DrawImage(image,r,0,0,image.Width,image.Height,GraphicsUnit.Pixel,attrs);
            }
            g.Restore(saved);
        }
        public void Dispose(){foreach(var image in images.Values)image.Dispose();images.Clear();}
    }
}
