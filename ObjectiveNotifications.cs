using System;
using System.Linq;
using System.Windows.Forms;
namespace TvtLobby.MissionEditor
{
    public sealed partial class ObjectiveForm
    {
        TaskNode selectedNotice;
        bool noticeAdvanced;
        static string OptionLabel(string field,int value,string fallback="")
        {
            string[] labels=null;
            switch(field)
            {
                case "Event":labels=new[]{"Objective activated","Progress changed","Objective completed","Objective failed","Objective skipped","Zone / flag captured","Control lost","Zone contested","Hold started","Hold interrupted","Death confirmed","Waypoint reached","Time remaining","Loss threshold reached","Mission ended","Entered zone","Left zone","Return deadline expired"};break;
                case "Audience":labels=new[]{"Sides participating in this objective","Selected factions, sides or groups","Everyone","Administrators only","Player who triggered the event","That player's group","That player's faction","New owner","Previous owner"};break;
                case "Display":labels=new[]{"Hint on the right","Central system message","Chat only","Centered text"};break;
                case "Life":labels=new[]{"Living and dead players","Living players only","Dead players only"};break;
                case "Slots":labels=new[]{"Everyone, with or without a slot","Players with a slot","Players without a slot"};break;
                case "Roles":labels=new[]{"All roles","Side and group commanders","Side commanders only","Group commanders only"};break;
                case "Lifetime":labels=new[]{"Disappear after a duration","Keep while the objective is active","Pinned until dismissed"};break;
                case "Repeat":labels=new[]{"Each matching event","Only once","When the threshold is crossed"};break;
                case "Sound":labels=new[]{"Automatic","Silent","Vanilla notification sound"};break;
                case "Icon":labels=new[]{"No icon","Information","Warning"};break;
                case "Condition":labels=new[]{
                    "No opposing participants present (no defenders in the zone)",
                    "Required numerical ratio (attackers at least N times defenders)",
                    "Numerical superiority (at least N more attackers than defenders)",
                    "Custom conditions (combine participant count rules: all / any)",
                    "Custom expression (enter your own formula with attackers and defenders)"
                };break;
				case "Amount":labels=new[]{"All selected targets","Any one target","A specific number","A percentage of targets"};break;
				case "HoldMode":labels=new[]{"Continuous — reset when control is lost","Accumulated — keep time when control is lost"};break;
            }
            if(labels!=null&&value>=0&&value<labels.Length)return labels[value];
            string result=ObjectivesData.Words(fallback).ToLowerInvariant();return result.Length==0?value.ToString():Char.ToUpperInvariant(result[0])+result.Substring(1);
        }
        TaskField NoticeRules(TaskField field,bool create=false)
        {
            if(field==null)return null;
            if(create&&field.Nodes.Count==0)field.Nodes.Add(data.Make("tvtLobby_Notifications"));
            return field.Nodes.Count==0?null:field.Nodes[0].Field("Rules");
        }
        void AddNotice(TaskField field,int ev)
        {
            var rules=NoticeRules(field,true);if(rules==null)return;
            var existing=rules.Nodes.FirstOrDefault(n=>n.Text("Event")==ev.ToString());if(existing!=null){selectedNotice=existing;return;}
            var rule=data.Make("tvtLobby_NotificationRule");if(rule==null)return;
            rule.Set("Enabled","1");rule.Set("Name",OptionLabel("Event",ev));rule.Set("Event",ev.ToString());rule.Set("Display","0");
            rule.Set("Text",ev==1?"{objective}: {progress} / {required}":"{objective}");rules.Nodes.Add(rule);selectedNotice=rule;
        }
        void NotificationSetup(TableLayoutPanel parent,TaskField field)
        {
            if(field==null)return;var p=Card("Player notifications");var rules=NoticeRules(field);
            EditorTheme.Add(p,EditorTheme.Label(rules==null||rules.Nodes.Count==0?"No messages. The objective still works normally.":rules.Nodes.Count+" message(s) prepared. You can edit each message after creating the objective.",10,EditorTheme.Muted));
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,WrapContents=true};
            actions.Controls.Add(EditorTheme.Button("Progress + result hints",()=>{Preset(field);Touch(true);}));
            actions.Controls.Add(EditorTheme.Button("Result hint only",()=>{AddNotice(field,2);Touch(true);}));
            actions.Controls.Add(EditorTheme.Button("No messages",()=>{var list=NoticeRules(field);if(list!=null)list.Nodes.Clear();Touch(true);}));EditorTheme.Add(p,actions);
            EditorTheme.Add(p,EditorTheme.Label("Defaults: participating sides, hints on the right. Use the Notifications tab afterwards to change recipients, text and presentation.",9,EditorTheme.Muted));EditorTheme.Add(parent,p);
        }
        void Notifications(TableLayoutPanel parent,TaskField field)
        {
            if(field==null){EditorTheme.Add(parent,EditorTheme.Label("This selection does not send notifications."));return;}
            var rules=NoticeRules(field);
            if(rules==null||rules.Nodes.Count==0){NotificationSetup(parent,field);return;}
            if(selectedNotice==null||!rules.Nodes.Contains(selectedNotice))selectedNotice=rules.Nodes[0];
            EditorTheme.Add(parent,EditorTheme.Label("Messages sent by this objective",11,null,true));
			if(edit.Definition.TypeName=="tvtLobby_RaiseFlagModule")EditorTheme.Add(parent,EditorTheme.Label("Flag captured / Control lost: one pole changes owner. Objective completed: the required flags and hold duration are satisfied. Progress counts owned flags; {timeLeft} is the hold time remaining at the event.",9,EditorTheme.Muted));
            foreach(var node in rules.Nodes)
            {
                var n=node;int ev,recipient,display;Int32.TryParse(n.Text("Event"),out ev);Int32.TryParse(n.Text("Audience"),out recipient);Int32.TryParse(n.Text("Display"),out display);
                var b=new EditorButton{Text=OptionLabel("Event",ev)+(n.Text("Enabled")=="0"?" · disabled":""),Detail=OptionLabel("Audience",recipient)+"  ·  "+OptionLabel("Display",display),Navigation=true,Selected=n==selectedNotice,LayoutHeight=68};
                b.Click+=(s,e)=>{selectedNotice=n;noticeAdvanced=false;Render();};EditorTheme.Add(parent,b);
            }
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,WrapContents=true};
            actions.Controls.Add(EditorTheme.Button("+ Add message",()=>{var n=data.Make("tvtLobby_NotificationRule");n.Set("Enabled","1");n.Set("Name","Message "+(rules.Nodes.Count+1));n.Set("Text","{objective}");rules.Nodes.Add(n);selectedNotice=n;noticeAdvanced=false;Touch(true);}));
            actions.Controls.Add(EditorTheme.Button("Remove selected message",()=>{rules.Nodes.Remove(selectedNotice);selectedNotice=null;Touch(true);}));EditorTheme.Add(parent,actions);
            var card=Card("Message settings");var r=selectedNotice;
            foreach(string name in new[]{"Enabled","Event","Audience"})Field(card,r,r.Field(name));
            if(r.Text("Audience")=="1")foreach(string name in new[]{"Subjects","Groups"})Field(card,r,r.Field(name));
            Field(card,r,r.Field("Display"));Field(card,r,r.Field("Title"));Field(card,r,r.Field("Text"));
            if(r.Text("Display")!="2")
            {
                Field(card,r,r.Field("Lifetime"));if(r.Text("Lifetime")=="0")Field(card,r,r.Field("Seconds"));Field(card,r,r.Field("Sound"));
            }
            EditorTheme.Add(card,EditorTheme.Button(noticeAdvanced?"▾ Hide additional filters and options":"▸ Additional filters and options",()=>{noticeAdvanced=!noticeAdvanced;Render();}));
            if(noticeAdvanced)
            {
                EditorTheme.Add(card,EditorTheme.Label("Further restrict the audience chosen above",10,null,true));
                foreach(string name in new[]{"Life","Slots","Roles","Repeat","MinimumInterval"})Field(card,r,r.Field(name));
                if(r.Text("Repeat")=="2")Field(card,r,r.Field("Threshold"));
                foreach(string name in new[]{"LateJoin","ChatHistory","Icon","Name"})Field(card,r,r.Field(name));
                foreach(var f in field.Nodes[0].Fields.Where(f=>f.Name!="Rules"))Field(card,field.Nodes[0],f);
            }
            EditorTheme.Add(parent,card);
        }
    }
}
