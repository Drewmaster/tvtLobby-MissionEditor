using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace TvtLobby.MissionEditor
{
    internal static class EditorJson
    {
        // Reflection property order can change after controls inspect a type.
        // Draft fingerprints must depend on values, never that enumeration order.
        static readonly Dictionary<Type,System.Reflection.PropertyInfo[]> properties=new Dictionary<Type,System.Reflection.PropertyInfo[]>();
        public static string Serialize(object value){return new JavaScriptSerializer{MaxJsonLength=64*1024*1024}.Serialize(Ordered(value));}
        // JsonApiStruct distinguishes JSON integers from real numbers. A real
        // field sent as 7 instead of 7.0 silently retains its constructor default.
        public static string SerializeRequest(object value)
        {
            var output=new StringBuilder();WriteRequest(output,Ordered(value),new JavaScriptSerializer{MaxJsonLength=64*1024*1024});return output.ToString();
        }
        static void WriteRequest(StringBuilder output,object value,JavaScriptSerializer serializer)
        {
            if(value is decimal||value is double||value is float)
            {
                string number=value is decimal?((decimal)value).ToString(CultureInfo.InvariantCulture):value is double?((double)value).ToString("R",CultureInfo.InvariantCulture):((float)value).ToString("R",CultureInfo.InvariantCulture);
                if(number=="NaN"||number.Contains("Infinity"))throw new ArgumentException("Workbench numbers must be finite.");
                output.Append(number);if(number.IndexOfAny(new[]{'.','e','E'})<0)output.Append(".0");return;
            }
            var dictionary=value as System.Collections.IDictionary;
            if(dictionary!=null)
            {
                output.Append('{');bool first=true;
                foreach(System.Collections.DictionaryEntry entry in dictionary){if(!first)output.Append(',');first=false;output.Append(serializer.Serialize((string)entry.Key));output.Append(':');WriteRequest(output,entry.Value,serializer);}
                output.Append('}');return;
            }
            var sequence=value as System.Collections.IEnumerable;
            if(sequence!=null&&!(value is string))
            {
                output.Append('[');bool first=true;foreach(var entry in sequence){if(!first)output.Append(',');first=false;WriteRequest(output,entry,serializer);}output.Append(']');return;
            }
            output.Append(serializer.Serialize(value));
        }
        static object Ordered(object value)
        {
            if(value==null||value is string)return value;var type=value.GetType();
            if(type.IsPrimitive||type.IsEnum||value is decimal||value is DateTime||value is Guid)return value;
            var dictionary=value as System.Collections.IDictionary;
            if(dictionary!=null){var result=new SortedDictionary<string,object>(StringComparer.Ordinal);foreach(System.Collections.DictionaryEntry entry in dictionary)result.Add((string)entry.Key,Ordered(entry.Value));return result;}
            var sequence=value as System.Collections.IEnumerable;
            if(sequence!=null){var result=new List<object>();foreach(var entry in sequence)result.Add(Ordered(entry));return result;}
            System.Reflection.PropertyInfo[] fields;lock(properties){if(!properties.TryGetValue(type,out fields)){fields=type.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);properties.Add(type,fields);}}
            var record=new SortedDictionary<string,object>(StringComparer.Ordinal);
            foreach(var field in fields)if(field.CanRead&&field.GetIndexParameters().Length==0&&!Attribute.IsDefined(field,typeof(ScriptIgnoreAttribute)))record.Add(field.Name,Ordered(field.GetValue(value,null)));
            return record;
        }
    }
    public sealed class MissionDetails
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Preview { get; set; }
        public string Loading { get; set; }
        public int MaxPlayers { get; set; }
        public int AutoMax { get; set; }
        public MissionDetails() { MaxPlayers=1; }
        public MissionDetails Clone() { return (MissionDetails)MemberwiseClone(); }
        public string Serialize() { return EditorJson.Serialize(this); }
        public string Validate(int slots)
        {
            int maximum=AutoMax!=0?Math.Max(1,slots):MaxPlayers;
            if(maximum<1||maximum>4096)return "Use a maximum player count between 1 and 4096.";
            if((Title??"").Length>256||(Author??"").Length>256)return "Title and author: at most 256 characters.";
            if((Description??"").Length>16384)return "Description: at most 16,384 characters.";
            foreach(string image in new[]{Icon,Preview,Loading})
                if(String.IsNullOrWhiteSpace(image)||!image.EndsWith(".edds",StringComparison.OrdinalIgnoreCase))return "Choose registered .edds image resources, or restore the defaults.";
            return null;
        }
    }
    public sealed class FoundationItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string State { get; set; }
    }
    public sealed class FoundationReply
    {
        public bool SaveRecoveryPending { get; set; }
        public int Ok { get; set; }
        public int Ready { get; set; }
        public int CanCreate { get; set; }
        public int Slots { get; set; }
        public int PlayableGroups { get; set; }
        public int Pending { get; set; }
        public int ChoicesPaged { get; set; }
        public string CatalogRevision { get; set; }
        public int ChoiceCount { get; set; }
        public int NextChoiceOffset { get; set; }
        public List<TaskChoice> ChoicePage { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string WorldPath { get; set; }
        public string WorldSession { get; set; }
        public string ViewRevision { get; set; }
        public string HeaderPath { get; set; }
        public string RulesPath { get; set; }
        public string AddonRoot { get; set; }
        public string SetupHeaderFile { get; set; }
        public string SetupRulesFile { get; set; }
        public string HeaderTemplateText { get; set; }
        public string RulesTemplateText { get; set; }
        public MissionDetails Details { get; set; }
        public ForcesView Forces { get; set; }
        public MissionSettings Settings { get; set; }
        public FreezeZoneSettings FreezeZones {get;set;}
        public AdvancedSettings Advanced { get; set; }
        public MissionEndData MissionEnd { get; set; }
        public ObjectivesData Objectives { get; set; }
        public BriefingData Briefing { get; set; }
        public FactionFile FactionFile { get; set; }
        public List<FoundationItem> Items { get; set; }
        public List<string> Issues { get; set; }
    }
    public sealed class FactionFile
    {
        public string Path {get;set;} public string Content {get;set;}
    }
    public sealed class FoundationClient
    {
        private readonly Session session;
        private readonly Func<int,object,int,string> transport;
        private readonly Dictionary<string,string> fingerprints=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        private string worldSession;
        private string catalogRevision="",catalogWorld="";
        private List<TaskChoice> catalog;
        public FoundationClient(Session value,Func<int,object,int,string> send=null) { session=value;transport=send??WorkbenchClient.Call; }
        public static FoundationReply Read(string json) { return new JavaScriptSerializer().Deserialize<FoundationReply>(json); }
        public static string Inside(string root,string path)
        {
            string absolute=Path.GetFullPath(path),prefix=Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
            if(!absolute.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Mission resource is outside the authoring addon.");
            return absolute;
        }
        public static void PrepareDirectories(FoundationReply view)
        {
            if(view==null)throw new IOException("Refresh the mission first.");
            var pairs=new[]{new[]{view.SetupHeaderFile,view.HeaderTemplateText,view.HeaderPath},new[]{view.SetupRulesFile,view.RulesTemplateText,view.RulesPath}};
            var files = new List<KeyValuePair<string, string>>();
            foreach (var pair in pairs)
            {
                if (!String.IsNullOrEmpty(pair[2])) continue;
                string path = Inside(view.AddonRoot, pair[0]);
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length == 0)
                        throw new IOException("An empty mission config already exists. Move it out of the addon before creating the mission: " + path);
                    continue;
                }
                if (String.IsNullOrWhiteSpace(pair[1])) throw new IOException("Workbench could not read a tvtLobby mission template. Check that the addon is loaded.");
                files.Add(new KeyValuePair<string, string>(path, pair[1]));
            }
            foreach (var file in files)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file.Key));
                string temporary = file.Key + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, file.Value, new UTF8Encoding(false));
                    File.Move(temporary, file.Key);
                }
                finally { File.Delete(temporary); }
            }
        }
        private static string Hash(string path)
        {
            if(!File.Exists(path))return "missing";
            using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        public static void PrepareFactionFile(FoundationReply view)
        {
            var file=view.FactionFile;
            if(file==null||String.IsNullOrEmpty(file.Content))throw new IOException("Workbench did not return a faction config.");
            string path=Inside(view.AddonRoot,file.Path);
            if(!path.EndsWith(".conf",StringComparison.OrdinalIgnoreCase))throw new IOException("Expected a native .conf file.");
            if(File.Exists(path))
            {
                if(File.ReadAllText(path)!=file.Content)throw new IOException("A different config already exists at "+path+". Choose another faction key; no file was overwritten.");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false)))writer.Write(file.Content);
        }
        private void Remember(FoundationReply view)
        {
            worldSession=view.WorldSession;
            fingerprints.Clear();
            foreach(string resource in new[]{view.HeaderPath,view.RulesPath})
            {
                if(String.IsNullOrEmpty(resource))continue;
                int brace=resource.IndexOf('}');
                string path=Inside(view.AddonRoot,Path.Combine(view.AddonRoot,resource.Substring(brace+1).Replace('/',Path.DirectorySeparatorChar)));
                fingerprints[path]=Hash(path);
            }
        }
        // Read the full terrain target catalog without exceeding Workbench's
        // 1 MiB JSON envelope. Never publish a partially downloaded snapshot.
        private void LoadChoices(FoundationReply reply)
        {
            if(reply.ChoicesPaged==2){
                if(reply.Objectives==null)throw new IOException("Workbench omitted objectives for a reused catalog. Refresh to retry.");
                if(catalog==null||catalogWorld!=reply.WorldSession||catalogRevision!=reply.CatalogRevision||catalog.Count!=reply.ChoiceCount)
                    throw new IOException("Target catalog cache does not match Workbench. Reopen the editor to reconnect.");
                reply.Objectives.Choices=catalog;return;
            }
            if(reply.ChoicesPaged!=1)return;
            if(reply.Objectives==null||reply.ChoiceCount<0)throw new IOException("Invalid Workbench target catalog.");
            var choices=new List<TaskChoice>();int offset=0;
            while(offset<reply.ChoiceCount)
            {
                var request=new Dictionary<string,object>{{"APIFunc","tvtLobby_MissionFoundation"},{"Token",session.Token},{"Operation","choices"},
                    {"ExpectedWorld",reply.WorldPath},{"ViewRevision",reply.ViewRevision},{"ChoiceOffset",offset}};
                var part=Read(transport(session.Port,request,15000));
                if(part==null||part.Ok!=1)throw new IOException(part==null?"Empty target catalog response.":part.Message+" ["+part.Code+"]");
                if(part.ViewRevision!=reply.ViewRevision||part.WorldPath!=reply.WorldPath||part.ChoiceCount!=reply.ChoiceCount||part.ChoicePage==null||part.ChoicePage.Count==0||part.ChoicePage.Count>256)
                    throw new IOException("Workbench returned an inconsistent target catalog. Refresh to retry.");
                choices.AddRange(part.ChoicePage);offset=choices.Count;
                if(offset>reply.ChoiceCount || part.NextChoiceOffset!=(offset==reply.ChoiceCount?-1:offset))
                    throw new IOException("Workbench returned an incomplete target catalog. Refresh to retry.");
            }
            reply.Objectives.Choices=choices;
            catalog=choices;catalogRevision=reply.CatalogRevision;catalogWorld=reply.WorldSession;
        }
        public FoundationReply Execute(string operation,FoundationReply view,MissionDetails details,ForcesEdit edit=null,MissionSettings settings=null,AdvancedSettings advanced=null,MissionEndData missionEnd=null,ObjectiveEdit objective=null,BriefingEdit briefing=null,FreezeZoneSettings freezeZones=null)
        {return ExecuteCore(operation,view,details,edit,settings,advanced,missionEnd,objective,briefing,freezeZones,true);}
        // A second window/read renews Workbench's global revision even without an edit.
        // Only retry a rejected write, once, after comparing the complete relevant data.
        static object GuardData(string operation,FoundationReply view)
        {
            switch(operation){
                case "apply":return view.Details;
                case "settings_apply":return view.Settings;
                case "advanced_apply":return view.Advanced;
                case "freezezones_apply":return view.FreezeZones;
                case "end_apply":return view.MissionEnd;
                case "objective_apply":case "objective_remove":if(view.Objectives!=null)view.Objectives.Normalize();return view.Objectives;
                case "brief_apply":case "brief_remove":case "brief_up":case "brief_down":case "brief_move":if(view.Briefing!=null)view.Briefing.Normalize();return view.Briefing;
                case "group_update":case "group_move":case "faction_update":return view.Forces;
                case "save":return new object[]{view.Details,view.Forces,view.Settings,view.Advanced,view.FreezeZones,view.MissionEnd,view.Objectives,view.Briefing};
                default:return null;
            }
        }
        private FoundationReply ExecuteCore(string operation,FoundationReply view,MissionDetails details,ForcesEdit edit,MissionSettings settings,AdvancedSettings advanced,MissionEndData missionEnd,ObjectiveEdit objective,BriefingEdit briefing,FreezeZoneSettings freezeZones,bool retryStale)
        {
            if(operation!="snapshot"&&operation!="validate")
                foreach(var entry in fingerprints)if(Hash(entry.Key)!=entry.Value)
                    throw new IOException("A mission config changed on disk. Reopen the world and Refresh to reload its resources. Your draft is kept.");
            if(operation=="create")
            {
                // Validate world identity and conflicts before touching source directories.
                var prepared=Execute("prepare",view,null);
                if(prepared.Ok!=1)return prepared;
                if(prepared.CanCreate!=1)throw new IOException("Resolve the foundation issues before creating files.");
                view=prepared;PrepareDirectories(view);
            }
            if(operation=="faction_create")
            {
                var prepared=Execute("faction_prepare",view,null,edit);
                if(prepared.Ok!=1)return prepared;
                // Preflight refreshes the native revision without changing data.
                // Keep the caller's revision usable even if exclusive file creation
                // fails, so correcting the key in the open dialog can be retried.
                if(view!=null)view.ViewRevision=prepared.ViewRevision;
                PrepareFactionFile(prepared);view=prepared;
            }
            var request=new Dictionary<string,object>{{"APIFunc","tvtLobby_MissionFoundation"},{"Token",session.Token},{"Operation",operation},
                {"ExpectedWorld",view==null?"":view.WorldPath},{"ViewRevision",view==null?"":view.ViewRevision},{"PagedChoices",1},{"KnownCatalogRevision",catalogRevision},{"Details",details},{"Edit",edit},{"Settings",settings},{"FreezeZones",freezeZones},{"Advanced",advanced},{"MissionEnd",missionEnd},{"Objective",objective},{"BriefingEdit",briefing}};
            string saveJournal=operation=="save"?EditorSaveRecovery.Begin(view):null;
            FoundationReply reply;
            try
            {
                reply=Read(transport(session.Port,request,60000));
                if(saveJournal!=null)EditorSaveRecovery.Finish(saveJournal,reply!=null&&reply.Ok==1,reply==null?"No reply":reply.Message);
            }
            catch(Exception error)
            {
                if(saveJournal!=null)
                {
                    EditorSaveRecovery.Finish(saveJournal,false,error.Message);
                    foreach(string path in new List<string>(fingerprints.Keys))fingerprints[path]=Hash(path);
                    throw new IOException(error.Message+"\nSave recovery: "+saveJournal+". Retry Save mission to finish the interrupted save.",error);
                }
                throw;
            }
            if(saveJournal!=null&&reply!=null&&reply.Ok!=1)
            {
                foreach(string path in new List<string>(fingerprints.Keys))fingerprints[path]=Hash(path);
                reply.Message+="\nOriginal files are preserved at "+saveJournal+". Retry Save mission to finish.";
            }
            if(reply==null)throw new IOException("Empty Workbench response.");
            if(retryStale&&reply.Ok!=1&&reply.Code=="STALE_VIEW"&&view!=null)
            {
                object guard=GuardData(operation,view);
                if(guard!=null){
                    string expected=EditorJson.Serialize(guard);
                    var fresh=ExecuteCore("snapshot",view,null,null,null,null,null,null,null,null,false);
                    if(fresh.Ok!=1)return fresh;
                    if(view.WorldPath!=fresh.WorldPath||view.WorldSession!=fresh.WorldSession||view.HeaderPath!=fresh.HeaderPath||view.RulesPath!=fresh.RulesPath)
                        return new FoundationReply{Code="WORLD_CHANGED",Message="The mission changed in Workbench. Nothing was applied or saved; your draft is kept. Refresh to review the current world."};
                    if(expected!=EditorJson.Serialize(GuardData(operation,fresh)))
                        return new FoundationReply{Code="VALUE_CHANGED",Message="These settings or their references changed in Workbench. Nothing was applied or saved; your draft is kept. Refresh to review the changes."};
                    return ExecuteCore(operation,fresh,details,edit,settings,advanced,missionEnd,objective,briefing,freezeZones,false);
                }
            }
            if(reply.Ok==1)
            {
                LoadChoices(reply);
                if((operation=="snapshot"||operation=="validate")&&worldSession==reply.WorldSession)
                    foreach(var entry in fingerprints)if(Hash(entry.Key)!=entry.Value)
                        throw new IOException("A mission config changed on disk. Reopen the world and Refresh to reload its resources. Your draft is kept.");
                Remember(reply);
                string recovery=EditorSaveRecovery.Pending(reply);reply.SaveRecoveryPending=recovery!=null;
                if(recovery!=null)reply.Message+="\nAn earlier save was interrupted. Retry Save mission to finish. Original files: "+recovery;
            }
            return reply;
        }
    }
    public static class MissionDrafts
    {
        public static string PathFor(string world)
        {
            using(var hash=SHA256.Create())
            {
                string key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(world))).Replace("-","");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"tvtLobby","MissionDrafts",key+".json");
            }
        }
        public static void Save(string world,MissionDetails details)
        {
            string path=PathFor(world); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary=path+".tmp"; File.WriteAllText(temporary,details.Serialize(),Encoding.UTF8);
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        public static MissionDetails Read(string world)
        {
            string path=PathFor(world);
            return File.Exists(path)?new JavaScriptSerializer().Deserialize<MissionDetails>(File.ReadAllText(path)):null;
        }
        public static void Remove(string world) { string path=PathFor(world); if(File.Exists(path))File.Delete(path); }
    }
}
