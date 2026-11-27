using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class MissionMod
    {
        public string Id{get;set;} public string Name{get;set;} public string Version{get;set;} public string Reason{get;set;} public int Manual{get;set;}
        public MissionMod(){Id=Name=Version=Reason="";Manual=1;}
        public MissionMod Clone(){return (MissionMod)MemberwiseClone();}
        public override string ToString(){return Name+"  ["+Id+"]"+(String.IsNullOrEmpty(Version)?"":"  "+Version);}
        public static MissionMod FromFolder(string path)
        {
            string folder=Directory.Exists(path)?Path.GetFullPath(path):Path.GetDirectoryName(Path.GetFullPath(path));
            string project=Path.Combine(folder,"addon.gproj"),data=Path.Combine(folder,"ServerData.json");
            var item=new MissionMod{Reason="Added from addon folder"};
            if(File.Exists(data))
            {
                var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(ReadSmall(data));object value;
                if(json.TryGetValue("id",out value))item.Id=Convert.ToString(value);
                if(json.TryGetValue("name",out value))item.Name=Convert.ToString(value);
                if(json.TryGetValue("revision",out value)) {var revision=value as Dictionary<string,object>;if(revision!=null&&revision.TryGetValue("version",out value))item.Version=Convert.ToString(value);}
            }
            if(File.Exists(project))
            {
                string text=ReadSmall(project),guid=Property(text,"GUID");
                if(!String.IsNullOrEmpty(item.Id)&&!String.Equals(item.Id,guid,StringComparison.OrdinalIgnoreCase))throw new IOException("addon.gproj and ServerData.json identify different addons.");
                item.Id=guid;if(String.IsNullOrWhiteSpace(item.Name))item.Name=Property(text,"TITLE");if(String.IsNullOrWhiteSpace(item.Name))item.Name=Property(text,"ID");
            }
            item.Id=(item.Id??"").ToUpperInvariant();
            if(!Regex.IsMatch(item.Id,"^[0-9A-F]{16}$"))throw new IOException("Select an addon folder containing addon.gproj or ServerData.json with a valid addon ID.");
            if(String.IsNullOrWhiteSpace(item.Name))item.Name=item.Id;return item;
        }
        static string ReadSmall(string path){if(new FileInfo(path).Length>4*1024*1024)throw new IOException("Addon metadata is unexpectedly large.");return File.ReadAllText(path);}
        static string Property(string text,string name){var match=Regex.Match(text,"(?m)^\\s*"+name+"\\s+\"([^\"]+)\"",RegexOptions.IgnoreCase);return match.Success?match.Groups[1].Value:"";}
    }
    public sealed class MissionMods
    {
        public int Schema{get;set;} public int Declared{get;set;}
        public List<MissionMod> Entries{get;set;} public List<MissionMod> Loaded{get;set;} public List<MissionMod> Mandatory{get;set;} public List<string> Excluded{get;set;}
        public MissionMods(){Schema=1;Entries=new List<MissionMod>();Loaded=new List<MissionMod>();Mandatory=new List<MissionMod>();Excluded=new List<string>();}
        public MissionMods Clone(){return new MissionMods{Schema=Schema,Declared=Declared,Entries=Entries.Select(x=>x.Clone()).ToList(),Loaded=Loaded.Select(x=>x.Clone()).ToList(),Mandatory=Mandatory.Select(x=>x.Clone()).ToList(),Excluded=new List<string>(Excluded)};}
        public string Fingerprint(){return EditorJson.Serialize(new{Schema,Declared,Entries,Excluded});}
        public string Validate()
        {
            if(Schema!=1||Entries==null||Excluded==null||Entries.Count>256||Excluded.Count>256)return "Use no more than 256 addons.";
            var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in Entries)if(item==null||!Regex.IsMatch(item.Id??"","^[0-9A-Fa-f]{16}$")||!ids.Add(item.Id)||Excluded.Contains(item.Id)||(item.Name??"").Length>128||(item.Version??"").Length>64||(item.Reason??"").Length>2000)return "Check addon IDs, duplicates, names and version lengths.";
            return null;
        }
        public void Merge(IEnumerable<MissionMod> items,bool manual)
        {
            foreach(var source in items)
            {
                string id=(source.Id??"").ToUpperInvariant();if(!manual&&Excluded.Contains(id))continue;
                if(manual)Excluded.Remove(id);
                var current=Entries.FirstOrDefault(x=>String.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase));
                if(current==null){current=source.Clone();current.Id=id;current.Manual=manual?1:0;Entries.Add(current);}
                else if(current.Manual==0){current.Name=source.Name;current.Version=source.Version;current.Reason=source.Reason;if(manual)current.Manual=1;}
            }
            Declared=1;
        }
        public string Export()
        {
            string error=Validate();if(error!=null)throw new InvalidOperationException(error);
            var json=new JavaScriptSerializer();
            var complete=Entries.Concat(Mandatory).GroupBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();
            error=new MissionMods{Entries=complete}.Validate();if(error!=null)throw new InvalidOperationException(error);
            if(complete.Count==0)throw new InvalidOperationException("There are no mods to export.");
            return String.Join(",\r\n\t",complete.Select(x=>"{\r\n\t\t\"modId\": "+json.Serialize(x.Id.ToUpperInvariant())+",\r\n\t\t\"name\": "+json.Serialize(x.Name??x.Id)+",\r\n\t\t\"version\": "+json.Serialize(x.Version??"")+"\r\n\t}"));
        }
        public MissionMods WithClipboardImport(string text)
        {
            if(String.IsNullOrWhiteSpace(text))throw new FormatException("The clipboard is empty. Copy a mod list first.");
            if(text.Length>1024*1024)throw new FormatException("The clipboard mod list is too large.");
            string input=text.Trim().TrimStart('\uFEFF').Trim();
            // Native exports contain comma-separated objects without the surrounding array.
            if(!input.StartsWith("[",StringComparison.Ordinal))input="["+input.TrimEnd().TrimEnd(',')+"]";
            object[] rows;
            try { rows=new JavaScriptSerializer{MaxJsonLength=1024*1024+2,RecursionLimit=16}.DeserializeObject(input) as object[]; }
            catch(ArgumentException) { throw new FormatException("The clipboard is not a valid mod list. Copy objects with modId, name and version."); }
            catch(InvalidOperationException) { throw new FormatException("The clipboard is not a valid mod list. Copy objects with modId, name and version."); }
            if(rows==null||rows.Length==0)throw new FormatException("The clipboard does not contain any mods.");
            if(rows.Length>256)throw new FormatException("Use no more than 256 addons.");
            var result=Clone();
            for(int index=0;index<rows.Length;index++)
            {
                var row=rows[index] as Dictionary<string,object>;object id,name,version;
                string problem="Check mod "+(index+1)+": modId must be 16 hexadecimal characters; name and version must be text.";
                if(row==null||!row.TryGetValue("modId",out id)||!(id is string)||!Regex.IsMatch((string)id,"^[0-9A-Fa-f]{16}$"))throw new FormatException(problem);
                row.TryGetValue("name",out name);row.TryGetValue("version",out version);
                if((name!=null&&!(name is string))||(version!=null&&!(version is string)))throw new FormatException(problem);
                string guid=((string)id).ToUpperInvariant();
                var item=result.Entries.FirstOrDefault(x=>String.Equals(x.Id,guid,StringComparison.OrdinalIgnoreCase));
                if(item==null){item=new MissionMod();result.Entries.Add(item);}
                item.Id=guid;item.Name=String.IsNullOrWhiteSpace(name as string)?guid:(string)name;item.Version=version as string??"";
                item.Reason="Imported from clipboard";item.Manual=1;
                result.Excluded.RemoveAll(x=>String.Equals(x,guid,StringComparison.OrdinalIgnoreCase));
            }
            result.Declared=1;
            string error=result.Validate();if(error!=null)throw new FormatException(error);
            return result;
        }
    }
    public sealed class ModScan
    {
        public int Complete{get;set;} public int Resources{get;set;} public List<MissionMod> Found{get;set;} public List<string> Issues{get;set;}
    }
    public sealed class ModsPage : UserControl
    {
        readonly Func<string,MissionMods,Task<FoundationReply>> send;
        readonly DataGridView grid=new DataGridView(); readonly Label note=EditorTheme.Label(""),message=EditorTheme.Label("",9,EditorTheme.Muted);
        readonly TextBox reason=new TextBox{Multiline=true,ReadOnly=true,Height=90,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
        MissionMods draft=new MissionMods(); string baseline="",world="";bool loading,conflict;
        public event Action DraftChanged;
        public bool HasDraft{get{return draft.Fingerprint()!=baseline;}}
        public string DraftError{get{return conflict?"The mod list changed in Workbench. Reload before applying.":ReadFields().Validate();}}
        public MissionMods ReadFields(){grid.EndEdit();return draft.Clone();}
        public ModsPage(Func<string,MissionMods,Task<FoundationReply>> action)
        {
            send=action;Dock=DockStyle.Fill;BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;Font=EditorTheme.Font();
            var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,94));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
            note.Text="Add the mods this mission needs. For mods chosen by scripts, use Loaded addons or Add folder. The server export always includes tvtLobby and the mission pack.";note.MaximumSize=new Size(1100,0);SizeChanged+=(s,e)=>{note.MaximumSize=new Size(Math.Max(240,ClientSize.Width-24),0);message.MaximumSize=new Size(Math.Max(180,ClientSize.Width-280),0);};panel.Controls.Add(note,0,0);
            var toolbar=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
            toolbar.Controls.Add(EditorTheme.Button("Scan resources",async()=>await Scan()));toolbar.Controls.Add(EditorTheme.Button("Loaded addons...",ChooseLoaded));toolbar.Controls.Add(EditorTheme.Button("Add folder...",AddFolder));toolbar.Controls.Add(EditorTheme.Button("Remove",Remove));toolbar.Controls.Add(EditorTheme.Button("Restore exclusions...",RestoreExcluded));toolbar.Controls.Add(EditorTheme.Button("Import from clipboard",Import));toolbar.Controls.Add(EditorTheme.Button("Export to clipboard",Export));panel.Controls.Add(toolbar,0,1);
            grid.Dock=DockStyle.Fill;grid.AutoGenerateColumns=false;grid.AllowUserToAddRows=grid.AllowUserToDeleteRows=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.RowHeadersVisible=false;
            foreach(var column in new[]{new[]{"Name","Addon"},new[]{"Id","ID"},new[]{"Version","Required version"},new[]{"Reason","Why required"}})
                grid.Columns.Add(new DataGridViewTextBoxColumn{Name=column[0],HeaderText=column[1],DataPropertyName=column[0],ReadOnly=column[0]!="Version",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=column[0]=="Reason"?180:100});
            grid.Columns.Add(new DataGridViewTextBoxColumn{Name="WorkbenchStatus",HeaderText="Workbench",ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=80});
            grid.CellFormatting+=(s,e)=>{if(e.RowIndex>=0&&grid.Columns[e.ColumnIndex].Name=="WorkbenchStatus"){var item=grid.Rows[e.RowIndex].DataBoundItem as MissionMod;e.Value=item!=null&&draft.Loaded.Any(x=>String.Equals(x.Id,item.Id,StringComparison.OrdinalIgnoreCase))?"Loaded":"Not loaded";e.FormattingApplied=true;}};
            grid.CellValueChanged+=(s,e)=>{if(!loading){if(e.RowIndex>=0){var edited=grid.Rows[e.RowIndex].DataBoundItem as MissionMod;if(edited!=null)edited.Manual=1;}Changed();}};grid.SelectionChanged+=(s,e)=>{var item=grid.CurrentRow==null?null:grid.CurrentRow.DataBoundItem as MissionMod;reason.Text=item==null?"":(item.Manual==0?"Detected from resources\r\n":"Added by author\r\n")+item.Reason;};panel.Controls.Add(grid,0,2);panel.Controls.Add(reason,0,3);
            var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};footer.Controls.Add(EditorTheme.Button("Apply",async()=>await ApplyPending(),true));footer.Controls.Add(EditorTheme.Button("Reload fields",Reload));footer.Controls.Add(message);panel.Controls.Add(footer,0,4);Controls.Add(panel);EditorTheme.Apply(this);
        }
        MissionMods latest;
        public void Accept(FoundationReply view,bool replace)
        {
            latest=(view.Mods??new MissionMods()).Clone();
            if(replace||world!=view.WorldPath||!HasDraft){draft=latest.Clone();baseline=draft.Fingerprint();conflict=false;Bind();}
            else if(latest.Fingerprint()!=baseline)conflict=true;
            draft.Loaded=latest.Loaded.Select(x=>x.Clone()).ToList();draft.Mandatory=latest.Mandatory.Select(x=>x.Clone()).ToList();grid.Invalidate();world=view.WorldPath;
            if(view.Mods==null)message.Text="Update the addon and restart Workbench to use Mods.";else if(conflict)message.Text="Workbench changed. Your draft is kept; Reload fields before applying.";
        }
        void Reload(){if(latest==null)return;draft=latest.Clone();baseline=draft.Fingerprint();conflict=false;Bind();Changed();}
        void Bind(){loading=true;grid.DataSource=null;grid.DataSource=draft.Entries;loading=false;}
        void Changed(){if(!loading){message.Text=draft.Entries.Count+" required addons · "+draft.Excluded.Count+" excluded from scans";if(DraftChanged!=null)DraftChanged();}}
        public void Applied(){baseline=draft.Fingerprint();conflict=false;}
        public bool CanLeave(){return !HasDraft||MessageBox.Show(this,"The Mods draft has not been applied. Close and discard this draft?","Unsaved mod list",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;}
        public async Task<bool> ApplyPending()
        {
            var data=ReadFields();data.Declared=1;string error=DraftError;if(error!=null){message.Text=error;return false;}
            var result=await send("mods_apply",data);if(result.Ok!=1){message.Text=result.Message;return false;}return true;
        }
        async Task Scan()
        {
            var result=await send("mods_scan",null);if(result.Ok!=1||result.ModScan==null){message.Text=result.Message;return;}
            var scan=result.ModScan;
            // An incomplete scan may add evidence, but must never erase existing requirements.
            if(scan.Complete!=0)draft.Entries.RemoveAll(x=>x.Manual==0);
            draft.Merge(scan.Found??new List<MissionMod>(),false);Bind();Changed();
            message.Text=(scan.Complete!=0?"Scan complete":"Incomplete scan; old requirements retained")+" · "+scan.Resources+" resources";
            if(scan.Issues!=null&&scan.Issues.Count>0)reason.Text=String.Join("\r\n",scan.Issues);
        }
        void ChooseLoaded()
        {
            using(var dialog=new Form{Text="Loaded addons — Open with mods",Size=new Size(700,560),StartPosition=FormStartPosition.CenterParent,BackColor=EditorTheme.Background,ForeColor=EditorTheme.Text})
            {
                var list=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};foreach(var item in draft.Loaded)list.Items.Add(item,false);
                var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};buttons.Controls.Add(EditorTheme.Button("Select all",()=>{for(int i=0;i<list.Items.Count;i++)list.SetItemChecked(i,true);}));buttons.Controls.Add(EditorTheme.Button("Add selected",()=>{draft.Merge(list.CheckedItems.Cast<MissionMod>(),true);dialog.DialogResult=DialogResult.OK;dialog.Close();},true));dialog.Controls.Add(list);dialog.Controls.Add(buttons);EditorTheme.Apply(dialog);if(dialog.ShowDialog(this)==DialogResult.OK){Bind();Changed();}
            }
        }
        void AddFolder()
        {
            using(var dialog=new FolderBrowserDialog{Description="Select an addon folder containing addon.gproj or ServerData.json",ShowNewFolderButton=false})if(dialog.ShowDialog(this)==DialogResult.OK)
                try{draft.Merge(new[]{MissionMod.FromFolder(dialog.SelectedPath)},true);Bind();Changed();}catch(Exception e){message.Text=e.Message;}
        }
        void Remove(){var items=grid.SelectedRows.Cast<DataGridViewRow>().Select(x=>x.DataBoundItem as MissionMod).Where(x=>x!=null).ToList();foreach(var item in items){draft.Entries.Remove(item);if(!draft.Excluded.Contains(item.Id))draft.Excluded.Add(item.Id);}draft.Declared=1;Bind();Changed();}
        void RestoreExcluded(){if(draft.Excluded.Count==0)return;if(MessageBox.Show(this,"Allow all previously removed addons to appear in resource scans again?","Restore exclusions",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;draft.Excluded.Clear();Changed();}
        void Export()
        {
            try{Clipboard.SetText(ReadFields().Export(),TextDataFormat.UnicodeText);message.Text="Copied to clipboard. Paste inside the server's mods array.";}
            catch(ExternalException){message.Text="The clipboard is busy. Try exporting again.";}
            catch(Exception e){message.Text=e.Message;}
        }
        void Import()
        {
            try{draft=ReadFields().WithClipboardImport(Clipboard.GetText(TextDataFormat.UnicodeText));Bind();Changed();message.Text="Imported to the draft. Matching mods were updated; click Apply to save.";}
            catch(ExternalException){message.Text="The clipboard is busy. Try importing again.";}
            catch(Exception e){message.Text=e.Message;}
        }
    }
}
