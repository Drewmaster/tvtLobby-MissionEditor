using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class NativeFactionDialog : Form
    {
        readonly ComboBox basis=new EditorCombo();
        readonly TextBox key=new TextBox(),name=new TextBox(),flag=new TextBox();
        readonly Label path=new EditorLabel(),error=new EditorLabel();
        readonly Button color=new Button(),create=new EditorButton{Primary=true},cancel=new EditorButton();
        readonly ForcesView catalog;
        readonly Func<string,ForcesEdit,Task<FoundationReply>> send;
        bool pending;
        public NativeFactionDialog(ForcesView data,Func<string,ForcesEdit,Task<FoundationReply>> callback)
        {
            catalog=data;send=callback;Text="Create native faction";StartPosition=FormStartPosition.CenterParent;
            ClientSize=new Size(720,575);MinimumSize=new Size(650,600);BackColor=Color.FromArgb(22,28,31);ForeColor=Color.Gainsboro;Font=new Font("Segoe UI",10);
            var rows=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Padding=new Padding(20),AutoScroll=true};rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(rows);
            Add(rows,new EditorLabel{Text="Choose a native base. Callsign dictionaries, character identity, equipment catalogs and radio settings are inherited. Set mission alliances in Factions & alliances.",AutoSize=true,MaximumSize=new Size(660,0)});
            basis.DropDownStyle=ComboBoxStyle.DropDownList;
            foreach(var f in data.Available.Where(f=>!String.IsNullOrEmpty(f.TemplateResource)&&f.TemplateResource.EndsWith(".conf",StringComparison.OrdinalIgnoreCase)))basis.Items.Add(f);
            Field(rows,"Base faction",basis);key.MaxLength=48;name.MaxLength=128;Field(rows,"Engine key — unique, stable; letters, digits, underscores",key);Field(rows,"Default faction name",name);
            color.Text="Faction color";color.Height=32;color.FlatStyle=FlatStyle.Flat;Add(rows,color);
            color.Click+=(s,e)=>{using(var pick=new ColorDialog{Color=color.BackColor,FullOpen=true})if(pick.ShowDialog(this)==DialogResult.OK)SetColor(pick.Color);};
            Field(rows,"Flag / UI icon — existing .edds resource (resource path or GUID + path)",flag);
            var browse=new Button{Text="Browse .edds…",AutoSize=true};Add(rows,browse);browse.Click+=(s,e)=>BrowseFlag();
            path.AutoSize=true;path.MaximumSize=new Size(650,0);path.ForeColor=Color.FromArgb(158,178,184);Add(rows,path);
            error.AutoSize=true;error.MaximumSize=new Size(650,0);error.ForeColor=Color.FromArgb(224,175,86);Add(rows,error);
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};create.Text="Create faction";cancel.Text="Cancel";create.AutoSize=cancel.AutoSize=true;create.Height=cancel.Height=34;actions.Controls.Add(create);actions.Controls.Add(cancel);Add(rows,actions);
            cancel.Click+=(s,e)=>{if(!pending)Close();};create.Click+=async(s,e)=>await CreateFaction();
            FormClosing+=(s,e)=>{if(pending)e.Cancel=true;};
            basis.SelectedIndexChanged+=(s,e)=>{var f=basis.SelectedItem as EditorFaction;if(f==null)return;flag.Text=f.Flag;SetColor(Color.FromArgb(Clamp(f.Red),Clamp(f.Green),Clamp(f.Blue)));};
            key.TextChanged+=(s,e)=>path.Text="Config: "+data.FactionDirectory+"tvtLobby_"+key.Text+".conf\nAlso creates mission entry, Groups_"+key.Text+" and Vehicles_"+key.Text+". Save mission saves the scene. Undo keeps the config file for reuse.";
            EditorTheme.Apply(this);
            if(basis.Items.Count>0)basis.SelectedIndex=0;else {create.Enabled=false;error.Text="No registered faction with a native .conf base. Refresh after configuring FactionManager.";}
            key.Text="NEW_FACTION";
        }
        static int Clamp(int value){return Math.Max(0,Math.Min(255,value));}
        static void Add(TableLayoutPanel parent,Control child){int row=parent.RowCount++;parent.RowStyles.Add(new RowStyle(SizeType.AutoSize));parent.Controls.Add(child,0,row);child.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;child.Margin=new Padding(0,0,0,10);}
        static void Field(TableLayoutPanel parent,string label,Control field){Add(parent,new EditorLabel{Text=label,AutoSize=true});Add(parent,field);}
        void SetColor(Color value){color.BackColor=value;color.ForeColor=value.GetBrightness()>.5?Color.Black:Color.White;color.Text="Faction color · #"+value.R.ToString("X2")+value.G.ToString("X2")+value.B.ToString("X2");}
        void BrowseFlag()
        {
            using(var pick=new OpenFileDialog{Filter="Enfusion texture (*.edds)|*.edds",Title="Choose a registered faction flag"})
            {
                if(pick.ShowDialog(this)!=DialogResult.OK)return;
                string meta=pick.FileName+".meta";
                if(!File.Exists(meta)){error.Text="Choose a texture already imported by Workbench (.edds.meta required), or paste its resource path.";return;}
                var match=Regex.Match(File.ReadAllText(meta),"Name\\s+\"([^\"]+)\"");
                if(!match.Success){error.Text="Could not read the texture resource name from its .meta file.";return;}
                flag.Text=match.Groups[1].Value;error.Text="";
            }
        }
        public static string Validate(ForcesEdit edit,ForcesView data)
        {
            if(!Regex.IsMatch(edit.Key??"","^[A-Za-z][A-Za-z0-9_]{0,47}$"))return "Engine key: start with a letter; use up to 48 letters, digits or underscores.";
            if(data.Available.Any(f=>String.Equals(f.Key,edit.Key,StringComparison.OrdinalIgnoreCase)))return "This engine key is already registered.";
            if(String.IsNullOrWhiteSpace(edit.Name)||edit.Name.Length>128||edit.Name.IndexOfAny(new[]{'\r','\n'})>=0)return "Enter a single-line faction name (up to 128 characters).";
            if(String.IsNullOrWhiteSpace(edit.Flag)||!edit.Flag.EndsWith(".edds",StringComparison.OrdinalIgnoreCase))return "Choose an existing .edds flag resource.";
            if(!data.Available.Any(f=>f.Key==edit.BaseKey&&!String.IsNullOrEmpty(f.TemplateResource)))return "Choose a native base faction.";
            return null;
        }
        async Task CreateFaction()
        {
            var f=basis.SelectedItem as EditorFaction;
            var edit=new ForcesEdit{Key=key.Text.Trim(),Name=name.Text.Trim(),BaseKey=f==null?"":f.Key,Flag=flag.Text.Trim(),Red=color.BackColor.R,Green=color.BackColor.G,Blue=color.BackColor.B};
            string invalid=Validate(edit,catalog);if(invalid!=null){error.Text=invalid;return;}
            pending=true;create.Enabled=cancel.Enabled=false;error.Text="Creating native config and attaching it in Workbench…";
            try{var result=await send("faction_create",edit);if(result.Ok==1){pending=false;DialogResult=DialogResult.OK;Close();}else error.Text=result.Message;}
            catch(Exception ex){error.Text=ex.Message;}
            finally{pending=false;if(!IsDisposed)create.Enabled=cancel.Enabled=true;}
        }
    }
}
