using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace TvtLobby.MissionEditor
{
    public sealed class TaskField
    {
        public string Name{get;set;} public string Kind{get;set;} public string Value{get;set;} public string Base{get;set;}
        public List<string> Values{get;set;} public List<string> EnumNames{get;set;} public List<int> EnumValues{get;set;} public List<TaskNode> Nodes{get;set;}
        public TaskField(){Values=new List<string>();EnumNames=new List<string>();EnumValues=new List<int>();Nodes=new List<TaskNode>();Value="";}
        public void Normalize(){Values=Values??new List<string>();EnumNames=EnumNames??new List<string>();EnumValues=EnumValues??new List<int>();Nodes=Nodes??new List<TaskNode>();foreach(var n in Nodes)n.Normalize();}
    }
    public sealed class TaskNode
    {
        public string TypeName{get;set;} public List<TaskField> Fields{get;set;}
        public TaskNode(){Fields=new List<TaskField>();}
        public TaskField Field(string name){return Fields.FirstOrDefault(f=>f.Name==name);}
        public string Text(string name){var f=Field(name);return f==null?"":f.Value??"";}
        public void Set(string name,string value){var f=Field(name);if(f!=null)f.Value=value;}
        public void Normalize(){Fields=Fields??new List<TaskField>();foreach(var f in Fields)f.Normalize();}
        public TaskNode Clone(){var n=new JavaScriptSerializer().Deserialize<TaskNode>(new JavaScriptSerializer().Serialize(this));n.Normalize();return n;}
    }
    public sealed class TaskRow
    {
        public string Key{get;set;} public string Category{get;set;} public int Active{get;set;} public TaskNode Definition{get;set;}
        public int AutoLosers{get;set;} public List<string> Losers{get;set;} public int EndRule{get;set;} public string Winner{get;set;} public string EndSubject{get;set;} public string Message{get;set;}
        public TaskRow(){EndRule=-1;AutoLosers=1;Losers=new List<string>();}
    }
    public sealed class TaskChoice
    {
        public string Key{get;set;} public string Alias{get;set;} public string Label{get;set;} public string Group{get;set;} public string Prefab{get;set;} public int Kind{get;set;} public int Member{get;set;}
        public override string ToString(){return Label;}
    }
    public sealed class ObjectivesData
    {
        public int Version{get;set;} public string Problem{get;set;} public string EndRevision{get;set;}
        public List<TaskRow> Rows{get;set;} public List<TaskNode> Schemas{get;set;} public List<TaskChoice> Choices{get;set;} public List<EndChoice> Subjects{get;set;}
        public void Normalize(){Rows=Rows??new List<TaskRow>();Schemas=Schemas??new List<TaskNode>();Choices=Choices??new List<TaskChoice>();Subjects=Subjects??new List<EndChoice>();foreach(var n in Schemas)n.Normalize();foreach(var r in Rows)r.Definition.Normalize();}
        public string Serialize(){return EditorJson.Serialize(this);}
        public TaskNode Make(string type){var s=Schemas.FirstOrDefault(x=>x.TypeName==type);return s==null?null:s.Clone();}
        public TaskNode Subject(string key){var n=Make("tvtLobby_SelectSubject");if(!String.IsNullOrEmpty(key)){n.Set("Kind",key.Substring(0,1));n.Set("Id",key.Substring(2));}return n;}
        public static string SubjectKey(TaskNode n){return n==null?"":n.Text("Kind")+":"+n.Text("Id");}
        public static readonly string[] Types={"tvtLobby_ZoneCaptureModule","tvtLobby_RaiseFlagModule","tvtLobby_DestructionTask","tvtLobby_EliminationTask","tvtLobby_DeliveryTask","tvtLobby_EvacuationTask","tvtLobby_RouteCompletionTask"};
        public static readonly string[] Titles={"Zone Control","Flag Control","Destruction","Elimination","Delivery","Evacuation","Route Completion"};
        public static string Title(string type){int i=Array.IndexOf(Types,type);if(i>=0)return Titles[i];switch(type){case "tvtLobby_TargetSet":return "Target set";case "tvtLobby_EntityTarget":return "Placed object";case "tvtLobby_GroupTarget":return "Whole group (characters)";case "tvtLobby_GroupMemberTarget":return "Place in a group";case "tvtLobby_TagTarget":return "Tagged objects";case "tvtLobby_SetTarget":return "Reusable target set";default:return Words(type.Replace("tvtLobby_",""));}}
        public static string Words(string value){return Regex.Replace(value??"","([a-z])([A-Z])","$1 $2").Replace('_',' ');}
        public static int Section(string name){if(new[]{"Capturers","InitialOwner","Opposition","ResultSubject","Targets","Zone","Flag","Zones","SafeZone","Waypoints"}.Contains(name))return 1;if(new[]{"Prerequisites","AnyPrerequisite","Notifications"}.Contains(name))return 3;if(new[]{"AdditionalResults","AuthorNote"}.Contains(name))return 4;return 2;}
        public static string Help(string name){switch(name){case "Targets":return "Groups include characters only. Vehicles are selected separately. Targets bind to concrete bodies after freeze cleanup; an empty selection skips without victory.";case "Prerequisites":return "Empty: active at combat start. Dependencies use the selected objective's result and participating subject.";case "CaptureRules":return "Custom capture: one rule per capturing subject; compare the presence of selected factions, sides or alliances.";case "Zones":return "Choose existing zones, or create new ones when applying. Geometry stays editable on the map.";case "Waypoints":return "Visit zones in this order. Use Up / Down to change the route.";case "ConfirmDeath":return "Requires an interaction at the body; death alone does not complete the task.";case "Cumulative":return "On: accept each target separately after its hold time, so several trips can contribute. Off: the required targets must qualify together.";case "RequireInitialPresence":return "Each target must first be observed inside a source zone while the objective is active, then leave all source zones.";case "SafeZone":return "Optional destination. When set, targets must reach it after leaving every source zone.";case "RequireDropped":return "Items must be placed on the ground in a delivery zone, not carried inside an inventory.";case "AdditionalResults":return "Assign success/failure outcomes to other sides, such as a defender losing when a protected target dies.";case "ConfirmationSeconds":return "How long an allowed player must hold Confirm Death near a target body.";case "AuthorNote":return "Private author note. It is never generated into the briefing.";case "Count":return "Physical target tasks cap the required count to the actual available targets after freeze.";case "Text":return "Manual message. Tokens depend on event: {objective}, {progress}, {required}, {total}, {remaining}, {zone}, {waypoint}, {target}.";default:return "";}}
    }
    public sealed class ObjectiveEdit
    {
        public string Key{get;set;} public TaskNode Definition{get;set;} public int AutoLosers{get;set;} public List<string> Losers{get;set;} public int EndRule{get;set;} public int EndMission{get;set;}
        public string Winner{get;set;} public string EndSubject{get;set;} public string Message{get;set;}
        public ObjectiveEdit(){AutoLosers=1;Losers=new List<string>();Key="";EndRule=-1;Winner=EndSubject=Message="";}
        public string Serialize(){return EditorJson.Serialize(this);}
        public ObjectiveEdit Clone(){var e=new JavaScriptSerializer().Deserialize<ObjectiveEdit>(Serialize());e.Definition.Normalize();return e;}
        public static ObjectiveEdit From(TaskRow row){return new ObjectiveEdit{Key=row.Key,AutoLosers=row.AutoLosers,Losers=new List<string>(row.Losers??new List<string>()),Definition=row.Definition.Clone(),EndRule=row.EndRule,EndMission=row.EndRule>=0?1:0,Winner=row.Winner??"",EndSubject=row.EndSubject??"",Message=row.Message??""};}
        public string Validate()
        {
            if(Definition==null)return "Choose an objective type.";
            string name=Definition.Text("Name");
            if(String.IsNullOrWhiteSpace(name)||name.Length>120||name.IndexOfAny(new[]{',','\r','\n','"'})>=0)return "Enter a unique title without commas, quotes or newlines.";
            if(Definition.TypeName=="tvtLobby_ZoneCaptureModule" && Definition.Text("Condition")=="4")
            {
                string error; if(CaptureExpression.Compile(Definition.Text("CaptureExpression"),out error)==null)return "Capture expression: "+error;
            }
            if(Definition.TypeName=="tvtLobby_RaiseFlagModule")
            {
                var flags=new List<string>{Definition.Text("Flag")};var more=Definition.Field("AdditionalFlags");if(more!=null)flags.AddRange(more.Values);
                if(flags.Any(String.IsNullOrWhiteSpace)||flags.Count>128)return "Select each flag, or remove the empty row. At least one flag is required (maximum 128).";
                if(flags.Distinct().Count()!=flags.Count)return "Each flag may appear only once.";
                int count;if(Definition.Text("Amount")=="2"&&(!Int32.TryParse(Definition.Text("Count"),out count)||count<1||count>flags.Count))return "Required flag count must be between 1 and the number of selected flags.";
                double value;if(Definition.Text("Amount")=="3"&&(!Double.TryParse(Definition.Text("Percentage"),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||value<=0||value>100))return "Required flag percentage must be greater than 0 and at most 100.";
            }
            if((Message??"").Length>600)return "Result message supports up to 600 characters.";
            if(EndMission!=0&&(String.IsNullOrEmpty(Winner)||String.IsNullOrEmpty(EndSubject)))return "Choose the result subject and winner.";
            return null;
        }
    }
}
