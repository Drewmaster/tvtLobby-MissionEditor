using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace TvtLobby.MissionEditor
{
    public sealed class EndChoice
    {
        public string Key{get;set;} public string Name{get;set;} public int Slots{get;set;}
        public int Control{get;set;} public List<string> Owners{get;set;}
        public string InitialOwner{get;set;} public List<string> Members{get;set;}
        public EndChoice(){Owners=new List<string>();Members=new List<string>();InitialOwner="";}
        public override string ToString(){return Name;}
    }
    // Presentation only: preserve typed runtime keys, including existing SIDE results.
    public static class SubjectOptions
    {
        static string Identity(EndChoice choice)
        {
            if(choice.Key.StartsWith("1:")&&choice.Members.Distinct().Count()==1)return "0:"+choice.Members[0];
            return choice.Key;
        }
        public static bool Same(IEnumerable<EndChoice> source,string first,string second)
        {
            if(first==second)return true;
            var a=source.FirstOrDefault(x=>x.Key==first);var b=source.FirstOrDefault(x=>x.Key==second);
            return a!=null&&b!=null&&Identity(a)==Identity(b);
        }
        public static string Label(IEnumerable<EndChoice> source,string key)
        {
            var all=source.ToList();var choice=all.FirstOrDefault(x=>x.Key==key);if(choice==null)return key;
            if(!key.StartsWith("1:"))return choice.Name;
            var names=choice.Members.Distinct().Select(id=>all.FirstOrDefault(x=>x.Key=="0:"+id)).ToList();
            return names.Count>0&&names.All(x=>x!=null)?String.Join(" + ",names.Select(x=>x.Name)):choice.Name;
        }
        public static List<EndChoice> Visible(IEnumerable<EndChoice> source,IEnumerable<string> selected=null,IEnumerable<string> allowed=null)
        {
            var all=source.ToList();var chosen=new HashSet<string>(selected??Enumerable.Empty<string>());
            var permitted=allowed==null?null:new HashSet<string>(allowed);
            var rows=all.Where(x=>permitted==null||permitted.Contains(x.Key));var result=new List<EndChoice>();
            foreach(var group in rows.GroupBy(Identity))
            {
                var row=group.FirstOrDefault(x=>chosen.Contains(x.Key))??group.FirstOrDefault(x=>x.Key.StartsWith("0:"))??group.First();
                result.Add(new EndChoice{Key=row.Key,Name=Label(all,row.Key),Slots=row.Slots,Control=row.Control,Owners=row.Owners,InitialOwner=row.InitialOwner,Members=row.Members});
            }
            return result;
        }
    }
    public sealed class EndRequirement
    {
        public string Objective{get;set;} public string Subject{get;set;} public int Fact{get;set;}
        // Keep the desktop model and older bridge JSON compatible with the renamed script field.
        public string SelectedSubject{get{return Subject;}set{Subject=value;}}
        public EndRequirement(){Objective=Subject="";Fact=2;}
    }
    public sealed class EndRule
    {
        public int Index{get;set;} public int Type{get;set;} public int Enabled{get;set;} public int Draw{get;set;} public int Any{get;set;} public int Fallback{get;set;} public int ThresholdMode{get;set;}
        public string Name{get;set;} public string Winner{get;set;} public string Subject{get;set;} public string Message{get;set;}
        public string SelectedSubject{get{return Subject;}set{Subject=value;}}
        public int AutoLosers{get;set;} public decimal Threshold{get;set;}
        public List<string> Losers{get;set;} public List<EndRequirement> Requirements{get;set;}
        public EndRule(){Index=-1;Enabled=Draw=ThresholdMode=AutoLosers=1;Threshold=1;Name="New result";Winner=Subject="";Message="Mission complete.";Losers=new List<string>();Requirements=new List<EndRequirement>();}
        public string Estimate(int slots)
        {
            if(slots<=0)return "0 available slots · an empty side never triggers a result.";
            decimal lost=ThresholdMode==0?Math.Ceiling(slots*Threshold/100):ThresholdMode==3?slots-Math.Floor(slots*Threshold/100):ThresholdMode==2?Threshold:slots-Threshold;
            return slots+" available slots → "+(ThresholdMode==0||ThresholdMode==2?"at least "+lost+" lost":"at most "+(slots-lost)+" alive")+" ("+lost+" lost / "+(slots-lost)+" alive)"+(lost<=0?" · already met at full occupancy":lost>slots?" · unreachable at full occupancy":"");
        }
    }
    public sealed class EndProfile
    {
        public int Enabled{get;set;} public int Display{get;set;} public decimal Seconds{get;set;} public int Pinned{get;set;} public int Sound{get;set;} public int Chat{get;set;}
        public decimal DebriefDelay{get;set;} public string Recipient{get;set;} public string DrawText{get;set;}
        public EndProfile(){Enabled=Display=Sound=Chat=1;Seconds=DebriefDelay=10;Recipient="";DrawText="Mission ended in a draw.";}
    }
    public sealed class MissionEndData
    {
        public int Version{get;set;} public int ObjectivesEnabled{get;set;} public int LossesEnabled{get;set;} public int TimeEnabled{get;set;}
        public decimal TimeSeconds{get;set;} public string Problem{get;set;} public EndProfile Profile{get;set;}
        public List<EndRule> Rules{get;set;} public List<EndChoice> Subjects{get;set;} public List<EndChoice> Objectives{get;set;}
        public MissionEndData(){TimeSeconds=3600;Problem="";Profile=new EndProfile();Rules=new List<EndRule>();Subjects=new List<EndChoice>();Objectives=new List<EndChoice>();}
        public string Serialize(){return EditorJson.Serialize(this);}
        public string EditableFingerprint(){var copy=Clone();copy.Profile=null;return copy.Serialize();}
        public MissionEndData Clone(){return new JavaScriptSerializer().Deserialize<MissionEndData>(Serialize());}
        public string Validate()
        {
            if(Version!=1)return "Restart Workbench and reopen Mission Editor to load Mission End.";
            if(!String.IsNullOrEmpty(Problem))return Problem;
            if(TimeSeconds<0||TimeSeconds>604800||(TimeEnabled!=0&&TimeSeconds<=0))return "Set a positive enabled combat time limit (up to 7 days).";
            if(Rules.Count>128)return "At most 128 result rules.";
            if(Profile.Display<0||Profile.Display>3||Profile.Seconds<=0||Profile.Seconds>3600||Profile.DebriefDelay<0||Profile.DebriefDelay>3600||!Known(Profile.Recipient,true))return "Invalid shared message profile.";
            if(Rules.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=Rules.Count)return "Each result rule needs a unique name.";
            if(Rules.Count(x=>x.Type==2&&x.Fallback!=0)>1)return "Only one default timeout result is allowed.";
            foreach(var r in Rules)
            {
                if(String.IsNullOrWhiteSpace(r.Name)||r.Name.Length>120||r.AutoLosers<0||r.AutoLosers>1||(r.Message??"").Length>600)return "Check rule name and result text (maximum 600 characters).";
                if(!Known(r.Winner,true)||r.Draw!=0&&(!String.IsNullOrEmpty(r.Winner)||r.Losers.Count>0))return "Choose a winner or a draw with no explicit losers.";
                if(r.Losers.Any(x=>!Known(x,false)||Overlaps(x,r.Winner)))return "Winner and losers must not overlap, including alliances.";
                if(r.Type==1)
                {
                    if(!Known(r.Subject,false)||r.ThresholdMode<0||r.ThresholdMode>3||r.Threshold<0)return "Choose a loss subject and valid threshold.";
                    if((r.ThresholdMode==0||r.ThresholdMode==3)&&r.Threshold>100)return "Percentages must be 0–100.";
                    if((r.ThresholdMode==1||r.ThresholdMode==2)&&(r.Threshold!=Math.Floor(r.Threshold)||r.Threshold>4096))return "Use whole participants (0–4096).";
                }
                if(r.Type==0&&r.Enabled!=0&&r.Requirements.Count==0)return "Choose at least one objective condition.";
                if(r.Fallback!=0&&r.Requirements.Count!=0)return "The default timeout has no objective conditions.";
                foreach(var q in r.Requirements)
                    if(!Objectives.Any(x=>x.Key==q.Objective&&(x.Slots!=0||r.Enabled==0)&&(x.Owners.Contains(q.Subject)||q.Fact==1&&x.InitialOwner==q.Subject)&&(x.Control!=0?q.Fact<2:q.Fact>=2))||q.Fact<0||q.Fact>3||!Known(q.Subject,false))return "Choose enabled objectives, a supported fact and a participating subject.";
            }
            return null;
        }
        bool Known(string value,bool empty){return String.IsNullOrEmpty(value)?empty:Subjects.Any(x=>x.Key==value);}
        bool Overlaps(string first,string second){var a=Subjects.FirstOrDefault(x=>x.Key==first);var b=Subjects.FirstOrDefault(x=>x.Key==second);return a!=null&&b!=null&&(a.Key==b.Key||a.Members.Intersect(b.Members).Any());}
    }
}
