using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
namespace TvtLobby.MissionEditor
{
    public sealed class BriefingData
    {
        public int Version{get;set;} public string Problem{get;set;}
        public List<TaskNode> Sections{get;set;} public List<TaskNode> Schemas{get;set;}
        public List<TaskChoice> Targets{get;set;} public List<EndChoice> Subjects{get;set;} public List<EndChoice> Objectives{get;set;}
        public void Normalize(){Sections=Sections??new List<TaskNode>();Schemas=Schemas??new List<TaskNode>();Targets=Targets??new List<TaskChoice>();Subjects=Subjects??new List<EndChoice>();Objectives=Objectives??new List<EndChoice>();foreach(var n in Sections.Concat(Schemas))n.Normalize();}
        public string Serialize(){return EditorJson.Serialize(this);}
        public TaskNode Make(string type){var s=Schemas.FirstOrDefault(x=>x.TypeName==type);return s==null?null:s.Clone();}
        public TaskNode Subject(string key){var n=Make("tvtLobby_SelectSubject");n.Set("Kind",key.Substring(0,1));n.Set("Id",key.Substring(2));return n;}
        public string Label(string key){return SubjectOptions.Label(Subjects,key);}
    }
    public sealed class BriefingEdit
    {
        public int Index{get;set;} public int Destination{get;set;} public TaskNode Definition{get;set;}
        public BriefingEdit(){Index=-1;Destination=-1;}
        public string Serialize(){return EditorJson.Serialize(this);}
        public BriefingEdit Clone(){return new BriefingEdit{Index=Index,Destination=Destination,Definition=Definition==null?null:Definition.Clone()};}
        public string Validate(){if(Definition==null)return "Choose a briefing section.";if(String.IsNullOrWhiteSpace(Definition.Text("Title"))||Definition.Text("Title").Length>256)return "Enter a heading of 1–256 characters.";if(Definition.Text("Text").Length>16384)return "Text is limited to 16,384 characters. Split it into sections.";return null;}
    }
}
