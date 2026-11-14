using System;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed partial class AdvancedSettingsPage
    {
        readonly Label nativeLifecycle=EditorTheme.Label("",9,EditorTheme.Muted);
        readonly Label hydrationDefault=EditorTheme.Label("",9,EditorTheme.Muted);
        void BuildVanillaSettings()
        {
            var hud=Section("Notifications & HUD");
            tabs.Group(hud,"Friendly nametags","Names above friendly characters during play. Spectator names are configured separately.");
            Choice(hud,"FriendlyNametagMode","Friendly names",new[]{"Use mission default","Disabled","Custom distance"},"Mission default preserves the world's native nametag configuration. Custom distance uses vanilla friendly nametags and keeps the native visibility and player/AI filters.");
            Number(hud,"FriendlyNametagDistance","Maximum distance (m)","1–500 metres. The same limit applies with scopes and binoculars; distant markers are hidden too.",1,500);
            tabs.Group(hud,"Vanilla killfeed","Controls death notices, including those seen in Game Master.");
            Choice(hud,"Killfeed","Killfeed",new[]{"Disabled","Deaths without killer names","Full killfeed"},"Disabled also suppresses the native Game Master exception. Statistics continue to be collected.");
            Choice(hud,"KillfeedAudience","Receive deaths of",new[]{"All players","Enemies","Allies","Own faction","Own group"},"Native audience filter. With killfeed enabled, Game Master retains its native full-feed view.");
            Check(hud,"FriendlyFeed","Show friendly-fire deaths","Only when killfeed is enabled. Off hides these deaths even in Game Master.");
            Choice(hud,"FriendlyAudience","Friendly-fire disclosure",new[]{"Follow killfeed settings","Players involved","Friendly team"},"Native disclosure can reveal killer names. Requires friendly-fire deaths above.");
            tabs.Group(hud,"Server & membership notices","Independent of killfeed and the mission's own messages.");
            Check(hud,"ConnectionFeed","Join, leave, kick and ban notices","Other participants' connection notices, including a Game Master leaving. Your own connection errors remain available.");
            Check(hud,"MembershipFeed","Faction, group and leader-change notices","Routine membership notices only. Requests and action failures remain available.");
            tabs.Group(hud,"Mission messages & HUD");
            Note(hud,"Objective feedback, authored hints, System messages and administrator announcements stay enabled. Freeze, technical pause and battlefield warnings are unaffected by vanilla feed settings.");
            Note(hud,"Per-type restrictions for authored messages and personal nametag profiles are planned separately.");

            var permissions=Section("Vanilla systems & permissions");
            tabs.Group(permissions,"Game Master");
            Check(permissions,"GMBudgets","GM budgets","Enforce placement limits for props, AI, vehicles and systems. GM can change this for the current mission in Scenario properties > Game. Restart restores this initial setting.");
            tabs.Group(permissions,"Hydration");
            Choice(permissions,"Hydration","Hydration decay and stamina penalty",new[]{"Use mission default","Disabled","Enabled"},"Enabling uses Arma's native hydration rules. Applies when the mission starts.");
            EditorTheme.Add(permissions,hydrationDefault);
            tabs.Group(permissions,"Player access","The native components remain the source of their own settings.");
            Check(permissions,"ArmaVision","Allow Arma Vision for ordinary players","Stored in Mission Header. Does not remove Game Master or permitted spectator cameras.");
            Check(permissions,"GroupMenu","Allow vanilla group menu","The tvtLobby group list and role slots remain available regardless of this switch.");
            Check(permissions,"PlayerBuilding","Allow player free-roam building","Allows building rights granted by the world's native systems; does not create building zones. Administrator Game Master tools stay available.");
            tabs.Group(permissions,"Voting & discipline");
            Check(permissions,"Voting","Allow vanilla voting","Native vote types and limits still apply. Commander freeze readiness and direct administrator actions are separate.");
            Check(permissions,"TeamkillPunishment","Automatic teamkill punishment","Native crime penalties. Off preserves statistics and manual administrator moderation.");
            tabs.Group(permissions,"Player lifecycle","Managed by tvtLobby's slot and reconnect flow.");
            EditorTheme.Add(permissions,nativeLifecycle);
            Note(permissions,"The base prefab disables vanilla autospawn and independent faction changes. Disconnected bodies are retained for reconnect. Allowed tvtLobby slot changes still work. These fields are not silently overwritten by Apply.");
            Note(permissions,"Garbage cleanup, personal HUD settings and performance tuning remain separate work.");
            var lifecycle=Section("Mission lifecycle");
            tabs.Group(lifecycle,"After Debriefing","Default: everyone remains in the results lobby.");
            Choice(lifecycle,"AutomaticCycle","When debrief ends",new[]{"Remain in Debriefing","Request configured server handoff"},"Handoff requires the existing supervised-server setup. It does not create a rotation or load another mission by itself.");
            Note(lifecycle,"With Remain in Debriefing, there is no automatic countdown or cycle request. Spectating ends and players remain in the Debriefing tab. External server restarts are controlled outside this editor.");
        }
        void VanillaState()
        {
            if(!controls.ContainsKey("Killfeed"))return;
            controls["FriendlyNametagDistance"].Enabled=((ComboBox)controls["FriendlyNametagMode"]).SelectedIndex==2;
            bool feed=((ComboBox)controls["Killfeed"]).SelectedIndex>0;
            controls["KillfeedAudience"].Enabled=feed;controls["FriendlyFeed"].Enabled=feed;
            controls["FriendlyAudience"].Enabled=feed&&((CheckBox)controls["FriendlyFeed"]).Checked;
            nativeLifecycle.Text=current==null?"":("Vanilla autospawn: "+(current.NativeAutospawn==0?"Off":"On — review in World Editor")+"  ·  Vanilla faction change: "+(current.NativeFactionChange==0?"Off":"On — review in World Editor"));
            hydrationDefault.Text=current==null?"":("Mission default: "+(current.NativeHydration==0?"Disabled":"Enabled")+". New tvtLobby missions start with hydration disabled.");
        }
    }
}
