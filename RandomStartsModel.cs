using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.Script.Serialization;

namespace TvtLobby.MissionEditor
{
    public sealed class StartPoint
    {
        public string Id{get;set;} public string Name{get;set;} public string Variant{get;set;}
        public double X{get;set;} public double Y{get;set;} public double Z{get;set;} public double Yaw{get;set;}
        public int Ground{get;set;} public int ReadOnly{get;set;}
        public StartPoint(){Id="tvtLobby_Start_"+Guid.NewGuid().ToString("N");Name="Start";Variant="";Ground=1;}
        public override string ToString(){return Name+(String.IsNullOrWhiteSpace(Variant)?"":" · "+Variant);}
    }
    public sealed class StartMember
    {
        public int Kind{get;set;} public string Key{get;set;}
        public string Identity{get{return Kind+":"+Key;}}
    }
    public sealed class StartBundle
    {
        public string Id{get;set;} public string Name{get;set;} public string Pool{get;set;}
        public int Mode{get;set;} public int IncludeVehicles{get;set;} public int IncludeZones{get;set;}
        public double X{get;set;} public double Y{get;set;} public double Z{get;set;} public double Yaw{get;set;}
        public List<StartMember> Members{get;set;} public List<string> Points{get;set;}
        public StartBundle(){Id=Guid.NewGuid().ToString();Name="New set";Pool="";IncludeVehicles=IncludeZones=1;Members=new List<StartMember>();Points=new List<string>();}
        public override string ToString(){return Name;}
    }
    public sealed class StartObject
    {
        public int Kind{get;set;} public string Key{get;set;} public string Name{get;set;} public string EngineKey{get;set;}
        public string EntityId{get;set;} public string ParentId{get;set;}
        public double X{get;set;} public double Y{get;set;} public double Z{get;set;} public double Yaw{get;set;} public double GroundY{get;set;}
        public List<string> Groups{get;set;} public List<string> Zones{get;set;}
        public StartObject(){Groups=new List<string>();Zones=new List<string>();}
        public string Identity{get{return Kind+":"+Key;}}
        public override string ToString(){return new[]{"Group","Vehicle","Faction","Object"}[Kind]+" · "+Name;}
    }
    public sealed class RandomStartData
    {
        public int Version{get;set;} public int Enabled{get;set;} public int Seed{get;set;} public int SelectionMode{get;set;} public int WorldPoints{get;set;}
        public List<StartPoint> Points{get;set;} public List<StartBundle> Bundles{get;set;}
        public RandomStartData(){Version=2;WorldPoints=1;Points=new List<StartPoint>();Bundles=new List<StartBundle>();}
        public int ModeOf(StartBundle b){return Version>=2?SelectionMode:b.Mode;}
        private string PoolOf(StartBundle b){return Version>=2?"mission":b.Pool??"";}
        public void UseMissionMode(int mode){Version=2;SelectionMode=mode;foreach(var b in Bundles){b.IncludeVehicles=1;b.Mode=0;b.Pool="";}}
        public RandomStartData Copy(){var j=new JavaScriptSerializer{MaxJsonLength=16000000};return j.Deserialize<RandomStartData>(j.Serialize(this));}
        public static bool Finite(double x){return x>=-1000000&&x<=1000000;}
        public string Validate(IList<StartObject> catalog)
        {
            if((Version!=1&&Version!=2)||Seed<0||Seed>2147483646)return "Unsupported settings or seed.";
            if(Version==2&&(SelectionMode<0||SelectionMode>1))return "Choose a mission-wide selection method.";
            if(Points.Count>256||Bundles.Count>64)return "Use at most 256 destinations and 64 sets.";
            if(Points.Select(p=>p.Id).Distinct().Count()!=Points.Count||Bundles.Select(b=>b.Id).Distinct().Count()!=Bundles.Count)return "Duplicate point or set ID.";
            if(Enabled!=0&&Bundles.Count==0)return "Add a set before enabling random starts.";
            foreach(var p in Points)if(!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z)||!Finite(p.Yaw)||Math.Abs(p.Yaw)>360||String.IsNullOrWhiteSpace(p.Name)||p.Name.Length>120)return "Check the name, coordinates and direction of "+p.Name+".";
            var owners=new Dictionary<string,string>();
            foreach(var b in Bundles)
            {
                if(String.IsNullOrWhiteSpace(b.Name)||b.Name.Length>120||!Finite(b.X)||!Finite(b.Y)||!Finite(b.Z)||!Finite(b.Yaw)||Math.Abs(b.Yaw)>360)return "Check the name and anchor of "+b.Name+".";
                if(b.Members.Count==0||b.Points.Count==0)return "Choose members and destinations for "+b.Name+".";
                if(Version==2&&b.IncludeVehicles!=1)return "Linked vehicles must move with their group.";
                if(Version==1&&(b.Mode<0||b.Mode>2||(b.Mode>0&&String.IsNullOrWhiteSpace(b.Pool))))return "Choose a selection pool for "+b.Name+".";
                if(Version==1&&Bundles.Any(other=>other!=b&&!String.IsNullOrEmpty(b.Pool)&&other.Pool==b.Pool&&other.Mode!=b.Mode))return "Sets in the same pool need the same selection rule.";
                var choices=Points.Where(p=>b.Points.Contains(p.Id)).ToList();
                if(choices.Count!=b.Points.Count)return "A destination is missing or repeated in "+b.Name+".";
                if(ModeOf(b)==2&&(choices.Any(p=>String.IsNullOrWhiteSpace(p.Variant))||choices.Select(p=>p.Variant).Distinct().Count()!=choices.Count))return "Linked variants need one destination per variant label in "+b.Name+".";
                foreach(var member in b.Members)if(member.Kind==2?!catalog.Any(o=>o.Kind==0&&o.EngineKey==member.Key):!catalog.Any(o=>o.Identity==member.Identity))return "Member missing or locked in "+b.Name+": "+member.Key;
                var expanded=Expand(b,catalog);
                if(b.IncludeZones!=0)foreach(var group in expanded.Where(o=>o.Kind==0))foreach(string zone in group.Zones)
                    if(catalog.Any(o=>o.Kind==0&&o.Zones.Contains(zone)&&!expanded.Contains(o)))return "Freeze zone "+zone+" also covers a group outside "+b.Name+". Combine those groups or assign separate zones.";
                foreach(var item in expanded)
                {
                    string previous;
                    if(owners.TryGetValue(item.EntityId,out previous)&&previous!=b.Id)return "Sets share "+item.Name+". Combine the sets or remove the shared vehicle / zone.";
                    owners[item.EntityId]=b.Id;
                }
            }
            foreach(var pair in owners)
            {
                var item=catalog.FirstOrDefault(o=>o.EntityId==pair.Key);var visited=new HashSet<string>();
                while(item!=null&&!String.IsNullOrEmpty(item.ParentId)&&visited.Add(item.ParentId))
                {
                    string other;if(owners.TryGetValue(item.ParentId,out other)&&other!=pair.Value)return "Different sets contain an object and its parent. Combine them into one set.";
                    item=catalog.FirstOrDefault(o=>o.EntityId==item.ParentId);
                }
            }
            string error;Sample(1,out error);return error;
        }
        public static List<StartObject> Expand(StartBundle b,IList<StartObject> catalog)
        {
            var result=catalog.Where(o=>b.Members.Any(m=>m.Kind==2?o.EngineKey==m.Key&&(o.Kind==0||(o.Kind==1&&b.IncludeVehicles!=0)):o.Identity==m.Identity)).ToList();
            foreach(var g in result.Where(o=>o.Kind==0).ToList())
            {
                if(b.IncludeVehicles!=0)result.AddRange(catalog.Where(o=>o.Kind==1&&(o.Groups.Contains(g.Key)||o.Groups.Contains(g.Name))));
                if(b.IncludeZones!=0)result.AddRange(catalog.Where(o=>o.Kind==3&&g.Zones.Contains(o.Key)));
            }
            return result.GroupBy(o=>o.EntityId).Select(g=>g.First()).ToList();
        }
        public static MapPoint Transform(StartBundle b,StartPoint p,StartObject o)
        {
            double radians=(p.Yaw-b.Yaw)*Math.PI/180,dx=o.X-b.X,dz=o.Z-b.Z;
            return new MapPoint{X=p.X+Math.Cos(radians)*dx+Math.Sin(radians)*dz,Z=p.Z-Math.Sin(radians)*dx+Math.Cos(radians)*dz,Y=p.Y+o.Y-b.Y};
        }
        // Preview uses the same bounded integer generator and matching rule as the server.
        private sealed class Generator
        {
            private long state;public Generator(int seed){state=Math.Max(1,seed);}
            public int Next(int count){state=16807*(state%127773)-2836*(state/127773);if(state<=0)state+=2147483647;return (int)(state%count);}
            public List<string> Shuffle(IEnumerable<string> values){var v=values.ToList();for(int i=v.Count-1;i>0;i--){int j=Next(i+1);string s=v[i];v[i]=v[j];v[j]=s;}return v;}
        }
        private static bool Match(int index,List<StartBundle> sets,Dictionary<string,int> owners,HashSet<string> visited,Generator random)
        {
            foreach(string point in random.Shuffle(sets[index].Points)){if(!visited.Add(point))continue;int before;if(!owners.TryGetValue(point,out before)||Match(before,sets,owners,visited,random)){owners[point]=index;return true;}}return false;
        }
        public Dictionary<string,string> Sample(int seed,out string error)
        {
            error=null;var output=new Dictionary<string,string>();var pools=new HashSet<string>();var random=new Generator(seed);
            foreach(var b in Bundles)
            {
                if(b.Points.Count==0){error="Choose destinations for "+b.Name;return output;}
                int mode=ModeOf(b);string pool=PoolOf(b);
                if(mode==0){output[b.Id]=b.Points[random.Next(b.Points.Count)];continue;}
                if(!pools.Add(pool))continue;var sets=Bundles.Where(s=>PoolOf(s)==pool).ToList();
                if(mode==1)
                {
                    var owners=new Dictionary<string,int>();for(int i=0;i<sets.Count;i++)if(!Match(i,sets,owners,new HashSet<string>(),random)){error="Not enough different allowed points for all assigned groups / objects.";return output;}
                    foreach(var choice in owners)output[sets[choice.Value].Id]=choice.Key;
                }
                else
                {
                    var variants=Points.Where(p=>b.Points.Contains(p.Id)&&sets.All(s=>Points.Any(q=>s.Points.Contains(q.Id)&&q.Variant==p.Variant))).Select(p=>p.Variant).ToList();
                    if(variants.Count==0){error="No shared variant across all sets in pool "+b.Pool;return output;}string label=variants[random.Next(variants.Count)];
                    foreach(var s in sets)output[s.Id]=Points.First(p=>s.Points.Contains(p.Id)&&p.Variant==label).Id;
                }
            }
            return output;
        }
    }
    public sealed class StartPointProperties
    {
        private readonly StartPoint p;public StartPointProperties(StartPoint value){p=value;}
        [Category("1. Destination")] public string Name{get{return p.Name;}set{p.Name=value;}}
        [Category("2. Placement")] public double X{get{return Math.Round(p.X,3);}set{p.X=value;}}
        [Category("2. Placement")] public double Z{get{return Math.Round(p.Z,3);}set{p.Z=value;}}
        [Category("2. Placement"),DisplayName("Rotate arrangement (°)"),Description("0 keeps the authored directions. Rotate all members together around this point.")] public double Heading{get{return Math.Round(p.Yaw,3);}set{p.Yaw=value;}}
        [Category("3. Height"),DisplayName("Follow terrain"),Description("Keep each member's original clearance above terrain. Turn off for a platform, bridge or other exact altitude.")]
        public bool Ground{get{return p.Ground!=0;}set{p.Ground=value?1:0;}}
        [Category("3. Height"),DisplayName("Height / offset (m)"),Description("Follow terrain ON: extra height above ground, normally 0. OFF: absolute destination altitude.")]
        public double Height{get{return Math.Round(p.Y,3);}set{p.Y=value;}}
    }
}
