using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace TvtLobby.MissionEditor
{
    public sealed partial class EditorWindow
    {
        Label worldTitle,breadcrumb,subtitle,foundationTitle;
        TableLayoutPanel resourceRows,validationRows;
        readonly Dictionary<string,ResourcePreview> imagePreviews=new Dictionary<string,ResourcePreview>();
        string PageDescription(string name)
        {
            switch(name){case "Mission guide":return "A step-by-step route through authoring. Open any section at any time.";case "Files & setup":return "Create the common foundation once. Existing resources are reused.";case "Mission details":return "Public information, player count and mission artwork.";case "Mission settings":return "Frequently changed conditions for this mission.";case "Advanced settings":return "Rules grouped by behaviour. Defaults are explicit; each value is stored once.";case "Factions & alliances":return "Real faction keys, inherited names and mission-specific alliances.";case "Groups & vehicles":return "Arrange playable groups and assign transport from each faction’s vehicle layer.";case "Validation":return "Check the current mission and resolve authoring issues.";case "Objectives":return "Guided creation; direct editing afterwards. Scene references, conditions and feedback stay together.";case "Mission end":return "Separate rules, shared resolution: objectives → critical losses → time.";case "Briefing":return "Manual sections, formatted text, illustrations and internal links. Visibility follows the mission rules.";case "Map, markers & zones":return "Place and edit markers, lines and zones on the terrain map.";default:return "Authoring tools for the current mission.";}
        }
        Control BuildFiles(out Button createButton)
        {
            var p=Page();var hero=EditorTheme.Stack(20);hero.BackColor=EditorTheme.Panel;EditorTheme.Add(hero,EditorTheme.Label("MISSION FOUNDATION",8,EditorTheme.Muted));foundationTitle=EditorTheme.Label("Read the current world",17,null,true);EditorTheme.Add(hero,foundationTitle);EditorTheme.Add(hero,EditorTheme.Label("Header, rules, logic, zones and markers layers; GameMode, Objectives, Mission End and Briefing instances.",10));EditorTheme.Add(hero,EditorTheme.Label("Groups_<key> and Vehicles_<key> are created when you add a faction. Opening this editor creates nothing.",9,EditorTheme.Muted));createButton=Button("Create mission foundation",async()=>await Execute("create"),true);createButton.Anchor=AnchorStyles.Left;EditorTheme.Add(hero,createButton);Add(p,hero);
            resourceRows=EditorTheme.Stack();Add(p,resourceRows);Add(p,EditorTheme.Label("Scene edits use native Undo. Save mission writes the world and configs. Existing resources and manual edits are retained.",9,EditorTheme.Muted));Add(p,Button("Continue with mission details →",()=>ShowPage("Mission details")));return p;
        }
        Control BuildValidation(){var p=Page();Add(p,EditorTheme.Label("Foundation & metadata",14,null,true));Add(p,EditorTheme.Label("Checks the current world, duplicate core objects, required resources and available slots. Objectives and runtime checks remain in the Workbench validation tool.",9,EditorTheme.Muted));validationRows=EditorTheme.Stack();Add(p,validationRows);return p;}
        void RefreshPresentation(FoundationReply reply)
        {
            ClearRows(resourceRows);int missing=reply.Items.Count(x=>x.State!="Present"&&x.State!="Inherited");foundationTitle.Text=reply.Ready==1&&missing==0?"Foundation ready":"Mission foundation · "+reply.Items.Count+" elements";
            foreach(var item in reply.Items){var row=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=2,Padding=new Padding(0,14,0,14),Margin=Padding.Empty};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));var text=EditorTheme.Stack();EditorTheme.Add(text,EditorTheme.Label(item.Name,10,null,true));EditorTheme.Add(text,EditorTheme.Label(item.Path,8.5f,EditorTheme.Muted));row.Controls.Add(text);row.Controls.Add(EditorTheme.Label(item.State,9,item.State=="Present"?EditorTheme.Green:EditorTheme.Amber));row.Paint+=(s,e)=>{using(var pen=new Pen(EditorTheme.Line))e.Graphics.DrawLine(pen,0,row.Height-1,row.Width,row.Height-1);};EditorTheme.Add(resourceRows,row);}
            ClearRows(validationRows);var list=reply.Issues??new List<string>();foreach(string problem in list){var card=EditorTheme.Stack(16);card.BackColor=EditorTheme.Panel;card.Margin=new Padding(0,0,0,10);EditorTheme.Add(card,EditorTheme.Label("REVIEW",8,EditorTheme.Amber));EditorTheme.Add(card,EditorTheme.Label(problem));EditorTheme.Add(validationRows,card);}if(list.Count==0)EditorTheme.Add(validationRows,EditorTheme.Label("✓ Foundation checks passed",12,EditorTheme.Green));foreach(Control c in navigation.Controls)if(c is EditorButton&&(string)c.Tag=="Validation"){((EditorButton)c).Badge=list.Count==0?"":list.Count.ToString();c.Invalidate();}
            foreach(var preview in imagePreviews.Values)preview.Root=reply.AddonRoot;
        }
        static void ClearRows(TableLayoutPanel p){while(p.Controls.Count>0)p.Controls[0].Dispose();p.RowStyles.Clear();p.RowCount=0;}
        Control BuildGuide()
        {
            var p=Page();Add(p,EditorTheme.Label("Create, place, review",15,null,true));Add(p,EditorTheme.Label("After mission setup, place groups and vehicles first. Then prepare the map, markers, lines and zones, including freeze areas. Configure objectives using those prepared objects. You can return to any section at any time.",10,EditorTheme.Muted));
            string[] names={"Files & setup","Mission details","Factions & alliances","Mission settings","Advanced settings","Groups & vehicles","Map, markers & zones","Objectives","Mission end","Briefing","Validation"};
            string[] help={
                "Save your world, then create the missing files, layers and core instances in one action.",
                "Enter the name, author, player count, description and mission images.",
                "Add participating factions, choose display names and configure alliances.",
                "Set freeze readiness, cleanup and the time/weather overrides.",
                "Review communication, spectator cameras, map markers, notifications and native permissions.",
                "Place the required groups and vehicles in World Editor. Then set native callsigns, lobby order and transport assignments here.",
                "Prepare the terrain map before configuring objectives: place markers, draw lines and areas, create objective zones, battlefield boundaries and manual freeze areas. Set marker appearance and visibility here.",
                "Add objectives step by step, selecting the groups, vehicles, targets and zones already prepared. Configure conditions, dependencies and feedback; return to the map whenever you need to adjust an object.",
                "Configure objective victories, per-side critical losses, timeout results and shared result messages.",
                "Write and format sections, insert illustrations and links to the prepared map objects, then set audiences, phases and objective events.",
                "Review issues, save the mission, then run the Workbench checks and a Play test."
            };
            for(int i=0;i<names.Length;i++){string dest=names[i];var row=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,BackColor=EditorTheme.Panel,Padding=new Padding(18),Margin=new Padding(0,0,0,12)};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,40));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));row.Controls.Add(EditorTheme.Label((i+1).ToString("00"),15,EditorTheme.Muted));var copy=EditorTheme.Stack();EditorTheme.Add(copy,EditorTheme.Label(dest,12,null,true));EditorTheme.Add(copy,EditorTheme.Label(help[i],9,EditorTheme.Muted));row.Controls.Add(copy);row.Controls.Add(Button("Open section →",()=>ShowPage(dest)));Add(p,row);}return p;
        }
        Control BuildDetails(out Button applyButton,out Button restoreButton)
        {
            var p=Page();Add(p,EditorTheme.Pair(TextField("Title","Mission name"),TextField("Author","Author")));Add(p,TextField("Description","Short description",true));
            var players=EditorTheme.Stack(18);players.BackColor=EditorTheme.Panel;EditorTheme.Add(players,EditorTheme.Label("Players",11,EditorTheme.Amber,true));
            foreach(var n in new[]{maximum}){n.Minimum=1;n.Maximum=4096;n.ValueChanged+=(s,e)=>Changed();}
            EditorTheme.Add(players,EditorTheme.Field("Max players",maximum));autoMax.Text="Use available slot count for maximum";autoMax.AutoSize=true;autoMax.CheckedChanged+=(s,e)=>{maximum.Enabled=!autoMax.Checked;if(!loading&&autoMax.Checked&&view!=null)maximum.Value=Math.Max(1,Math.Min(4096,view.Slots));Changed();};EditorTheme.Add(players,autoMax);slotCount.AutoSize=true;slotCount.ForeColor=EditorTheme.Muted;slotCount.Font=EditorTheme.Font(8.5f);EditorTheme.Add(players,slotCount);Add(p,players);
            var art=EditorTheme.Stack(18);art.BackColor=EditorTheme.Panel;EditorTheme.Add(art,EditorTheme.Label("Mission images",11,EditorTheme.Amber,true));var cards=new TableLayoutPanel{ColumnCount=3,Dock=DockStyle.Top,AutoSize=true};for(int i=0;i<3;i++)cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.33f));
            foreach(string key in new[]{"Icon","Preview","Loading"}){string field=key;var card=EditorTheme.Stack(8);EditorTheme.Add(card,EditorTheme.Label(key=="Icon"?"Mission icon":key=="Preview"?"Scenario preview":"Loading screen",10,null,true));var preview=new ResourcePreview{Height=150,Dock=DockStyle.Top};imagePreviews[key]=preview;EditorTheme.Add(card,preview);var input=new TextBox{Multiline=true,Height=52,MaxLength=1024};fields[key]=input;input.TextChanged+=(s,e)=>{preview.Resource=input.Text;Changed();};EditorTheme.Add(card,input);var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};buttons.Controls.Add(Button("Choose .edds…",()=>PickImage(field)));buttons.Controls.Add(Button("Default",()=>fields[field].Text=field=="Icon"?"{6CFADAEE9287D1D2}UI/Textures/MissionThumbnails/Default.edds":"{C58FCC06AF13075B}UI/Textures/MissionLoadingScreens/placeholder_1.edds"));EditorTheme.Add(card,buttons);card.Dock=DockStyle.Fill;cards.Controls.Add(card);}EditorTheme.Add(art,cards);EditorTheme.Add(art,EditorTheme.Label("Choose registered .edds resources. Preview keeps the image proportions. PNG/JPG import is a later feature.",8.5f,EditorTheme.Muted));Add(p,art);
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};applyButton=Button("Apply details",async()=>await Execute("apply"),true);actions.Controls.Add(applyButton);actions.Controls.Add(Button("Reload fields",()=>{if(view!=null){MissionDrafts.Remove(view.WorldPath);LoadFields(view.Details);}}));restoreButton=Button("Restore local draft",()=>{if(view==null)return;var d=MissionDrafts.Read(view.WorldPath);if(d!=null){LoadFields(d);baseline=view.Details.Serialize();dirty=true;UpdateActions();}});actions.Controls.Add(restoreButton);Add(p,actions);detailSource.AutoSize=true;detailSource.Font=EditorTheme.Font(8);detailSource.ForeColor=EditorTheme.Muted;Add(p,detailSource);EditorTheme.Apply(p);return p;
        }
        Control TextField(string key,string label,bool multiline=false){var t=new TextBox{Multiline=multiline,Height=multiline?85:30,MaxLength=multiline?16384:1024,ScrollBars=multiline?ScrollBars.Vertical:ScrollBars.None,AcceptsReturn=multiline};fields[key]=t;t.TextChanged+=(s,e)=>Changed();return EditorTheme.Field(label,t);}
    }
    public sealed class ResourcePreview : Control
    {
        readonly Timer timer=new Timer{Interval=500};Image loaded;string resource="",root="",state="No image selected";int serial;
        public string Root{set{if(root!=value){root=value;Schedule();}}}
        public string Resource{set{if(resource!=value){resource=value;Schedule();}}}
        public ResourcePreview(){DoubleBuffered=true;BackColor=Color.FromArgb(20,26,29);timer.Tick+=async(s,e)=>{timer.Stop();await LoadResource();};}
        void Schedule(){serial++;timer.Stop();timer.Start();}
        async Task LoadResource(){int version=serial;string name=Regex.Replace(resource??"",@"^\{[^}]+\}","").Trim();state="Loading preview…";Invalidate();try{string path=await Task.Run(()=>{var r=new MapReply{BackgroundResource=name,AddonDirectory=""};if(!String.IsNullOrEmpty(root)&&!Path.IsPathRooted(name)&&!name.Split('/','\\').Contains(".."))r.BackgroundCandidates.Add(Path.Combine(root,name));return MapTexture.DecodeReply(r);});if(IsDisposed||version!=serial)return;using(var image=Image.FromFile(path)){if(loaded!=null)loaded.Dispose();loaded=new Bitmap(image);}state="";}catch{if(IsDisposed||version!=serial)return;if(loaded!=null){loaded.Dispose();loaded=null;}state="Preview unavailable\nResource remains selected";}Invalidate();}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(loaded!=null){float scale=Math.Min((float)Width/loaded.Width,(float)Height/loaded.Height);float w=loaded.Width*scale,h=loaded.Height*scale;e.Graphics.DrawImage(loaded,(Width-w)/2,(Height-h)/2,w,h);}else using(var font=EditorTheme.Font(9))TextRenderer.DrawText(e.Graphics,state,font,ClientRectangle,EditorTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);using(var pen=new Pen(EditorTheme.Line))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);}
        protected override void Dispose(bool disposing){if(disposing){serial++;timer.Dispose();if(loaded!=null)loaded.Dispose();}base.Dispose(disposing);}
    }
}

