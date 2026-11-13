using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace TvtLobby.MissionEditor
{
    public sealed class EditorSaveFile
    {
        public string Source {get;set;}
        public string Backup {get;set;}
    }
    public sealed class EditorSaveJournal
    {
        public string World {get;set;}
        public string StartedUtc {get;set;}
        public string State {get;set;}
        public string Message {get;set;}
        public List<EditorSaveFile> Files {get;set;}
    }
    public static class EditorSaveRecovery
    {
        public static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"tvtLobby","SaveRecovery");
        static string Key(FoundationReply view)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(view.AddonRoot+"|"+view.WorldPath))).Replace("-","");
        }
        static string Local(FoundationReply view,string resource)
        {
            string relative=resource??"";int brace=relative.IndexOf('}');if(brace>=0)relative=relative.Substring(brace+1);
            if(relative.StartsWith("$")){int colon=relative.IndexOf(':');if(colon<0)throw new IOException("Invalid mission world path.");relative=relative.Substring(colon+1);}
            string root=Path.GetFullPath(view.AddonRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Save recovery path is outside this mission's addon.");
            return path;
        }
        static void Write(string path,EditorSaveJournal journal)
        {
            WriteDurable(path,new JavaScriptSerializer().Serialize(journal));
        }
        public static void WriteDurable(string path,string text)
        {
            string temporary=path+".tmp";
            byte[] bytes=new UTF8Encoding(false).GetBytes(text);
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        public static string Pending(FoundationReply view)
        {
            if(view==null||String.IsNullOrEmpty(view.AddonRoot)||String.IsNullOrEmpty(view.WorldPath))return null;
            string directory=Path.Combine(Root,Key(view));string active=Path.Combine(directory,"active.txt");
            if(!File.Exists(active))return null;
            string folder=File.ReadAllText(active); // only an opaque folder name is persisted
            if(folder!=Path.GetFileName(folder))throw new IOException("Invalid save recovery record.");
            string journal=Path.Combine(directory,folder,"journal.json");
            if(!File.Exists(journal))return null;
            return new JavaScriptSerializer().Deserialize<EditorSaveJournal>(File.ReadAllText(journal)).State=="complete"?null:journal;
        }
        public static string Begin(FoundationReply view)
        {
            string pending=Pending(view);if(pending!=null)return pending;
            string directory=Path.Combine(Root,Key(view)),folder=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N");
            string destination=Path.Combine(directory,folder);
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string resource in new[]{view.HeaderPath,view.RulesPath,view.WorldPath})
            {
                if(String.IsNullOrEmpty(resource))continue;string path=Local(view,resource);paths.Add(path);paths.Add(path+".meta");
            }
            string world=Local(view,view.WorldPath),layers=Path.Combine(Path.GetDirectoryName(world),Path.GetFileNameWithoutExtension(world)+"_Layers");
            if(Directory.Exists(layers))foreach(string file in Directory.GetFiles(layers,"*",SearchOption.AllDirectories))paths.Add(file);
            var journal=new EditorSaveJournal{World=view.WorldPath,StartedUtc=DateTime.UtcNow.ToString("o"),State="prepared",Message="Save not yet confirmed. Retry Save mission to finish; original files are preserved here.",Files=new List<EditorSaveFile>()};
            try
            {
                Directory.CreateDirectory(destination);
                long bytes=0;
                foreach(string path in paths.OrderBy(p=>p))
                {
                    if(!File.Exists(path))continue;
                    bytes+=new FileInfo(path).Length;if(bytes>2L*1024*1024*1024)throw new IOException("Mission save backup exceeds 2 GiB. Save was not started.");
                    string name=journal.Files.Count.ToString("D5")+".original";File.Copy(path,Path.Combine(destination,name),false);
                    using(var flushed=new FileStream(Path.Combine(destination,name),FileMode.Open,FileAccess.ReadWrite,FileShare.Read)){flushed.Flush(true);}
                    journal.Files.Add(new EditorSaveFile{Source=path,Backup=name});
                }
                string manifest=Path.Combine(destination,"journal.json");Write(manifest,journal);
                WriteDurable(Path.Combine(directory,"active.txt"),folder);return manifest;
            }
            catch
            {
                // No WB write has occurred. This fresh generation is incomplete,
                // so do not accumulate copied layers after a failed backup.
                RemoveGeneration(directory,destination);throw;
            }
        }
        static void RemoveGeneration(string directory,string target)
        {
            string resolved=Path.GetFullPath(target),parent=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!resolved.StartsWith(parent,StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid recovery cleanup path.");
            try{if(Directory.Exists(resolved))Directory.Delete(resolved,true);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        public static void Finish(string manifest,bool success,string message)
        {
            var journal=new JavaScriptSerializer().Deserialize<EditorSaveJournal>(File.ReadAllText(manifest));
            journal.State=success?"complete":"interrupted";journal.Message=message;Write(manifest,journal);
            if(!success)return;
            string directory=Path.GetDirectoryName(Path.GetDirectoryName(manifest));
            try{File.Delete(Path.Combine(directory,"active.txt"));}catch(IOException){}catch(UnauthorizedAccessException){}
            // Three complete generations per mission; interrupted generations are retained.
            try
            {
                var complete=Directory.GetDirectories(directory).Where(d=>File.Exists(Path.Combine(d,"journal.json")))
                    .Where(d=>new JavaScriptSerializer().Deserialize<EditorSaveJournal>(File.ReadAllText(Path.Combine(d,"journal.json"))).State=="complete")
                    .OrderByDescending(d=>d).Skip(3).ToArray();
                foreach(string old in complete)RemoveGeneration(directory,old);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}
        }
    }
}
