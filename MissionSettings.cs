using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed class MissionSettings
    {
        public int Version {get;set;}
        public int EndDisplay{get;set;} public int EndSound{get;set;} public int EndChat{get;set;}
        public decimal EndDelay{get;set;} public decimal EndSeconds{get;set;} public string EndDrawText{get;set;}
        public int RemoveBodies {get;set;} public int RemoveVehicles {get;set;}
        public int Readiness {get;set;} public int SquadPercent {get;set;}
        public decimal FreezeSeconds {get;set;} public decimal CountdownSeconds {get;set;}
        public int VehicleMovement {get;set;} public int CharacterDamage {get;set;}
        public int VehicleDamage {get;set;} public int PauseMedical {get;set;} public int Fire {get;set;}
        public int Environment {get;set;} public int EnvironmentAvailable {get;set;}
        public int Hour {get;set;} public int Minute {get;set;} public int OverrideDate {get;set;}
        public int Day {get;set;} public int Month {get;set;} public int Year {get;set;}
        public int WeatherChanges {get;set;} public decimal DaySpeed {get;set;} public decimal NightSpeed {get;set;}
        public string Weather {get;set;} public string EnvironmentProblem {get;set;}
        public List<string> WeatherOptions {get;set;}
        public MissionSettings(){EndDisplay=EndSound=EndChat=1;EndDelay=EndSeconds=10;EndDrawText="Mission ended in a draw.";RemoveBodies=1;SquadPercent=75;FreezeSeconds=300;CountdownSeconds=5;CharacterDamage=VehicleDamage=1;Hour=8;Day=Month=1;Year=1989;DaySpeed=NightSpeed=1;Weather="";WeatherOptions=new List<string>();}
        public MissionSettings Clone(){return (MissionSettings)MemberwiseClone();}
        public string Fingerprint()
        {
            // Reflection's cached property order can change after form binding.
            // Compare canonical values, including decimal scale, rather than JSON order.
            var values=new SortedDictionary<string,object>(StringComparer.Ordinal);
            foreach(var prop in typeof(MissionSettings).GetProperties())
            {object value=prop.GetValue(this,null);if(value is decimal)value=((decimal)value).ToString("G29",System.Globalization.CultureInfo.InvariantCulture);values[prop.Name]=value;}
            return new JavaScriptSerializer().Serialize(values);
        }
        public string Validate()
        {
            if(Version!=2)return "Restart Workbench and reopen Mission Editor to load the settings bridge.";
            if(FreezeSeconds<0||FreezeSeconds>86400||CountdownSeconds<1||CountdownSeconds>300)return "Freeze must be 0–86400 seconds; final countdown 1–300 seconds.";
            if(EndDisplay<0||EndDisplay>3||EndDelay<0||EndDelay>3600||EndSeconds<=0||EndSeconds>3600||(EndDrawText??"").Length>600)return "Check mission ending display, delays and message length (maximum 600).";
            if(Readiness<0||Readiness>2||SquadPercent<1||SquadPercent>100)return "Choose a readiness mode and a percentage from 1 to 100.";
            if(new[]{RemoveBodies,RemoveVehicles,VehicleMovement,CharacterDamage,VehicleDamage,PauseMedical,Fire,Environment,OverrideDate,WeatherChanges,EndSound,EndChat}.Any(x=>x<0||x>1))return "Invalid switch value.";
            if(Environment!=0)
            {
                if(EnvironmentAvailable==0)return EnvironmentProblem??"No time/weather manager in this world.";
                if(!String.IsNullOrEmpty(Weather)&&!(WeatherOptions??new List<string>()).Contains(Weather))return "Select a weather preset available on this terrain.";
                if(Hour<0||Hour>23||Minute<0||Minute>59||DaySpeed<0.1m||DaySpeed>12||NightSpeed<0.1m||NightSpeed>12)return "Invalid time or acceleration (0.1–12x).";
                if(OverrideDate!=0){try{if(Year<1899||Year>2050)throw new ArgumentOutOfRangeException();new DateTime(Year,Month,Day);}catch(ArgumentOutOfRangeException){return "Enter a valid calendar date between 1899 and 2050.";}}
            }
            return null;
        }
    }

    public sealed class MissionSettingsPage : UserControl
    {
        static readonly Color Dark=Color.FromArgb(22,28,31),CardColor=Color.FromArgb(30,38,42),Muted=Color.FromArgb(158,178,184),Gold=Color.FromArgb(224,175,86);
        readonly Dictionary<string,Control> controls=new Dictionary<string,Control>();
        readonly Func<MissionSettings,Task<FoundationReply>> send;
        readonly Label message=new EditorLabel(); readonly Button apply=new EditorButton{Primary=true},discard=new EditorButton();
        readonly SettingsDeck tabs=new SettingsDeck();
        readonly CheckBox commandersReady=new EditorToggle{Compact=true};
        MissionSettings current; string baseline="",world=""; bool loading,dirty,conflict;
        Action updateFooter;
        public event Action DraftChanged;
        public bool HasDraft {get{return dirty;}}
        public string DraftError{get{return conflict?"Mission settings changed in Workbench. Reload fields first.":ReadFields().Validate();}}
        public MissionSettingsPage(Func<MissionSettings,Task<FoundationReply>> action)
        {
            send=action;Dock=DockStyle.Fill;BackColor=EditorTheme.Background;ForeColor=EditorTheme.Text;Font=EditorTheme.Font();
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};SizeChanged+=(s,e)=>{if(frame.RowStyles.Count>1)frame.RowStyles[1].Height=Height<500?64:86;message.MaximumSize=new Size(Math.Max(180,Width-275),0);};frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,86));
            tabs.Dock=DockStyle.Fill;frame.Controls.Add(tabs,0,0);
            var people=Section("Participants & vehicles");
            tabs.Group(people,"After freeze","Cleanup and shared transport");
            Check(people,"RemoveBodies","Remove unoccupied playable characters","After freeze ends. A disconnected participant's body is retained for reconnect.");
            Check(people,"RemoveVehicles","Remove vehicles of empty groups","At the start of freeze; only if every linked group is empty. Occupied vehicles and objective targets are retained.");
            Note(people,"Keeping bots and allowing equipment exchange are separate rules. An allowance to loot bots does not keep their bodies automatically.");
            var freeze=Section("Freeze & readiness");
            tabs.Group(freeze,"Timing & readiness","Preparation duration and the transition to combat");
            Number(freeze,"FreezeSeconds","Freeze duration (minutes)",0,1440,3,"Combat begins after freeze ends. Zero goes directly to combat.");
            commandersReady.Checked=true;commandersReady.CheckedChanged+=(s,e)=>Changed();
            tabs.Row(freeze,"CommandersReady","Commanders can end freeze","Off: timer and administrator only.",commandersReady,1);
            Choice(freeze,"Readiness","Readiness rule",new[]{"All side commanders","Squad commander percentage"});
            Number(freeze,"SquadPercent","Required squad commanders (%)",1,100);
            Number(freeze,"CountdownSeconds","Final countdown (seconds)",1,300,0,"Commander readiness never extends the remaining freeze. Admin force-end uses the full delay.");
            tabs.Group(freeze,"Weapons & vehicles","One rule covers all damaging weapon actions");
            Check(freeze,"VehicleMovement","Allow vehicle movement","");
            Check(freeze,"Fire","Allow weapon fire & damaging throws","One rule for firing and grenades. Aiming, reloading and switching weapons remain available.");
            tabs.Group(freeze,"Damage & medical effects");
            Check(freeze,"CharacterDamage","Allow character damage","");Check(freeze,"VehicleDamage","Allow vehicle damage","");
            Check(freeze,"PauseMedical","Pause bleeding & harmful effects","");
            Note(freeze,"Character movement, treatment and engine operation remain available. Choose automatic circles or custom areas in Freeze zones. Draw manual zones in Map, markers & zones.");
            EditorTheme.Add(freeze,EditorTheme.Button("Configure freeze zones →",()=>SelectSection("Freeze zones")));
            Note(freeze,"Use Undo before Save for GameMode fields. Workbench may not restore earlier scene-field edits after saving; new edits can still be undone.");
            var ending=Section("Mission ending");
            tabs.Group(ending,"Result announcement","Shared by objective victories, critical losses and the combat time limit");
            Note(ending,"Victory condition → 5 seconds → result announcement → delay below → Debriefing. The result text is authored on the winning objective or end rule and stays visible on Debriefing, including when the announcement is disabled.");
            Choice(ending,"EndDisplay","Announcement display",new[]{"Side hint","System message","Centered text","None"});
            Number(ending,"EndDelay","Delay after announcement (seconds)",0,3600,1,"Time before opening Debriefing. With None, this delay starts 5 seconds after the result is decided.");
            Number(ending,"EndSeconds","Announcement duration (seconds)",0.1m,3600,1);
            Check(ending,"EndSound","Play announcement sound","");
            Check(ending,"EndChat","Copy announcement to chat","");
            var drawText=new TextBox{Multiline=true,Height=80,Width=340,MaxLength=600};controls["EndDrawText"]=drawText;drawText.TextChanged+=(s,e)=>Changed();Row(ending,"Message when results tie","Used when simultaneous winning conditions resolve to a draw.",drawText);
            var env=Section("Environment");
            tabs.Group(env,"Time & weather","Use the terrain defaults or override them for this mission");
            Check(env,"Environment","Override this world's environment","Off: use the full terrain's settings. On: apply the values below at mission start, in both Play and scenario launch.");
            Number(env,"Hour","Starting hour",0,23);Number(env,"Minute","Starting minute",0,59);
            Number(env,"DaySpeed","Daytime speed (×)",0.1m,12,1);Number(env,"NightSpeed","Nighttime speed (×)",0.1m,12,1);
            Check(env,"OverrideDate","Override date","");Number(env,"Year","Year",1899,2050);Number(env,"Month","Month",1,12);Number(env,"Day","Day",1,31);
            Choice(env,"Weather","Starting weather",new[]{"Keep terrain's starting weather"});
            Check(env,"WeatherChanges","Allow weather changes","Uses this terrain's native weather transitions.");
            Note(env,"The preset list comes from the open map, including modded terrains. Existing wind/fog overrides are retained; another time/weather handler must be resolved before enabling this override.");
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(0,10,0,0)};
            Style(apply,"Apply settings");Style(discard,"Reload fields");footer.Controls.Add(apply);footer.Controls.Add(discard);
            message.AutoSize=true;message.MaximumSize=new Size(720,0);message.ForeColor=Muted;message.UseMnemonic=false;footer.Controls.Add(message);frame.Controls.Add(footer,0,1);Controls.Add(frame);
            updateFooter=()=>{bool areas=tabs.SelectedSection=="Freeze zones";footer.Visible=!areas;frame.RowStyles[1].Height=areas?0:Height<500?64:86;};
            tabs.SelectionChanged+=updateFooter;SizeChanged+=(s,e)=>updateFooter();
            apply.Click+=async(s,e)=>{var draft=ReadFields();string error=draft.Validate();if(error!=null){message.Text=error;return;}var result=await send(draft);if(result.Ok!=1)message.Text=result.Message;};
            discard.Click+=(s,e)=>LoadFields(current);
        }
        public void AddFreezeZones(FreezeZonesPage page)
        {
            page.Embed();tabs.AddPage("Freeze zones","Automatic circles or custom areas",page,page.RestoreDefaults,()=>page.ModifiedCount,2);
            page.DraftChanged+=State;updateFooter();
        }
        public void SelectSection(string name){tabs.SelectSection(name);}
        static void Style(Button b,string text){b.Text=text;b.AutoSize=true;b.MinimumSize=new Size(110,34);b.Margin=new Padding(0,0,10,5);}
        TableLayoutPanel Section(string title)
        {
            return tabs.AddSection(title,title=="Participants & vehicles"?"Unoccupied characters and shared transport":title=="Freeze & readiness"?"Timing, actions, damage and medical effects":title=="Mission ending"?"Announcement style and delay before Debriefing":"Date, time, weather and day speed");
        }
        void Row(TableLayoutPanel parent,string title,string help,Control input)
        {
            string key=controls.First(p=>p.Value==input).Key;object value=typeof(MissionSettings).GetProperty(key).GetValue(new MissionSettings(),null);if(key=="FreezeSeconds")value=(decimal)value/60;tabs.Row(parent,key,title,help,input,value);
        }
        void Note(TableLayoutPanel p,string text){tabs.Note(p,text);}
        void Check(TableLayoutPanel p,string key,string title,string help){var c=new EditorToggle{Compact=true};controls[key]=c;c.CheckedChanged+=(s,e)=>Changed();Row(p,title,help,c);}
        void Number(TableLayoutPanel p,string key,string title,decimal min,decimal max,int decimals=0,string help=""){var n=new NumericUpDown{Minimum=min,Maximum=max,DecimalPlaces=decimals,Increment=decimals==0?1:0.1m,Width=125,BackColor=CardColor,ForeColor=Color.Gainsboro};controls[key]=n;n.ValueChanged+=(s,e)=>Changed();Row(p,title,help,n);}
        void Choice(TableLayoutPanel p,string key,string title,string[] entries){var c=new EditorCombo{Width=260};c.Items.AddRange(entries);controls[key]=c;c.SelectedIndexChanged+=(s,e)=>Changed();Row(p,title,"",c);}
        public void Accept(FoundationReply reply,bool replace)
        {
            var next=reply.Settings;bool changed=world!=reply.WorldSession;world=reply.WorldSession;
            if(!replace&&!changed&&dirty&&next!=null&&baseline!=next.Fingerprint())conflict=true;
            current=next;if(replace||changed||!dirty)LoadFields(next);else State();
        }
        void LoadFields(MissionSettings value)
        {
            loading=true;var d=value??new MissionSettings();commandersReady.Checked=d.Readiness!=2;
            foreach(var pair in controls)
            {
                object v=typeof(MissionSettings).GetProperty(pair.Key).GetValue(d,null);
                if(pair.Key=="FreezeSeconds")v=(decimal)v/60;
                if(pair.Key=="Readiness"&&d.Readiness==2)v=0;
                var n=pair.Value as NumericUpDown;var check=pair.Value as CheckBox;var combo=pair.Value as ComboBox;
                if(n!=null)n.Value=Math.Max(n.Minimum,Math.Min(n.Maximum,Convert.ToDecimal(v)));
                else if(check!=null)check.Checked=Convert.ToInt32(v)!=0;
                else if(pair.Value is TextBox)pair.Value.Text=(string)v??"";
                else if(pair.Key=="Weather"){combo.Items.Clear();combo.Items.Add("Keep terrain's starting weather");foreach(string name in d.WeatherOptions??new List<string>())combo.Items.Add(name);if(!String.IsNullOrEmpty(d.Weather)&&!combo.Items.Contains(d.Weather))combo.Items.Add(d.Weather);combo.SelectedIndex=String.IsNullOrEmpty(d.Weather)?0:combo.Items.IndexOf(d.Weather);}
                else combo.SelectedIndex=Math.Max(0,Math.Min(combo.Items.Count-1,Convert.ToInt32(v)));
            }
            baseline=d.Fingerprint();dirty=conflict=false;loading=false;State();if(DraftChanged!=null)DraftChanged();
        }
        public MissionSettings ReadFields()
        {
            var d=(current??new MissionSettings()).Clone();foreach(var pair in controls)
            {
                var prop=typeof(MissionSettings).GetProperty(pair.Key);var n=pair.Value as NumericUpDown;var c=pair.Value as CheckBox;var combo=pair.Value as ComboBox;
                object value=pair.Value is TextBox?(object)pair.Value.Text:n!=null?(object)n.Value:c!=null?(c.Checked?1:0):pair.Key=="Weather"?(combo.SelectedIndex>0?combo.Text:""):(object)combo.SelectedIndex;
                if(pair.Key=="FreezeSeconds")value=Decimal.Round((decimal)value*60,3);
                prop.SetValue(d,Convert.ChangeType(value,prop.PropertyType),null);
            }if(!commandersReady.Checked)d.Readiness=2;return d;
        }
        void Changed(){if(loading)return;dirty=ReadFields().Fingerprint()!=baseline;State();if(DraftChanged!=null)DraftChanged();}
        void State()
        {
            bool available=current!=null&&current.Version==2;tabs.Enabled=available;apply.Enabled=available&&dirty&&!conflict;discard.Enabled=available&&dirty;
            foreach(string key in new[]{"EndSeconds","EndSound","EndChat"})controls[key].Enabled=available&&((ComboBox)controls["EndDisplay"]).SelectedIndex!=3;
            controls["Readiness"].Enabled=commandersReady.Checked;
            controls["SquadPercent"].Enabled=commandersReady.Checked&&((ComboBox)controls["Readiness"]).SelectedIndex==1;
            bool env=((CheckBox)controls["Environment"]).Checked;controls["Environment"].Enabled=available&&(current.EnvironmentAvailable!=0||env);
            foreach(string key in new[]{"Hour","Minute","DaySpeed","NightSpeed","OverrideDate","Weather","WeatherChanges"})controls[key].Enabled=env&&available&&current.EnvironmentAvailable!=0;
            foreach(string key in new[]{"Day","Month","Year"})controls[key].Enabled=env&&((CheckBox)controls["OverrideDate"]).Checked&&available&&current.EnvironmentAvailable!=0;
            message.Text=!available?"Restart Workbench and reopen the editor to load mission settings.":conflict?"Workbench changed. Your draft is kept; Reload fields before applying.":dirty?"Unapplied settings. Apply, then Save mission.":String.IsNullOrEmpty(current.EnvironmentProblem)?"Values read from the current mission. Use native Undo / Redo before Save.":current.EnvironmentProblem;
            tabs.UpdateValues(((NumericUpDown)controls["FreezeSeconds"]).Value.ToString("0.###")+" min freeze · "+((NumericUpDown)controls["CountdownSeconds"]).Value.ToString("0.##")+" s final countdown · "+(((CheckBox)controls["RemoveBodies"]).Checked?"remove":"keep")+" empty playable bodies");
        }
        public void Applied(){dirty=conflict=false;}
        public bool CanLeave(){return !dirty||MessageBox.Show(this,"Discard unapplied mission settings?","Unapplied settings",MessageBoxButtons.OKCancel)==DialogResult.OK;}
    }
}
