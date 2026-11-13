using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Web.Script.Serialization;

namespace TvtLobby.MissionEditor
{
    public static class MapOrder
    {
        public static bool IsMarker(MapRow row)
        {return row.Kind=="Icon"||row.Kind=="Line"||row.Kind=="Area"||((row.Kind=="Zone"||row.Kind=="Boundary")&&(row.ShowMap||row.Style!=null));}
        public static int Value(MapRow row){return row.Style==null?0:row.Style.ZOrder;}
        // The list is front-to-back; painting uses the exact reverse order.
        public static List<MapRow> FrontToBack(IEnumerable<MapRow> rows)
        {return rows.OrderByDescending(Value).ThenByDescending(r=>r.OrderKey??r.Key,StringComparer.Ordinal).ToList();}
        public static List<string> Move(IEnumerable<MapRow> rows,string key,string target,bool after)
        {
            var order=FrontToBack(rows.Where(IsMarker)).Select(r=>r.Key).ToList();
            if(key==target||!order.Contains(key)||!order.Contains(target))return order;
            order.Remove(key);order.Insert(order.IndexOf(target)+(after?1:0),key);return order;
        }
    }
    public static class MapWire
    {
        // Enfusion RegAll serializes script bools as 0/1; normalize only schema bool fields.
        public static MapReply Read(string text)
        {
            var json=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};object value=json.DeserializeObject(text);Normalize(value);
            return json.ConvertToType<MapReply>(value);
        }
        private static void Normalize(object value)
        {
            var map=value as Dictionary<string,object>;
            if(map!=null)
            {
                foreach(string field in new[]{"Ok","ReadOnly","CanMoveLayer","Enabled","InitiallyVisible","Follow","KillAfterDeadline","DuringFreeze","AfterFreeze","ShowMap"})
                    if(map.ContainsKey(field)&&!(map[field] is bool))map[field]=Convert.ToInt32(map[field])!=0;
                foreach(object child in map.Values)Normalize(child);
            }
            var array=value as object[];if(array!=null)foreach(object child in array)Normalize(child);
        }
    }
    public sealed class MapPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public sealed class MapRuleSubject {public int Kind{get;set;} public string Id{get;set;}}
    public sealed class MapVisibilityEvent {public string Objective{get;set;} public int Event{get;set;} public MapRuleSubject Capturer{get;set;}}
    public sealed class MapRow
    {
        public string Layer {get;set;}
        public bool CanMoveLayer {get;set;}
        public static string DefaultLayer(string kind)
        {return kind=="Icon"||kind=="Line"||kind=="Area"?"tvtLobby_Markers":kind=="Zone"||kind=="Boundary"||kind=="FreezeCircle"?"tvtLobby_Zones":"";}
        public string EngineKey {get;set;} public string GroupRef {get;set;} public string EntityKey {get;set;}
        public MapStyle Style {get;set;}
        public bool ShowMap {get;set;}
        public List<MapVisibilityEvent> ShowOn{get;set;} public List<MapVisibilityEvent> HideOn{get;set;}
        public List<MapRuleSubject> Subjects{get;set;} public List<MapRuleSubject> AppliesTo{get;set;}
        public List<string> BoundaryGroups{get;set;}
        public bool DuringFreeze {get;set;} public bool AfterFreeze {get;set;}
        public string Key { get; set; }
        public string OrderKey { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string AudienceIds { get; set; }
        public bool ReadOnly { get; set; }
        public bool Enabled { get; set; }
        public bool InitiallyVisible { get; set; }
        public bool Follow { get; set; }
        public int Phases { get; set; }
        public int Audience { get; set; }
        public int Icon { get; set; }
        public int ColorIndex { get; set; }
        public int Rotation { get; set; }
        public double Scale { get; set; }
        public double Width { get; set; }
        public double ReturnSeconds { get; set; }
        public double Radius { get; set; }
        public double WarnRadius { get; set; }
        public bool KillAfterDeadline { get; set; }
        public string Stroke { get; set; }
        public string Fill { get; set; }
        public List<MapPoint> Points { get; set; }
        public MapRow()
        {
            ShowOn=new List<MapVisibilityEvent>();HideOn=new List<MapVisibilityEvent>();Subjects=new List<MapRuleSubject>();AppliesTo=new List<MapRuleSubject>();BoundaryGroups=new List<string>();
            Key = Name = Source = Description = AudienceIds = ""; Title = "New marker";
            DuringFreeze=AfterFreeze=true; Kind = "Icon"; Enabled = InitiallyVisible = true; Phases = 54;
            Scale = 1; Width = 2; ReturnSeconds = 30; Radius = 50; WarnRadius = 40;
            Stroke = "0.9 0.65 0.27 0.9"; Fill = "0.9 0.65 0.27 0.12"; Points = new List<MapPoint>();
        }
        public MapRow Copy() { var json = new JavaScriptSerializer(); return json.Deserialize<MapRow>(json.Serialize(this)); }
        public override string ToString() { return Kind + " · " + (String.IsNullOrEmpty(Title) ? Name : Title) + (ReadOnly ? " [read only]" : ""); }
    }
    public sealed class MapReply
    {
        public MapReply() { Objectives=new List<EndChoice>();Icons=new List<MapIcon>(); Colors=new List<string>(); AddonRoots=new List<string>(); Rows = new List<MapRow>(); BackgroundCandidates = new List<string>(); }
        public List<EndChoice> Objectives{get;set;}
        public List<MapIcon> Icons {get;set;} public List<string> Colors {get;set;} public List<string> AddonRoots {get;set;}
        public FreezeZoneSettings FreezeZones {get;set;} public ForcesView Forces {get;set;}
        public List<string> BackgroundCandidates { get; set; }
        public string BackgroundResource { get; set; }
        public string GeometryResource { get; set; }
        public string WorkbenchExecutable { get; set; }
        public string AddonDirectory { get; set; }
        public bool Ok { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string WorldPath { get; set; }
        public string ViewRevision { get; set; }
        public string Background { get; set; }
        public string BackgroundStatus { get; set; }
        public string SelectedKey { get; set; }
        public double MinX { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxZ { get; set; }
        public List<MapRow> Rows { get; set; }
    }
    // One projection for drawing and input. World geometry never goes through rounded screen pixels.
    public sealed class MapProjection
    {
        public double CenterX, CenterZ, PixelsPerMetre = 1;
        public Size Viewport;
        public PointF Screen(double x, double z) { return new PointF((float)(Viewport.Width / 2.0 + (x - CenterX) * PixelsPerMetre), (float)(Viewport.Height / 2.0 - (z - CenterZ) * PixelsPerMetre)); }
        public MapPoint World(PointF screen) { return new MapPoint { X = CenterX + (screen.X - Viewport.Width / 2.0) / PixelsPerMetre, Z = CenterZ - (screen.Y - Viewport.Height / 2.0) / PixelsPerMetre }; }
        public void Zoom(PointF cursor, double factor)
        {
            MapPoint before = World(cursor);
            PixelsPerMetre = Math.Max(0.002, Math.Min(100, PixelsPerMetre * factor));
            MapPoint after = World(cursor); CenterX += before.X - after.X; CenterZ += before.Z - after.Z;
        }
        public void Fit(double minX, double minZ, double maxX, double maxZ)
        {
            CenterX = (minX + maxX) / 2; CenterZ = (minZ + maxZ) / 2;
            PixelsPerMetre = Math.Max(.002, Math.Min(Math.Max(1, Viewport.Width - 48) / Math.Max(1, maxX - minX), Math.Max(1, Viewport.Height - 48) / Math.Max(1, maxZ - minZ)));
        }
    }
    public enum MarkerAudience { Everyone, Unassigned, Factions, Alliances, ExistingSubjects }
    [Flags] public enum MarkerPhases { Creating=1, Waiting=2, Lobby=4, PreBrief=8, Briefing=16, FreezeAndGame=32, Debriefing=64 }
    // Only properties supported by the game are sent. Objective event lists and references are preserved.
    public class MarkerProperties
    {
        protected readonly MapRow Row;
        public MarkerProperties(MapRow row) { Row = row; }
        [Category("Visibility")] public bool Enabled { get { return Row.Enabled; } set { Row.Enabled = value; } }
        [Category("Label")] public string Title { get { return Row.Title; } set { Row.Title = value; } }
        [Category("Visibility")] public MarkerAudience Audience { get { return (MarkerAudience)Row.Audience; } set { Row.Audience = (int)value; } }
        [Category("Visibility"), Description("Comma-separated faction or alliance IDs. ExistingSubjects preserves subject selectors configured in Workbench.")]
        public string AudienceIds { get { return Row.AudienceIds; } set { Row.AudienceIds = value; } }
        [Category("Visibility")] public MarkerPhases Stages { get { return (MarkerPhases)Row.Phases; } set { Row.Phases = (int)value; } }
        [Category("Visibility")] public bool InitiallyVisible { get { return Row.InitiallyVisible; } set { Row.InitiallyVisible = value; } }
    }
    public sealed class IconProperties : MarkerProperties
    {
        public IconProperties(MapRow row) : base(row) { }
        [Category("Icon"), Description("Index in the vanilla placed-marker catalog.")] public int Icon { get { return Row.Icon; } set { Row.Icon = value; } }
        [Category("Icon")] public int ColorIndex { get { return Row.ColorIndex; } set { Row.ColorIndex = value; } }
        [Category("Icon")] public int Rotation { get { return Row.Rotation; } set { Row.Rotation = value; } }
        [Category("Icon"), Description("Vanilla icon multiplier: 0.25–4.")] public double Scale { get { return Row.Scale; } set { Row.Scale = value; } }
        [Category("Label")] public string Description { get { return Row.Description; } set { Row.Description = value; } }
    }
    public sealed class ShapeProperties : MarkerProperties
    {
        public ShapeProperties(MapRow row) : base(row) { }
        [Category("Appearance"), Description("RGBA components from 0 to 1, separated by spaces.")] public string Stroke { get { return Row.Stroke; } set { Row.Stroke = value; } }
        [Category("Appearance"), Description("RGBA; alpha 0 makes the fill transparent. Used only for closed shapes.")] public string Fill { get { return Row.Fill; } set { Row.Fill = value; } }
        [Category("Appearance")] public double Width { get { return Row.Width; } set { Row.Width = value; } }
    }
    public sealed class BoundaryProperties
    {
        private readonly MapRow row;
        public BoundaryProperties(MapRow row) { this.row = row; }
        [Category("Battlefield")] public bool Enabled { get { return row.Enabled; } set { row.Enabled = value; } }
        [Category("Battlefield")] public double ReturnSeconds { get { return row.ReturnSeconds; } set { row.ReturnSeconds = value; } }
        [Category("Battlefield")] public bool KillAfterDeadline { get { return row.KillAfterDeadline; } set { row.KillAfterDeadline = value; } }
    }
    public sealed class FreezeCircleProperties
    {
        readonly MapRow row;
        public FreezeCircleProperties(MapRow row) { this.row=row; }
        [Category("Native freeze circle"), Description("Outer boundary in metres. Leaving your faction's combined territory kills the character during freeze.")]
        public double Radius {get{return row.Radius;}set{row.Radius=value;}}
        [Category("Native freeze circle"), Description("Distance from the center at which the vanilla directional warning starts; must not exceed the outer radius.")]
        public double WarningRadius {get{return row.WarnRadius;}set{row.WarnRadius=value;}}
    }
}
