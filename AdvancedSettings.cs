using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class AdvancedSettings
    {
        public int AutomaticCycle {get;set;}
        public int GMBudgets {get;set;}
        public int Version {get;set;}
        public int VoiceMode {get;set;} public int PublicCommand {get;set;} public int CombatChat {get;set;}
        public int DeadVoice {get;set;} public int DeadText {get;set;} public int SharedAudience {get;set;}
        public int VoiceAudience {get;set;} public int TextAudience {get;set;}
        public int AllowGuests {get;set;} public int Targets {get;set;} public int FirstPerson {get;set;}
        public int Orbit {get;set;} public int FollowView {get;set;} public int FreeCamera {get;set;}
        public int GoPro {get;set;}
        public int Names {get;set;} public int Health {get;set;} public int Weapon {get;set;}
        public int BotAccess {get;set;} public int BotCombat {get;set;}
        public int SlotAI {get;set;} public int OtherAI {get;set;}
        public int VanillaLeaders {get;set;} public int MarkerLimit {get;set;} public int MarkerDeletion {get;set;}
        public int StartMarkers {get;set;} public int StartEmpty {get;set;} public int StartAudience {get;set;} public int StartAdminSame {get;set;}
        public int StartPosition {get;set;} public int StartCapture {get;set;} public int StartShow {get;set;} public int StartHide {get;set;}
        public int StartKeepIndex {get;set;} public int StartLabels {get;set;} public int StartWhite {get;set;} public int StartAll {get;set;}
        public decimal StartSize {get;set;} public List<string> StartGroups {get;set;}
        public int Killfeed {get;set;} public int KillfeedAudience {get;set;} public int FriendlyFeed {get;set;} public int FriendlyAudience {get;set;}
        public int ConnectionFeed {get;set;} public int MembershipFeed {get;set;} public int ArmaVision {get;set;} public int Voting {get;set;}
        public int PlayerBuilding {get;set;} public int GroupMenu {get;set;} public int TeamkillPunishment {get;set;} public int NativeAutospawn {get;set;} public int NativeFactionChange {get;set;}
        public int Hydration {get;set;} public int NativeHydration {get;set;}
        public int SpecHUD {get;set;} public int SpecCard {get;set;} public int SpecBlood {get;set;} public int SpecCondition {get;set;} public int SpecMap {get;set;} public int SpecTags {get;set;} public int SpecXray {get;set;} public int SpecNight {get;set;} public int SpecFooter {get;set;} public int SpecFeed {get;set;} public int SpecBlur {get;set;} public int SpecHideUI {get;set;}
        public int SpecMapPositions {get;set;} public int SpecMapGuests {get;set;} public int SpecMapSelectFrozen {get;set;}
        public int SpecVoiceRooms {get;set;} public int SpecVoiceRoomLimit {get;set;} public int SpecVoiceRoomCapacity {get;set;} public int SpecHints {get;set;} public int SpecStatsBasic {get;set;} public int SpecStatsKills {get;set;} public int SpecStatsKiller {get;set;} public int SpecStatsVictims {get;set;} public int SpecStatsWeapon {get;set;} public int SpecStatsDistance {get;set;} public int SpecStatsDelay {get;set;}
        public string Problem {get;set;}
        public AdvancedSettings(){SpecVoiceRooms=1;SpecVoiceRoomLimit=8;SpecVoiceRoomCapacity=16;SpecHints=3;SpecStatsBasic=1;SpecStatsKills=0;SpecStatsKiller=0;SpecStatsVictims=0;SpecStatsWeapon=0;SpecStatsDistance=0;SpecStatsDelay=120;SpecHUD=3;SpecCard=3;SpecBlood=0;SpecCondition=0;SpecMap=3;SpecTags=0;SpecXray=0;SpecNight=2;SpecFooter=3;SpecFeed=0;SpecBlur=2;SpecHideUI=1;GMBudgets=OtherAI=1;VoiceMode=DeadVoice=DeadText=SharedAudience=Targets=FirstPerson=GoPro=Names=BotAccess=1;Problem="";MarkerLimit=20;StartMarkers=StartEmpty=StartAudience=StartAdminSame=StartHide=StartKeepIndex=StartLabels=StartAll=1;StartCapture=StartShow=4;StartSize=48;KillfeedAudience=4;StartGroups=new List<string>();}
        public AdvancedSettings Clone(){var c=(AdvancedSettings)MemberwiseClone();c.StartGroups=new List<string>(StartGroups??new List<string>());return c;}
        public string Fingerprint(){var values=new SortedDictionary<string,object>(StringComparer.Ordinal);foreach(var p in GetType().GetProperties())values[p.Name]=p.GetValue(this,null);return new JavaScriptSerializer().Serialize(values);}
        public string Validate()
        {
            if(Version!=10)return "Restart Workbench and reopen Mission Editor to load Advanced settings.";
            if(Hydration<0||Hydration>2)return "Choose a supported hydration rule.";
            if(!String.IsNullOrEmpty(Problem))return Problem;
            if(VoiceMode<0||VoiceMode>1||VoiceAudience<0||VoiceAudience>1||TextAudience<0||TextAudience>1||Targets<0||Targets>3||BotAccess<0||BotAccess>2)return "Unsupported mode or audience.";
            if(new[]{AutomaticCycle,PublicCommand,CombatChat,DeadVoice,DeadText,SharedAudience,AllowGuests,FirstPerson,GoPro,Orbit,FollowView,FreeCamera,BotCombat,SlotAI,OtherAI,GMBudgets}.Any(x=>x<0||x>1))return "Invalid switch value.";
            if(Killfeed<0||Killfeed>2||KillfeedAudience<0||KillfeedAudience>4||FriendlyAudience<0||FriendlyAudience>2)return "Unsupported vanilla feed mode or audience.";
            if(new[]{FriendlyFeed,ConnectionFeed,MembershipFeed,ArmaVision,Voting,PlayerBuilding,GroupMenu,TeamkillPunishment}.Any(x=>x<0||x>1))return "Invalid vanilla settings switch.";
            if(new[]{SpecHUD,SpecCard,SpecBlood,SpecCondition,SpecMap,SpecTags,SpecXray,SpecNight,SpecFooter,SpecFeed,SpecBlur,SpecHideUI,SpecHints,SpecStatsBasic,SpecStatsKills,SpecStatsKiller,SpecStatsVictims,SpecStatsWeapon,SpecStatsDistance,Names,Health,Weapon}.Any(x=>x<0||x>3))return "Unsupported spectator display rule.";
            if(SpecMapPositions<0||SpecMapPositions>1||SpecMapGuests<0||SpecMapGuests>2||SpecMapSelectFrozen<0||SpecMapSelectFrozen>1)return "Invalid spectator map rule.";
            if(SpecVoiceRooms<0||SpecVoiceRooms>1||SpecVoiceRoomLimit<1||SpecVoiceRoomLimit>32||SpecVoiceRoomCapacity<2||SpecVoiceRoomCapacity>128||SpecStatsDelay<0||SpecStatsDelay>3600)return "Invalid spectator room limit or statistics delay.";
            if(FirstPerson+GoPro+Orbit+FollowView+FreeCamera==0)return "Allow at least one spectator camera.";
            if(MarkerLimit<1||MarkerLimit>500||MarkerDeletion<0||MarkerDeletion>3)return "Player marker limit: 1–500; choose a valid deletion policy.";
            if(new[]{VanillaLeaders,StartMarkers,StartEmpty,StartAdminSame,StartPosition,StartKeepIndex,StartLabels,StartWhite,StartAll}.Any(x=>x<0||x>1))return "Invalid map switch.";
            if(StartAudience<0||StartAudience>2||StartCapture<1||StartCapture>5||StartShow<1||StartShow>5||StartHide<0||StartHide>2||StartSize<24||StartSize>96)return "Invalid marker phases, audience or size (24–96).";
            if(StartMarkers!=0&&(StartCapture>StartShow||(StartShow==5&&StartHide==0)))return "Capture must precede or equal display; hiding must be later than display.";
            if(StartMarkers!=0&&StartAll==0&&StartGroups.Count==0)return "Choose at least one playable group, or include all.";
            if(StartGroups.Distinct().Count()!=StartGroups.Count)return "A start-marker group was selected twice.";
            return null;
        }
    }

    public sealed partial class AdvancedSettingsPage : UserControl
    {
        static readonly Color Dark=Color.FromArgb(22,28,31),Card=Color.FromArgb(30,38,42),Muted=Color.FromArgb(158,178,184),Gold=Color.FromArgb(224,175,86);
        readonly Dictionary<string,Control> controls=new Dictionary<string,Control>();
        readonly Func<AdvancedSettings,Task<FoundationReply>> send;
        readonly Label message=new EditorLabel(); readonly Button apply=new EditorButton{Primary=true},reload=new EditorButton();
        readonly SettingsDeck tabs=new SettingsDeck();
        AdvancedSettings current; string baseline="",world=""; bool loading,dirty,conflict;
        public event Action DraftChanged;
        public bool HasDraft {get{return dirty;}}
        public string DraftError{get{return conflict?"Advanced settings changed in Workbench. Reload fields first.":ReadFields().Validate();}}
        public AdvancedSettingsPage(Func<AdvancedSettings,Task<FoundationReply>> action)
        {
            send=action;Dock=DockStyle.Fill;BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;Font=EditorTheme.Font();
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};SizeChanged+=(s,e)=>{if(frame.RowStyles.Count>1)frame.RowStyles[1].Height=Height<500?64:86;message.MaximumSize=new Size(Math.Max(180,Width-275),0);};frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,86));
            tabs.Dock=DockStyle.Fill;frame.Controls.Add(tabs,0,0);
            var comm=Section("Communication");
            tabs.Group(comm,"Lobby & Command","Ordinary rooms and combat chat");
            Choice(comm,"VoiceMode","Lobby voice transport",new[]{"Spatial","Radio"},"Used for ordinary lobby and Command rooms. Live announcements keep their own route.");
            Check(comm,"PublicCommand","Allow members into Command outside Briefing","During Briefing, members of the faction can already join Command.");
            Check(comm,"CombatChat","Allow living players' combat chat","After freeze until Debriefing. Living players cannot receive dead players' messages.");
            tabs.Group(comm,"Dead players & spectators","Separate from living combat communications");
            Check(comm,"DeadVoice","Enable dead / spectator voice","");Check(comm,"DeadText","Enable dead / spectator text","");
            Check(comm,"SharedAudience","Use the same audience for text and voice","With this enabled, both use the voice audience below.");
            Choice(comm,"VoiceAudience","Voice audience",new[]{"All dead / admitted spectators","Own faction"});
            Choice(comm,"TextAudience","Text audience",new[]{"All dead / admitted spectators","Own faction"});
            Note(comm,"Faction mode keeps unaffiliated guests in a separate room. Guest admission follows Spectator settings. Admin/system messages remain available; ordinary chat and voice switches do not disable announcements.");
            Note(comm,"Unconscious characters cannot speak, including announcements. Dead players do not hear the living. These remain fixed rules in this version.");
            var spec=Section("Spectator");
            tabs.Group(spec,"Access & targets");
            Check(spec,"AllowGuests","Allow spectators who did not participate","Guests without a faction need All players target access. This does not assign them a faction.");
            Choice(spec,"Targets","Players available to watch",new[]{"All players","Own faction","Own group","Own group, then own faction"});
            tabs.Group(spec,"Cameras","At least one mode must remain available");
            Check(spec,"FirstPerson","First-person camera","");
            Check(spec,"GoPro","GoPro — helmet camera","");
            Check(spec,"Orbit","Third-person orbit","");Check(spec,"FollowView","Third-person follows target view","");
            Check(spec,"FreeCamera","Free camera","Free flight does not reveal forbidden players in the list or on the map. Flight distance is not limited yet.");
            tabs.Group(spec,"Information","Shown only for permitted targets");
            var displayRules=new[]{"Off","On","Player choice (off by default)","Player choice (on by default)"};
            Choice(spec,"Names","Names and callsigns",displayRules); Choice(spec,"Health","Target health",displayRules); Choice(spec,"Weapon","Weapon name",displayRules);
            Choice(spec,"SpecHUD","Observed weapon HUD",displayRules);
            Choice(spec,"SpecCard","Observed player card",displayRules);
            Choice(spec,"SpecBlood","Blood level",displayRules);
            Choice(spec,"SpecCondition","Bleeding and unconsciousness",displayRules);
            Choice(spec,"SpecMap","Spectator map",displayRules);
            Choice(spec,"SpecTags","Names above allowed targets",displayRules);
            Choice(spec,"SpecXray","Targets through obstacles",displayRules);
            Choice(spec,"SpecNight","Spectator night view",displayRules);
            Choice(spec,"SpecFooter","Bottom control hints",displayRules);
            Choice(spec,"SpecFeed","Spectator killfeed",displayRules);
            Choice(spec,"SpecBlur","Distant depth of field",displayRules);
            Choice(spec,"SpecHideUI","Allow hiding the interface",new[]{"Disabled","Allowed"});
            Choice(spec,"SpecMapPositions","Map positions",new[]{"Live","At own death"}); Choice(spec,"SpecMapGuests","Guests without a death snapshot",new[]{"No positions","Snapshot on admission","Live"}); Check(spec,"SpecMapSelectFrozen","Select live targets from frozen markers","This can reveal that a historical target is still available.");
            Choice(spec,"SpecHints","Game hints",displayRules);
            tabs.Group(spec,"Personal death statistics","Each group of details has its own release rule");
            var timing=new[]{"Never","Immediately","After delay","At debriefing"};
            Choice(spec,"SpecStatsBasic","Survival time, deaths and role",timing);
            Choice(spec,"SpecStatsKills","Kill and team-kill counts",timing);
            Choice(spec,"SpecStatsKiller","Who killed the viewer",timing);
            Choice(spec,"SpecStatsVictims","Whom the viewer killed",timing);
            Choice(spec,"SpecStatsWeapon","Weapon details",timing);
            Choice(spec,"SpecStatsDistance","Distance at death",timing);
            Number(spec,"SpecStatsDelay","Disclosure delay","Mission seconds after the event; technical pauses stop this clock.",0,3600);
            Note(spec,"Unknown damage sources stay unknown. Statistics belong to the viewer, never to the current camera target.");
            tabs.Group(spec,"Dead voice rooms");
            Check(spec,"SpecVoiceRooms","Allow custom voice rooms","Only within the configured dead-player voice audience. Text chat keeps its separate audience.");
            Number(spec,"SpecVoiceRoomLimit","Rooms per audience","Empty custom rooms disappear automatically.",1,32);
            Number(spec,"SpecVoiceRoomCapacity","Members per custom room","One owned room per participant. Ownership passes to a remaining member when the owner leaves.",2,128);
            var presets=new FlowLayoutPanel{AutoSize=true};
            var full=new EditorButton{Text="Full information",AutoSize=true};
            var anti=new EditorButton{Text="Anti-meta",AutoSize=true};
            full.Click+=(s,e)=>SpectatorPreset(false);anti.Click+=(s,e)=>SpectatorPreset(true);
            presets.Controls.Add(full);presets.Controls.Add(anti);
            int presetRow=spec.RowCount++;spec.RowStyles.Add(new RowStyle(SizeType.AutoSize));spec.Controls.Add(presets,0,presetRow);spec.SetColumnSpan(presets,spec.ColumnCount);
            Note(spec,"These are the same spectator settings available under GameMode in World Editor. Administrators retain their authorized camera access. Debriefing stops observation and returns everyone to the lobby.");
            BuildMapSettings();
            var gear=Section("Equipment & AI");
            tabs.Group(gear,"AI behavior");
            Check(gear,"SlotAI","Enable AI for unoccupied lobby slots","After freeze only, for bodies retained by Mission settings. Disconnected players' bodies remain reserved without AI.");
            Check(gear,"OtherAI","Enable AI for other bots / Game Master","Non-playable groups and characters spawned during the mission. Uses their native AI behavior.");
            Note(gear,"AI pauses during a technical pause and debriefing. Removing unoccupied playable characters is a separate Mission setting.");
            tabs.Group(gear,"Group equipment exchange");
            Choice(gear,"BotAccess","Exchange equipment with living unoccupied group members",new[]{"Disabled","Group commander","All group members"});
            Check(gear,"BotCombat","Allow exchange after freeze too","Includes disconnected participants' bodies while nobody controls them. Ordinary corpse looting stays separate.");
            Note(gear,"Use the vanilla inventory to take, return and swap items nearby. Keeping unoccupied bots is configured separately in Mission settings.");
            BuildVanillaSettings();
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0)};
            Style(apply,"Apply settings");Style(reload,"Reload fields");footer.Controls.Add(apply);footer.Controls.Add(reload);
            message.AutoSize=true;message.MaximumSize=new Size(700,0);message.UseMnemonic=false;message.ForeColor=Muted;footer.Controls.Add(message);frame.Controls.Add(footer,0,1);Controls.Add(frame);
            apply.Click+=async(s,e)=>{var draft=ReadFields();string error=draft.Validate();if(error!=null){message.Text=error;return;}var result=await send(draft);if(result.Ok!=1)message.Text=result.Message;};
            reload.Click+=(s,e)=>LoadFields(current);
        }
        void SpectatorPreset(bool anti)
        {
            string[] displays={"SpecHUD","SpecCard","SpecBlood","SpecCondition","SpecMap","SpecTags","SpecXray","SpecNight","SpecFooter","SpecFeed","SpecBlur","Names","Health","Weapon","SpecHints"};
            foreach(string key in displays)((ComboBox)controls[key]).SelectedIndex=3;
            ((ComboBox)controls["SpecHideUI"]).SelectedIndex=1;((ComboBox)controls["SpecNight"]).SelectedIndex=2;((ComboBox)controls["SpecBlur"]).SelectedIndex=2;
            ((ComboBox)controls["Targets"]).SelectedIndex=anti?1:0;
            ((ComboBox)controls["SpecMapPositions"]).SelectedIndex=anti?1:0;
            ((ComboBox)controls["SpecMapGuests"]).SelectedIndex=anti?0:2;
            ((CheckBox)controls["SpecMapSelectFrozen"]).Checked=false;
            ((ComboBox)controls["VoiceAudience"]).SelectedIndex=anti?1:0;
            foreach(string key in new[]{"SpecStatsBasic","SpecStatsKills","SpecStatsKiller","SpecStatsVictims","SpecStatsWeapon","SpecStatsDistance"})((ComboBox)controls[key]).SelectedIndex=anti?3:1;
            if(anti){foreach(string key in new[]{"SpecBlood","SpecCondition","SpecTags","SpecXray","SpecFeed","Health"})((ComboBox)controls[key]).SelectedIndex=0;((ComboBox)controls["SpecStatsBasic"]).SelectedIndex=1;}
            Changed();
        }
        static void Style(Button b,string text){b.Text=text;b.AutoSize=true;b.MinimumSize=new Size(110,34);b.Margin=new Padding(0,0,10,5);}
        TableLayoutPanel Section(string title){return tabs.AddSection(title,title=="Mission lifecycle"?"Debriefing and supervised server handoff":title=="Notifications & HUD"?"Death feed, connections and membership":title=="Vanilla systems & permissions"?"Hydration, player access, voting and discipline":title=="Map & markers"?"Native markers and static group starts":title=="Communication"?"Voice, text and spectator audiences":title=="Spectator"?"Access, cameras and permitted information":"AI behavior and group equipment exchange");}
        void Row(TableLayoutPanel p,string title,string help,Control input){string key=controls.First(x=>x.Value==input).Key;tabs.Row(p,key,title,help,input,(key=="StartShow"||key=="StartCapture")?(object)3:typeof(AdvancedSettings).GetProperty(key).GetValue(new AdvancedSettings(),null));}
        void Note(TableLayoutPanel p,string text){tabs.Note(p,text);}
        void Check(TableLayoutPanel p,string key,string title,string help){var c=new EditorToggle{Compact=true};controls[key]=c;c.CheckedChanged+=(s,e)=>Changed();Row(p,title,help,c);}
        void Choice(TableLayoutPanel p,string key,string title,string[] entries,string help=""){var c=new EditorCombo{Width=260};c.Items.AddRange(entries);controls[key]=c;c.SelectedIndexChanged+=(s,e)=>Changed();Row(p,title,help,c);}
        public void Accept(FoundationReply reply,bool replace){mapForces=reply.Forces;var next=reply.Advanced;bool changed=world!=reply.WorldSession;world=reply.WorldSession;if(!replace&&!changed&&dirty&&next!=null&&baseline!=next.Fingerprint())conflict=true;current=next;if(replace||changed||!dirty)LoadFields(next);else {RebuildMapGroups();State();}}
        void LoadFields(AdvancedSettings value){loading=true;var d=value??new AdvancedSettings();foreach(var pair in controls){decimal number=Convert.ToDecimal(typeof(AdvancedSettings).GetProperty(pair.Key).GetValue(d,null));int v=(int)number;var check=pair.Value as CheckBox;if(check!=null)check.Checked=v!=0;else if(pair.Value is NumericUpDown){var n=(NumericUpDown)pair.Value;n.Value=Math.Max(n.Minimum,Math.Min(n.Maximum,number));}else{var combo=(ComboBox)pair.Value;int offset=pair.Key=="StartShow"||pair.Key=="StartCapture"?1:0;combo.SelectedIndex=Math.Max(0,Math.Min(combo.Items.Count-1,v-offset));}}selectedMapGroups=new List<string>(d.StartGroups??new List<string>());RebuildMapGroups();baseline=d.Fingerprint();dirty=conflict=false;loading=false;State();if(DraftChanged!=null)DraftChanged();}
        public AdvancedSettings ReadFields(){var d=(current??new AdvancedSettings()).Clone();foreach(var pair in controls){var c=pair.Value as CheckBox;var n=pair.Value as NumericUpDown;int offset=pair.Key=="StartShow"||pair.Key=="StartCapture"?1:0;decimal value=c!=null?(c.Checked?1:0):n!=null?n.Value:((ComboBox)pair.Value).SelectedIndex+offset;var property=typeof(AdvancedSettings).GetProperty(pair.Key);property.SetValue(d,Convert.ChangeType(value,property.PropertyType),null);}d.StartGroups=new List<string>(selectedMapGroups);return d;}
        void Changed(){if(loading)return;dirty=ReadFields().Fingerprint()!=baseline;State();if(DraftChanged!=null)DraftChanged();}
        void State(){MapState();VanillaState();tabs.UpdateValues((((ComboBox)controls["VoiceMode"]).SelectedIndex==1?"Radio":"Spatial")+" lobby voice · "+((NumericUpDown)controls["MarkerLimit"]).Value+" player markers · static group starts");bool available=current!=null&&current.Version==10;tabs.Enabled=available;bool writable=available&&String.IsNullOrEmpty(current.Problem);apply.Enabled=writable&&dirty&&!conflict;reload.Enabled=available&&dirty;controls["TextAudience"].Enabled=!((CheckBox)controls["SharedAudience"]).Checked&&((CheckBox)controls["DeadText"]).Checked;controls["VoiceAudience"].Enabled=((CheckBox)controls["DeadVoice"]).Checked||(((CheckBox)controls["SharedAudience"]).Checked&&((CheckBox)controls["DeadText"]).Checked);controls["BotCombat"].Enabled=((ComboBox)controls["BotAccess"]).SelectedIndex>0;message.Text=!available?"Restart Workbench and reopen the editor to load Advanced settings.":!writable?current.Problem:conflict?"Workbench changed. Draft kept; Reload fields before applying.":dirty?"Unapplied settings. Apply, then Save mission.":"Native Undo is available before Save. Save may reset scene-field history.";}
        public void Applied(){dirty=conflict=false;}
        public bool CanLeave(){return !dirty||MessageBox.Show(this,"Discard unapplied Advanced settings?","Unapplied settings",MessageBoxButtons.OKCancel)==DialogResult.OK;}
    }
}
