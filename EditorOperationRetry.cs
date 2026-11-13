using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace TvtLobby.MissionEditor
{
    // Serialized before sending, so a window/process failure cannot silently lose
    // the identity of an operation whose response was lost.
    public sealed class PendingEditorOperation
    {
        public string Token {get;set;}
        public string Id {get;set;}
        public string Operation {get;set;}
        public string Semantic {get;set;}
        public string Request {get;set;}
    }
    public static class EditorOperationRetry
    {
        static readonly object Gate=new object();
        static readonly HashSet<string> Reads=new HashSet<string>{"snapshot","validate","prepare","faction_prepare","choices","background"};
        public static string DirectoryPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"tvtLobby","PendingOperations");
        static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=32*1024*1024};}
        static string Get(IDictionary<string,object> data,string key){object value;return data.TryGetValue(key,out value)?Convert.ToString(value):"";}
        public static string Call(int port,object request,int timeout,Func<int,object,int,string> send)
        {
            var fields=request as IDictionary<string,object>;
            if(fields==null||Reads.Contains(Get(fields,"Operation"))||String.IsNullOrEmpty(Get(fields,"Operation")))return send(port,request,timeout);
            lock(Gate)
            {
                var copy=new Dictionary<string,object>(fields);
                var semantic=new Dictionary<string,object>(copy);
                foreach(string key in new[]{"ViewRevision","KnownCatalogRevision","OperationId"})semantic.Remove(key);
                string signature=EditorJson.SerializeRequest(semantic),token=Get(copy,"Token"),operation=Get(copy,"Operation");
                string file=Path.Combine(DirectoryPath,port+".json");
                PendingEditorOperation pending=null;
                if(File.Exists(file))pending=Json().Deserialize<PendingEditorOperation>(File.ReadAllText(file));
                if(pending!=null&&pending.Token!=token)pending=null; // A new authenticated WB session cannot replay old edits.
                bool recovered=pending!=null;
                if(pending!=null&&pending.Semantic!=signature)
                {
                    string status=send(port,new Dictionary<string,object>{{"APIFunc","tvtLobby_EditorOperationStatus"},{"Token",token},{"OperationId",pending.Id},{"Protocol",WorkbenchClient.EditorProtocol}},Math.Min(timeout,4000));
                    var result=Json().Deserialize<Dictionary<string,object>>(status);
                    string state=Get(result,"State");
                    if(state=="complete"||state=="expired")
                    {
                        File.Delete(file);
                        throw new IOException("Workbench received the previous '"+pending.Operation+"' operation. Refresh to inspect its result, then retry this change.");
                    }
                    throw new IOException("The result of the previous '"+pending.Operation+"' operation is still unconfirmed. Retry that same action first. Your drafts are kept.");
                }
                if(pending==null)
                {
                    string id=Guid.NewGuid().ToString("D");copy["OperationId"]=id;
                    pending=new PendingEditorOperation{Token=token,Id=id,Operation=operation,Semantic=signature,Request=EditorJson.SerializeRequest(copy)};
                    Directory.CreateDirectory(DirectoryPath);
                    EditorSaveRecovery.WriteDurable(file,Json().Serialize(pending));
                }
                try
                {
                    string reply;
                    try {reply=send(port,pending.Request,timeout);}
                    catch(IOException)
                    {
                        // The exact same ID and payload, including the old revision.
                        // The server checks replay identity before optimistic revisions.
                        recovered=true;reply=send(port,pending.Request,Math.Min(timeout,4000));
                    }
                    if(recovered)
                    {
                        var original=Json().Deserialize<Dictionary<string,object>>(reply);object ok;
                        if(original.TryGetValue("Ok",out ok)&&Convert.ToBoolean(ok))
                        {
                            // A Refresh (or process restart) may have renewed revisions
                            // and discarded catalogs since the original response.
                            var refresh=new Dictionary<string,object>(copy);refresh["Operation"]="snapshot";refresh.Remove("OperationId");
                            var current=Json().Deserialize<Dictionary<string,object>>(send(port,refresh,timeout));
                            if(!current.TryGetValue("Ok",out ok)||!Convert.ToBoolean(ok))throw new IOException("The operation completed, but the current world could not be refreshed. "+Get(current,"Message"));
                            object selected;if(original.TryGetValue("SelectedKey",out selected))current["SelectedKey"]=selected;
                            current["Message"]="Recovered '"+operation+"'. "+Get(original,"Message");reply=Json().Serialize(current);
                        }
                    }
                    var outcome=Json().Deserialize<Dictionary<string,object>>(reply);
                    string code=Get(outcome,"Code");
                    if(code=="OPERATION_EXPIRED"||code=="OPERATION_CHANGED")
                        throw new IOException(Get(outcome,"Message")+" The original operation ID has been retained; refresh and reconcile the earlier change before continuing.");
                    File.Delete(file);return reply;
                }
                catch(IOException error)
                {
                    throw new IOException(error.Message+"\nThe result of '"+operation+"' is not confirmed. Retry the same action after reconnecting; it will not be applied twice.",error);
                }
            }
        }
    }
}
