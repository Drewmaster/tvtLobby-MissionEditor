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
    public sealed partial class EditorWindow : Form
    {
        private static readonly Color Background=Color.FromArgb(22,28,31),PanelColor=Color.FromArgb(30,38,42),Muted=Color.FromArgb(158,178,184),Gold=Color.FromArgb(224,175,86);
        private readonly Session session;
        private readonly FoundationClient client;
        private readonly Panel content=new Panel();
        private readonly FlowLayoutPanel headerActions=new FlowLayoutPanel();
        private readonly Label heading=new EditorLabel(),world=new EditorLabel(),status=new EditorLabel(),state=new EditorLabel();
        private readonly FlowLayoutPanel navigation=new FlowLayoutPanel(),toolbar=new FlowLayoutPanel();
        private readonly Dictionary<string,Control> pages=new Dictionary<string,Control>();
        private readonly Dictionary<string,TextBox> fields=new Dictionary<string,TextBox>();
        private readonly DataGridView files=new DataGridView();
        private readonly ListBox issues=new ListBox();
        private readonly NumericUpDown maximum=new NumericUpDown();
        private readonly CheckBox autoMax=new EditorToggle();
        private readonly Label slotCount=new EditorLabel(),detailSource=new EditorLabel();
        private readonly Button create,apply,save,restore;
        private FoundationReply view;
        private string baseline="",page="Mission guide";
        private bool busy,loading,dirty,conflict,saving;
        private bool connectionFailed,saveFailed;
        private MapWindow map;
        private GroupsWindow groups;
        private readonly FactionCardsPage factionsPage; private readonly ForcesPage groupsPage;
        private readonly MissionSettingsPage settingsPage;
        private readonly AdvancedSettingsPage advancedPage;
        private readonly FreezeZonesPage freezeZonesPage;
        private readonly MissionEndPage endPage;
        private readonly ObjectivesPage objectivesPage;
        private readonly BriefingPage briefingPage;

        public EditorWindow(Session value,bool connectOnShow=true)
        {
            session=value;client=new FoundationClient(session);
            Text="tvtLobby | Mission Editor";Size=new Size(1440,1000);MinimumSize=new Size(980,680);
            AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterScreen;Font=EditorTheme.Font();BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Margin=Padding.Empty};
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220));frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute,83));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
            var brand=EditorTheme.Stack(20);brand.BackColor=EditorTheme.Panel;EditorTheme.Add(brand,EditorTheme.Label("tvtLobby",17,null,true));EditorTheme.Add(brand,EditorTheme.Label("M I S S I O N   E D I T O R",7,EditorTheme.Muted));frame.Controls.Add(brand,0,0);
            var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,Padding=new Padding(8,14,24,14),BackColor=EditorTheme.Panel,Margin=Padding.Empty};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var mission=EditorTheme.Stack();worldTitle=EditorTheme.Label("Connecting to Workbench…",13,null,true);world.Font=EditorTheme.Font(8);world.ForeColor=EditorTheme.Muted;world.AutoSize=true;world.UseMnemonic=false;world.MaximumSize=new Size(670,30);EditorTheme.Add(mission,worldTitle);EditorTheme.Add(mission,world);header.Controls.Add(mission);
            state.AutoSize=true;state.Font=EditorTheme.Font(8);state.ForeColor=EditorTheme.Green;state.Margin=new Padding(6,12,16,0);header.Controls.Add(state);
            headerActions.AutoSize=true;headerActions.WrapContents=false;headerActions.Controls.Add(Button("Files & setup",()=>ShowPage("Files & setup")));save=Button("Save mission",async()=>await SaveMission(),true);headerActions.Controls.Add(save);header.Controls.Add(headerActions);frame.Controls.Add(header,1,0);
            navigation.Dock=DockStyle.Fill;navigation.FlowDirection=FlowDirection.TopDown;navigation.WrapContents=false;navigation.AutoScroll=true;navigation.Padding=new Padding(12,20,12,0);navigation.BackColor=Color.FromArgb(27,32,36);
            AddNavCaption("START");AddNav("Mission guide");AddNavCaption("MISSION SETUP");AddNav("Files & setup");AddNav("Mission details");AddNav("Factions & alliances");AddNav("Mission settings");AddNav("Advanced settings");AddNavCaption("SCENARIO");AddNav("Groups & vehicles");AddNav("Map, markers & zones");AddNav("Objectives");AddNav("Mission end");AddNav("Briefing");AddNavCaption("REVIEW");AddNav("Validation");frame.Controls.Add(navigation,0,1);
            var main=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};main.RowStyles.Add(new RowStyle(SizeType.Absolute,55));main.RowStyles.Add(new RowStyle(SizeType.Absolute,98));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var toolsRow=new Panel{Dock=DockStyle.Fill};breadcrumb=EditorTheme.Label("MISSION GUIDE",8,EditorTheme.Muted);breadcrumb.Location=new Point(24,20);toolsRow.Controls.Add(breadcrumb);toolbar.Dock=DockStyle.Right;toolbar.AutoSize=true;toolbar.WrapContents=false;toolbar.Padding=new Padding(0,10,18,0);toolbar.Controls.Add(Button("Undo",async()=>await Execute("undo")));toolbar.Controls.Add(Button("Redo",async()=>await Execute("redo")));toolbar.Controls.Add(Button("Refresh",async()=>await Execute("snapshot")));toolbar.Controls.Add(Button("Validate",async()=>{await Execute("validate");ShowPage("Validation");}));toolsRow.Controls.Add(toolbar);
            SizeChanged+=(s,e)=>{main.RowStyles[0].Height=55;main.RowStyles[1].Height=Height<820?76:98;};var headingBox=EditorTheme.Stack();headingBox.Padding=new Padding(24,22,24,0);heading.Font=EditorTheme.Font(18,FontStyle.Bold);heading.AutoSize=true;heading.UseMnemonic=false;EditorTheme.Add(headingBox,heading);subtitle=EditorTheme.Label("",9,EditorTheme.Muted);EditorTheme.Add(headingBox,subtitle);
            SizeChanged+=(s,e)=>headingBox.Padding=new Padding(24,Height<820?6:22,24,0);content.Dock=DockStyle.Fill;content.Padding=new Padding(24,0,24,14);main.Controls.Add(toolsRow,0,0);main.Controls.Add(headingBox,0,1);main.Controls.Add(content,0,2);frame.Controls.Add(main,1,1);
            status.Dock=DockStyle.Fill;status.UseMnemonic=false;status.Font=EditorTheme.Font(8);status.Padding=new Padding(20,9,20,0);status.ForeColor=EditorTheme.Muted;status.BackColor=EditorTheme.Panel;status.Text="Start with the guide, or open any section directly.";frame.Controls.Add(status,0,2);frame.SetColumnSpan(status,2);Controls.Add(frame);
            pages["Mission guide"]=BuildGuide();
            pages["Files & setup"]=BuildFiles(out create);
            pages["Mission details"]=BuildDetails(out apply,out restore);
            settingsPage=new MissionSettingsPage(ExecuteSettings);settingsPage.DraftChanged+=UpdateActions;
            pages["Mission settings"]=settingsPage;
            advancedPage=new AdvancedSettingsPage(ExecuteAdvanced);advancedPage.DraftChanged+=UpdateActions;
            pages["Advanced settings"]=advancedPage;
            freezeZonesPage=new FreezeZonesPage(ExecuteFreezeZones,OpenMap,()=>settingsPage.SelectSection("Freeze & readiness"));freezeZonesPage.DraftChanged+=UpdateActions;settingsPage.AddFreezeZones(freezeZonesPage);
            endPage=new MissionEndPage(ExecuteEnd);endPage.DraftChanged+=UpdateActions;pages["Mission end"]=endPage;
            objectivesPage=new ObjectivesPage(ExecuteObjective,()=>ShowPage("Map, markers & zones"));objectivesPage.DraftChanged+=UpdateActions;pages["Objectives"]=objectivesPage;
            briefingPage=new BriefingPage(ExecuteBriefing);briefingPage.DraftChanged+=UpdateActions;pages["Briefing"]=briefingPage;
            factionsPage=new FactionCardsPage(ExecuteForces);groupsPage=new ForcesPage(true,ExecuteForces);
            factionsPage.DraftChanged+=UpdateActions;groupsPage.DraftChanged+=UpdateActions;
            pages["Factions & alliances"]=factionsPage;pages["Groups & vehicles"]=groupsPage;
            pages["Validation"]=BuildValidation();
            navigation.Layout+=(s,e)=>{int width=Math.Min(194,navigation.ClientSize.Width-navigation.Padding.Horizontal-(navigation.VerticalScroll.Visible?SystemInformation.VerticalScrollBarWidth:0));foreach(Control c in navigation.Controls)if(c.Width!=width)c.Width=width;}; EditorTheme.Apply(this);ShowPage("Mission guide");UpdateActions();
            if(connectOnShow)
            {
                Shown+=async(s,e)=>{await Execute("snapshot");};
                // Returning from World Editor is not an implicit world scan.
                // Refresh is explicit; native revision/value guards protect Apply.
            }
            FormClosing+=(s,e)=>{SaveDraft();if(busy||saving||!factionsPage.CanLeave()||!groupsPage.CanLeave()||!settingsPage.CanLeave()||!freezeZonesPage.CanLeave()||!advancedPage.CanLeave()||!endPage.CanLeave()||!objectivesPage.CanLeave()||!briefingPage.CanLeave())e.Cancel=true;};
        }
        private static Label Label(string text,float size=10,Color? color=null)
        {return new EditorLabel{Text=text,UseMnemonic=false,Font=new Font("Segoe UI",size),ForeColor=color??Color.Gainsboro,Dock=DockStyle.Fill,AutoSize=true,MaximumSize=new Size(850,0),Margin=new Padding(0,0,0,12)};}
        private Button Button(string text,Action action,bool primary=false){return EditorTheme.Button(text,action,primary);}
        private TableLayoutPanel Page()
        {
            var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,AutoScroll=true,Padding=new Padding(0,8,12,8)};
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            return panel;
        }
        private void Add(TableLayoutPanel parent,Control control)
        {
            int row=parent.RowCount++;parent.RowStyles.Add(new RowStyle(SizeType.AutoSize));parent.Controls.Add(control,0,row);
            control.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        }
        private void AddNavCaption(string text){navigation.Controls.Add(new EditorLabel{Text=text,Width=195,Height=26,Padding=new Padding(7,8,0,0),ForeColor=Muted,Font=new Font(Font.FontFamily,8,FontStyle.Bold)});}
        private void AddNav(string title)
        {var button=Button(title,()=>ShowPage(title));((EditorButton)button).Navigation=true;button.Tag=title;button.Width=194;button.AutoSize=false;button.Height=35;button.TextAlign=ContentAlignment.MiddleLeft;button.Margin=new Padding(0,0,0,3);button.FlatAppearance.BorderSize=0;navigation.Controls.Add(button);}
        public void ShowPage(string name)
        {
            page=name;heading.Text=name;content.Controls.Clear();
            if(!pages.ContainsKey(name))pages[name]=BuildHandoff(name);
            content.Controls.Add(pages[name]);EditorTheme.Apply(pages[name]);
            foreach(Control c in navigation.Controls)if(c is EditorButton){((EditorButton)c).Selected=(string)c.Tag==name;c.Invalidate();} breadcrumb.Text=name.ToUpperInvariant();subtitle.Text=PageDescription(name);
            UpdateActions();if(name=="Map, markers & zones"&&view!=null&&!busy&&!connectionFailed)OpenMap();
        }
        private Control BuildHandoff(string name)
        {
            var p=Page();
            if(name=="Map, markers & zones")
            {Add(p,Label("Connected map editor",15));Add(p,Label("Create and edit icons, lines, areas, objective zones, freeze areas and battlefield boundaries on the current terrain. Changes use native Undo and explicit Save.",11));Add(p,Button("Open map editor",OpenMap,true));Add(p,Button("General freeze settings →",()=>ShowPage("Mission settings")));}
            else if(name=="Groups & vehicles")
            {Add(p,Label("Connected group list",15));Add(p,Label("Reads the current world's groups, callsigns and playable flags. Select a group in World Editor or change its playable state with native Undo.",11));Add(p,Button("Open group list",()=>{if(groups==null||groups.IsDisposed){groups=new GroupsWindow(session);groups.Show(this);}else groups.Activate();},true));Add(p,Label("Group ordering, vehicle assignment and faction tabs will be connected in their own implementation stage.",10,Muted));}
            else {Add(p,Label("Next implementation stage",15));Add(p,Label("The workflow is reserved here. Its full editor is not connected yet; current mission components and the existing tvtLobby Workbench plugins remain available.",11));Add(p,Label("Changes made manually in World Editor are retained. This section does not create or reset mission data.",10,Muted));}
            return p;
        }
        private void PickImage(string key)
        {
            using(var dialog=new OpenFileDialog{Filter="Enfusion image (*.edds)|*.edds",Title="Choose a registered image"})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                string meta=dialog.FileName+".meta";
                if(!File.Exists(meta)){status.Text="This file has no .meta. Import/register it in Resource Browser first, then copy its resource name here.";return;}
                Match match=Regex.Match(File.ReadAllText(meta),"Name\\s+\"([^\"]+)\"");
                if(match.Success)fields[key].Text=match.Groups[1].Value;else status.Text="No resource name found in the image metadata.";
            }
        }
        private MissionDetails ReadFields()
        {return new MissionDetails{Title=fields["Title"].Text,Author=fields["Author"].Text,Description=fields["Description"].Text.Replace("\r\n","\n"),Icon=fields["Icon"].Text,Preview=fields["Preview"].Text,Loading=fields["Loading"].Text,MaxPlayers=(int)maximum.Value,AutoMax=autoMax.Checked?1:0};}
        private void LoadFields(MissionDetails d)
        {
            loading=true;fields["Title"].Text=d.Title;fields["Author"].Text=d.Author;fields["Description"].Text=(d.Description??"").Replace("\r\n","\n").Replace("\n","\r\n");fields["Icon"].Text=d.Icon;fields["Preview"].Text=d.Preview;fields["Loading"].Text=d.Loading;
            maximum.Value=Math.Max(1,Math.Min(4096,d.MaxPlayers));autoMax.Checked=d.AutoMax!=0;maximum.Enabled=!autoMax.Checked;
            baseline=d.Serialize();dirty=false;conflict=false;loading=false;UpdateActions();
        }
        private void Changed(){if(loading)return;dirty=true;SaveDraft();UpdateActions();}
        private void SaveDraft(){if(!dirty||view==null)return;try{MissionDrafts.Save(view.WorldPath,ReadFields());}catch(Exception e){status.Text="Draft could not be saved locally: "+e.Message;}}
        private void UpdateActions()
        {
            bool connected=view!=null&&!busy&&!saving&&!connectionFailed;
            content.Enabled=navigation.Enabled=headerActions.Enabled=connected;
            if(map!=null&&!map.IsDisposed)map.Enabled=connected;
            if(groups!=null&&!groups.IsDisposed)groups.Enabled=connected;
            bool forceDraft=(factionsPage!=null&&factionsPage.HasDraft)||(groupsPage!=null&&groupsPage.HasDraft)||(settingsPage!=null&&settingsPage.HasDraft)||(advancedPage!=null&&advancedPage.HasDraft);
            forceDraft=forceDraft||(freezeZonesPage!=null&&freezeZonesPage.HasDraft)||(endPage!=null&&endPage.HasDraft)||(objectivesPage!=null&&objectivesPage.HasDraft)||(briefingPage!=null&&briefingPage.HasDraft);
            save.Enabled=connected&&!conflict;save.Text=dirty||forceDraft?"Apply & save mission":"Save mission";create.Enabled=connected&&view.CanCreate==1;apply.Enabled=connected&&view.Ready==1&&dirty&&!conflict;
            restore.Enabled=connected&&File.Exists(MissionDrafts.PathFor(view.WorldPath));
            foreach(Control c in toolbar.Controls)if(c is Button)c.Enabled=!busy&&!saving&&(c.Text=="Refresh"||connected&&!dirty&&!forceDraft);
            state.Text=busy?"Working…":conflict?"Workbench changed · draft kept":dirty||forceDraft?"Unsaved changes · Apply & save":view!=null&&view.Pending!=0?"Applied · save required":"Connected";
            if(view==null&&!busy)state.Text="Not connected";
            if(connectionFailed&&!busy)state.Text="Not connected · Refresh to retry";
            else if(saveFailed&&!busy&&!saving)state.Text="Save failed · changes kept";
            else if(view!=null&&view.SaveRecoveryPending&&!busy&&!saving)state.Text="Save interrupted · retry Save mission";
            if(factionsPage!=null)factionsPage.Enabled=connected&&view.Ready==1;
            if(groupsPage!=null)groupsPage.Enabled=connected&&view.Ready==1;
            if(freezeZonesPage!=null)freezeZonesPage.Enabled=connected&&view.Ready==1;
            if(settingsPage!=null)settingsPage.Enabled=connected&&view.Ready==1;
            if(advancedPage!=null)advancedPage.Enabled=connected&&view.Ready==1;
            if(endPage!=null)endPage.Enabled=connected&&view.Ready==1;
            if(briefingPage!=null)briefingPage.Enabled=connected&&view.Ready==1;
            if(objectivesPage!=null)objectivesPage.Enabled=connected&&view.Ready==1;
        }
        public void AcceptSnapshot(FoundationReply reply,bool replaceFields)
        {
            connectionFailed=false;
            bool same=view!=null&&view.WorldPath==reply.WorldPath;
            if(!same)SaveDraft();
            view=reply;files.DataSource=reply.Items;RefreshPresentation(reply);
            worldTitle.Text=String.IsNullOrWhiteSpace(reply.Details.Title)?"Untitled mission":reply.Details.Title;world.Text=reply.WorldPath;
            detailSource.Text="Native scenario header\n"+(String.IsNullOrEmpty(reply.HeaderPath)?"Create the foundation first.":reply.HeaderPath);
            slotCount.Text=reply.Slots+" available slots across "+reply.PlayableGroups+" playable groups. Auto maximum updates on Apply / Save mission.";
            issues.Items.Clear();foreach(string item in reply.Issues??new List<string>())issues.Items.Add(item);if(issues.Items.Count==0)issues.Items.Add("Foundation checks passed.");
            if(replaceFields||!dirty||!same)LoadFields(reply.Details);
            else if(reply.Details.Serialize()!=baseline)conflict=true;
            factionsPage.Accept(reply,replaceFields||!same);groupsPage.Accept(reply,replaceFields||!same);
            settingsPage.Accept(reply,replaceFields||!same);
            advancedPage.Accept(reply,replaceFields||!same);
            freezeZonesPage.Accept(reply,replaceFields||!same);
            endPage.Accept(reply,replaceFields||!same);
            objectivesPage.Accept(reply,replaceFields||!same);
            briefingPage.Accept(reply,replaceFields||!same);
            UpdateActions();
        }
        private async Task<FoundationReply> ExecuteForces(string operation,ForcesEdit edit)
        {
            if(busy||view==null)return new FoundationReply{Message="Wait for the current operation, then Refresh."};
            busy=true;UpdateActions();
            try
            {
                FoundationReply result=await Task.Run(()=>client.Execute(operation,view,null,edit));
                if(IsDisposed)return result;
                if(result.Ok!=1){status.Text=result.Message+" ["+result.Code+"]";return result;}
                if(operation.StartsWith("faction_"))factionsPage.Applied(edit.Key);else groupsPage.Applied(operation);
                AcceptSnapshot(result,false);status.Text=result.Message;
                return result;
            }
            catch(Exception e){if(!IsDisposed)status.Text=e.Message;return new FoundationReply{Message=e.Message};}
            finally{busy=false;if(!IsDisposed)UpdateActions();}
        }
        private async Task<FoundationReply> ExecuteSettings(MissionSettings draft)
        {
            if(busy||view==null)return new FoundationReply{Message="Wait for the current operation, then Refresh."};
            busy=true;UpdateActions();
            try
            {
                var result=await Task.Run(()=>client.Execute("settings_apply",view,null,null,draft));
                if(IsDisposed)return result;
                if(result.Ok==1){settingsPage.Applied();AcceptSnapshot(result,false);}
                status.Text=result.Message;return result;
            }
            catch(Exception e){if(!IsDisposed)status.Text=e.Message;return new FoundationReply{Message=e.Message};}
            finally{busy=false;if(!IsDisposed)UpdateActions();}
        }
        void OpenMap(){if(map==null||map.IsDisposed){map=new MapWindow(session);map.FormClosed+=async(s,e)=>{if(!IsDisposed&&!busy)await Execute("snapshot");};map.Show(this);}else map.Activate();}
        private async Task<FoundationReply> ExecuteFreezeZones(FreezeZoneSettings draft)
        {
            if(busy||view==null)return new FoundationReply{Message="Wait for the current operation."};
            busy=true;UpdateActions();
            try {var result=await Task.Run(()=>client.Execute("freezezones_apply",view,null,freezeZones:draft));if(IsDisposed)return result;if(result.Ok==1){freezeZonesPage.Applied();AcceptSnapshot(result,false);}status.Text=result.Message;return result;}
            catch(Exception e){return new FoundationReply{Message=e.Message};}finally{busy=false;if(!IsDisposed)UpdateActions();}
        }
        private async Task<FoundationReply> ExecuteAdvanced(AdvancedSettings draft)
        {
            if(busy||view==null)return new FoundationReply{Message="Wait for the current operation, then Refresh."};
            busy=true;UpdateActions();
            try
            {
                var result=await Task.Run(()=>client.Execute("advanced_apply",view,null,null,null,draft));
                if(IsDisposed)return result;
                if(result.Ok==1){advancedPage.Applied();AcceptSnapshot(result,false);}
                status.Text=result.Message;return result;
            }
            catch(Exception e){if(!IsDisposed)status.Text=e.Message;return new FoundationReply{Message=e.Message};}
            finally{busy=false;if(!IsDisposed)UpdateActions();}
        }
        private async Task<FoundationReply> ExecuteEnd(MissionEndData draft)
        {
            if(busy||view==null)return new FoundationReply{Message="Wait for the current operation, then Refresh."};
            busy=true;UpdateActions();
            try
            {
                var result=await Task.Run(()=>client.Execute("end_apply",view,null,null,null,null,draft));
                if(IsDisposed)return result;
                if(result.Ok==1){endPage.Applied();AcceptSnapshot(result,false);}
                status.Text=result.Message;return result;
            }
            catch(Exception e){if(!IsDisposed)status.Text=e.Message;return new FoundationReply{Message=e.Message};}
            finally{busy=false;if(!IsDisposed)UpdateActions();}
        }
        private async Task<FoundationReply> ExecuteObjective(string operation,ObjectiveEdit draft)
        {
            if(busy||view==null)return new FoundationReply{Message="Wait for the current operation."};
            busy=true;UpdateActions();
            try{
                var result=await Task.Run(()=>client.Execute(operation,view,null,null,null,null,null,draft));
                if(IsDisposed)return result;
                if(result.Ok==1){objectivesPage.Applied();AcceptSnapshot(result,false);}
                status.Text=result.Message;return result;
            }catch(Exception e){return new FoundationReply{Message=e.Message};}
            finally{busy=false;if(!IsDisposed)UpdateActions();}
        }
        private async Task<FoundationReply> ExecuteBriefing(string operation,BriefingEdit draft)
        {
            if(busy||view==null)return new FoundationReply{Message="Wait for the current operation."};
            busy=true;UpdateActions();
            try{
                var result=await Task.Run(()=>client.Execute(operation,view,null,null,null,null,null,null,draft));
                if(IsDisposed)return result;
                if(result.Ok==1){briefingPage.Applied();AcceptSnapshot(result,false);}
                status.Text=result.Message;return result;
            }catch(Exception e){return new FoundationReply{Message=e.Message};}
            finally{busy=false;if(!IsDisposed)UpdateActions();}
        }
        private async Task<bool> Execute(string operation)
        {
            if(busy)return false;
            if(operation!="snapshot"&&view==null)return false;
            MissionDetails draft=operation=="apply"?ReadFields():null;
            if(draft!=null){string error=draft.Validate(view.Slots);if(error!=null){status.Text=error;return false;}}
            busy=true;UpdateActions();
            if(view==null){worldTitle.Text="Connecting to Workbench…";status.Text="Reading the open world. Large terrains can take up to 60 seconds.";}
            try
            {
                FoundationReply result=await Task.Run(()=>{
                    if(operation=="snapshot") WorkbenchClient.VerifyCompatibility(session);
                    return client.Execute(operation,view,draft);
                });
                if(IsDisposed)return false;
                if(result.Ok!=1){status.Text=result.Message+" ["+result.Code+"]";if(view==null||result.Code=="INCOMPATIBLE_EDITOR")ConnectionError(status.Text);return false;}
                bool replace=operation=="undo"||operation=="redo";
                if(operation=="create"&&view!=null&&view.Ready==0){baseline=result.Details.Serialize();conflict=false;}
                if(operation=="apply"){MissionDrafts.Remove(result.WorldPath);dirty=false;conflict=false;}
                AcceptSnapshot(result,replace);
                status.Text=!String.IsNullOrEmpty(result.Message)?result.Message:"Refreshed from Workbench. Local drafts are kept until Apply.";
                return true;
            }
            catch(Exception e){if(!IsDisposed)ConnectionError(e.Message);return false;}
            finally{busy=false;if(!IsDisposed)UpdateActions();}
        }
        sealed class PendingEdit{public string Page;public Func<string> Error;public Func<Task<bool>> Apply;}
        List<PendingEdit> PendingEdits()
        {
            var pending=new List<PendingEdit>();
            if(dirty)pending.Add(new PendingEdit{Page="Mission details",Error=()=>conflict?"Metadata changed in Workbench. Reload fields first.":ReadFields().Validate(view.Slots),Apply=async()=>{await Execute("apply");return !dirty;}});
            if(factionsPage.HasDraft)pending.Add(new PendingEdit{Page="Factions & alliances",Error=()=>factionsPage.DraftError,Apply=factionsPage.ApplyPending});
            if(groupsPage.HasDraft)pending.Add(new PendingEdit{Page="Groups & vehicles",Error=()=>groupsPage.DraftError,Apply=groupsPage.ApplyPending});
            if(settingsPage.HasDraft)pending.Add(new PendingEdit{Page="Mission settings",Error=()=>settingsPage.DraftError,Apply=async()=> (await ExecuteSettings(settingsPage.ReadFields())).Ok==1});
            if(advancedPage.HasDraft)pending.Add(new PendingEdit{Page="Advanced settings",Error=()=>advancedPage.DraftError,Apply=async()=> (await ExecuteAdvanced(advancedPage.ReadFields())).Ok==1});
            if(freezeZonesPage.HasDraft)pending.Add(new PendingEdit{Page="Mission settings",Error=()=>freezeZonesPage.DraftError,Apply=async()=> (await ExecuteFreezeZones(freezeZonesPage.ReadFields())).Ok==1});
            if(endPage.HasDraft)pending.Add(new PendingEdit{Page="Mission end",Error=()=>endPage.DraftError,Apply=async()=> (await ExecuteEnd(endPage.ReadFields())).Ok==1});
            if(objectivesPage.HasDraft)pending.Add(new PendingEdit{Page="Objectives",Error=()=>objectivesPage.DraftError,Apply=objectivesPage.ApplyPending});
            if(briefingPage.HasDraft)pending.Add(new PendingEdit{Page="Briefing",Error=()=>briefingPage.DraftError,Apply=briefingPage.ApplyPending});
            return pending;
        }
        private async Task SaveMission()
        {
            if(busy||saving||view==null||connectionFailed)return;
            saveFailed=false;
            var pending=PendingEdits();
            // Validate every draft before the first write, then recheck as dependencies change.
            foreach(var edit in pending){string error=edit.Error();if(error!=null){saveFailed=true;ShowPage(edit.Page);status.Text="Mission was not saved. "+edit.Page+": "+error;return;}}
            saving=true;UpdateActions();
            try{
                foreach(var edit in pending){string error=edit.Error();if(error!=null||!await edit.Apply()){saveFailed=true;ShowPage(edit.Page);status.Text="Save stopped at "+edit.Page+". "+(error??status.Text)+" Other drafts are retained; already applied changes have not been saved yet.";return;}}
                if(!await Execute("save")){saveFailed=true;status.Text="Mission was not saved. "+status.Text;}
            }catch(Exception e){saveFailed=true;status.Text="Save stopped: "+e.Message+". Unapplied drafts are retained.";}
            finally{saving=false;if(!IsDisposed)UpdateActions();}
        }
        private void ConnectionError(string message)
        {
            connectionFailed=true;status.Text=message;
            if(view==null){worldTitle.Text="Workbench connection failed";world.Text="Check the message below, then click Refresh to reconnect.";}
        }
    }
}

