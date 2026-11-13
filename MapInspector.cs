using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    public sealed partial class MapAppearanceEditor
    {
        MapRow mapRow; MapReply scene; MapAssets assets;
        public event Action<FreezeZoneSettings> FreezeChanged;
        public event Action<FreezeZoneSettings> FreezeDraftChanged;
        bool addingFreezeFields;
        void Notify(){if(PropertyValueChanged!=null)PropertyValueChanged(this,EventArgs.Empty);}
        void Heading(string title){EditorTheme.Add(rows,EditorTheme.Label(title,11,EditorTheme.Amber,true));}
        void Note(string text){EditorTheme.Add(rows,EditorTheme.Label(text,9,EditorTheme.Muted));}
        void TextField(string title,string value,Action<string> write){var c=new TextBox{Text=value??""};c.TextChanged+=(s,e)=>{write(c.Text);Notify();};EditorTheme.Add(rows,EditorTheme.Field(title,c));}
        void NumberField(string title,double value,double min,double max,Action<double> write,int decimals=1){var c=new NumericUpDown{Minimum=(decimal)min,Maximum=(decimal)max,DecimalPlaces=decimals,Value=(decimal)Math.Max(min,Math.Min(max,value)),Width=120};c.ValueChanged+=(s,e)=>{write((double)c.Value);Notify();};EditorTheme.Add(rows,EditorTheme.Field(title,c));}
        void Toggle(string title,bool value,Action<bool> write){bool freeze=addingFreezeFields;var c=new EditorToggle{Text=title,Checked=value,AutoSize=true};c.CheckedChanged+=(s,e)=>{write(c.Checked);if(!freeze)Notify();};EditorTheme.Add(rows,c);}
        void Choice(string title,string[] values,int value,Action<int> write){bool freeze=addingFreezeFields;var c=new EditorCombo();c.Items.AddRange(values);c.SelectedIndex=Math.Max(0,Math.Min(values.Length-1,value));c.SelectedIndexChanged+=(s,e)=>{write(c.SelectedIndex);if(!freeze)Notify();};EditorTheme.Add(rows,EditorTheme.Field(title,c));}
        void StageFreeze(FreezeZoneSettings settings){if(FreezeDraftChanged!=null)FreezeDraftChanged(settings);}
        static string GroupReference(EditorGroup group){return !String.IsNullOrEmpty(group.Id)?group.Id:!String.IsNullOrEmpty(group.Name)&&group.Name!="(unnamed group)"?group.Name:"@"+group.Key;}
        void ColorField(string title,string value,Action<string> write){var c=new MapColorField(value);c.Changed+=(s,e)=>{write(c.Value);Notify();};EditorTheme.Add(rows,EditorTheme.Field(title,c));}
        MapStyle Style(){if(mapRow.Style==null)mapRow.Style=new MapStyle{PixelSize=56*mapRow.Scale,Tint=scene!=null&&mapRow.ColorIndex>=0&&mapRow.ColorIndex<scene.Colors.Count?scene.Colors[mapRow.ColorIndex]:"1 1 1 1"};return mapRow.Style;}
        public void ShowRow(MapRow row,MapReply value,MapAssets images)
        {
            mapRow=row;scene=value;assets=images;SelectedObject=null;
            if(row==null)return;
            if(row.Kind=="Group"||row.Kind=="Vehicle")
            {
                Heading(row.Title);Note("Placed object · "+row.Name+"\n"+row.EngineKey+"\nPosition is read only. Use Select in World Editor to edit it.");
                if(row.Kind=="Group")GroupFreeze(row);return;
            }
            if(row.Kind=="FreezeCircle") { SelectedObject=new FreezeCircleProperties(row);GroupAssignments(row);return; }
            if(row.Kind=="Boundary") { SelectedObject=new BoundaryProperties(row);Note("This boundary controls return to the battlefield. Empty audience and groups means everyone.");Subjects("Apply restriction to",row.AppliesTo);BoundaryGroups(row); }
            if(row.Kind=="Zone") { Heading("Zone contour");Note("Use this existing contour in Objectives, or assign it to groups as a manual freeze area below.");GroupAssignments(row); }
            if(row.Kind=="Zone"||row.Kind=="Boundary") {Toggle("Show this contour on the game map",row.ShowMap,v=>{row.ShowMap=v;ShowRow(row,scene,assets);});if(!row.ShowMap)return;}
            var style=row.Style??new MapStyle{PixelSize=56*row.Scale,Tint=scene!=null&&row.ColorIndex>=0&&row.ColorIndex<scene.Colors.Count?scene.Colors[row.ColorIndex]:"1 1 1 1"};
            Heading("Appearance · live on map");
            if(row.Kind=="Icon")
            {
                EditorTheme.Add(rows,EditorTheme.Button("Choose symbol…",()=>{var current=row.Style??style;var previous=scene.Icons.FirstOrDefault(i=>i.Index==row.Icon);using(var dialog=new MapIconPicker(scene,assets,current.Tint,String.IsNullOrEmpty(current.ImageSet)&&previous!=null?previous.IconResource:current.ImageSet,String.IsNullOrEmpty(current.ImageSet)&&previous!=null?previous.Quad:current.Quad))if(dialog.ShowDialog(this)==DialogResult.OK){row.Icon=dialog.Selected.Index;Style().ImageSet=dialog.Selected.IconResource;Style().Quad=dialog.Selected.Quad;Notify();ShowRow(row,scene,assets);}},true));
                var icon=scene==null?null:scene.Icons.FirstOrDefault(x=>x.Index==row.Icon);Note(String.IsNullOrEmpty(style.ImageSet)?icon==null?"Select a catalog symbol.":icon.Name:style.Quad);
                TextField("Custom imageset / texture resource",style.ImageSet,v=>Style().ImageSet=v);
                TextField("Imageset symbol",style.Quad,v=>Style().Quad=v);
                EditorTheme.Add(rows,EditorTheme.Button("Load custom symbol preview",async()=>{var s=Style();if(assets!=null)await System.Threading.Tasks.Task.Run(()=>assets.Get(s.ImageSet,s.Quad));Notify();}));
                ColorField("Symbol color & opacity",style.Tint,v=>Style().Tint=v);
                Choice("Size",new[]{"Fixed screen size","Scale with map (metres)"},style.SizeMode,v=>Style().SizeMode=v);
                NumberField("Screen size (px)",style.PixelSize,8,256,v=>Style().PixelSize=v);
                NumberField("World size (m)",style.WorldSize,1,5000,v=>Style().WorldSize=v);
                NumberField("Minimum on screen (px)",style.MinPixels,1,256,v=>Style().MinPixels=v);
                NumberField("Maximum on screen (px)",style.MaxPixels,1,512,v=>Style().MaxPixels=v);
                NumberField("Rotation (degrees)",row.Rotation,0,359,v=>row.Rotation=(int)v);
                TextField("Tooltip description",row.Description,v=>row.Description=v);
            }
            else
            {
                bool mapSize=style.ShapeSizeMode!=0;
                Choice("Appearance size",new[]{"Fixed screen size (pixels)","Fixed map size (metres)"},style.ShapeSizeMode,v=>{Style().ShapeSizeMode=v;ShowRow(row,scene,assets);});
                Note("The contour always follows its world coordinates. This setting controls outline width, dashes and fill patterns. Labels use their separate pixel size.");
                ColorField("Outline",row.Stroke,v=>row.Stroke=v);NumberField(mapSize?"Outline width (m)":"Outline width (px)",row.Width,.1,32,v=>row.Width=v);
                Choice("Line appearance",new[]{"━━━━  Solid","╍╍╍╍  Dashed","······  Dotted","Texture"},style.StrokePattern,v=>Style().StrokePattern=v);
                TextField("Line texture (.edds)",style.StrokeTexture,v=>Style().StrokeTexture=v);
                if(row.Kind!="Line")
                {
                    Choice("Fill",new[]{"None","Solid color","Hatching","Texture"},style.FillPattern,v=>Style().FillPattern=v);
                    ColorField("Fill color & opacity",row.Fill,v=>row.Fill=v);Toggle("Fill outside the contour",style.FillOutside!=0,v=>Style().FillOutside=v?1:0);
                    TextField("Fill texture (.edds)",style.FillTexture,v=>Style().FillTexture=v);
                }
                NumberField(mapSize?"Pattern repetitions per metre":"Pattern repetitions per pixel",style.TextureScale,.005,1,v=>Style().TextureScale=v,3);
                EditorTheme.Add(rows,EditorTheme.Button("Load texture preview",async()=>{var s=Style();if(assets!=null)await System.Threading.Tasks.Task.Run(()=>{assets.Get(s.StrokeTexture);assets.Get(s.FillTexture);});Notify();}));
            }
            Heading("Label");TextField("Title",row.Title,v=>row.Title=v);Toggle("Show label",style.LabelVisible!=0,v=>Style().LabelVisible=v?1:0);
            TextField("Label override (blank = title)",style.LabelText,v=>Style().LabelText=v);ColorField("Label color",style.LabelColor,v=>Style().LabelColor=v);
            NumberField("Text size (px)",style.LabelSize,8,48,v=>Style().LabelSize=v);
            if(row.Kind=="Icon")Choice("Label position",new[]{"Above","Below","Left","Right","Center"},style.LabelPosition,v=>Style().LabelPosition=v);
            NumberField("Label horizontal offset",style.LabelX,-500,500,v=>Style().LabelX=v);NumberField("Label vertical offset",style.LabelY,-500,500,v=>Style().LabelY=v);
            Note("Layer order: move items in the list on the left. The top item is drawn in front, including its label.");
            Heading("Visibility");Toggle("Enabled",row.Enabled,v=>row.Enabled=v);Toggle("Visible before objective events",row.InitiallyVisible,v=>row.InitiallyVisible=v);
            Choice("Audience",new[]{"Everyone","Unassigned players","Selected factions","Selected alliances","Combined selection"},row.Audience,v=>{row.Audience=v;ShowRow(row,scene,assets);});
            if(row.Audience==4)Subjects("Visible to",row.Subjects);
            if(scene!=null&&scene.Forces!=null&&(row.Audience==2||row.Audience==3))
            {
                var selected=new HashSet<string>((row.AudienceIds??"").Split(','));
                var choices=row.Audience==2?scene.Forces.Factions.Select(f=>new KeyValuePair<string,string>(f.Key,f.DisplayName)):scene.Forces.Alliances.Select(a=>new KeyValuePair<string,string>(a,a));
                foreach(var pair in choices){string id=pair.Key;Toggle(pair.Value,selected.Contains(id),v=>{if(v)selected.Add(id);else selected.Remove(id);row.AudienceIds=String.Join(",",selected);});}
            }
            var phases=new[]{"Waiting","Lobby","Pre-brief","Briefing","Freeze","Game","Debriefing"};var bits=new[]{2,4,8,16,32,32,64};
            for(int i=0;i<phases.Length;i++){int index=i,bit=bits[i];bool on=(row.Phases&bit)!=0&&(i==4?row.DuringFreeze:i==5?row.AfterFreeze:true);Toggle(phases[i],on,v=>{if(index==4)row.DuringFreeze=v;else if(index==5)row.AfterFreeze=v;if(bit==32){if(row.DuringFreeze||row.AfterFreeze)row.Phases|=32;else row.Phases&=~32;}else if(v)row.Phases|=bit;else row.Phases&=~bit;});}
            EventRules("Show after objective event",row.ShowOn);EventRules("Hide after objective event",row.HideOn);
            Note("Any matching event triggers its list. Hide wins when events happen together. Preview shows initial visibility before events.");EditorTheme.Apply(rows);
        }
        void Subjects(string title,List<MapRuleSubject> subjects)
        {
            Heading(title);if(scene==null||scene.Forces==null)return;
            foreach(var f in scene.Forces.Factions){string id=f.Key;Toggle(f.DisplayName,subjects.Any(s=>s.Kind==0&&s.Id==id),v=>{subjects.RemoveAll(s=>s.Kind==0&&s.Id==id);if(v)subjects.Add(new MapRuleSubject{Kind=0,Id=id});});}
            foreach(string alliance in scene.Forces.Alliances){string id=alliance;string label=String.Join(" + ",scene.Forces.Factions.Where(f=>f.Alliance==id).Select(f=>f.DisplayName));Toggle(String.IsNullOrEmpty(label)?id:label,subjects.Any(s=>s.Kind==2&&s.Id==id),v=>{subjects.RemoveAll(s=>s.Kind==2&&s.Id==id);if(v)subjects.Add(new MapRuleSubject{Kind=2,Id=id});});}
            foreach(var entry in subjects.Where(s=>s.Kind==1).ToArray())EditorTheme.Add(rows,EditorTheme.Button("Remove saved side rule: "+entry.Id,()=>{subjects.Remove(entry);Notify();ShowRow(mapRow,scene,assets);}));
        }
        void BoundaryGroups(MapRow row)
        {
            if(scene==null||scene.Forces==null)return;Heading("Or selected groups");
            foreach(var group in scene.Forces.Groups.Where(g=>g.Playable!=0))
            {string id=GroupReference(group);Toggle(group.EngineKey+" · "+(group.Assigned!=0?group.Callsign:group.Name),row.BoundaryGroups.Contains(id),v=>{row.BoundaryGroups.Remove(id);if(v)row.BoundaryGroups.Add(id);});}
        }
        void EventRules(string title,List<MapVisibilityEvent> events)
        {
            Heading(title);
            foreach(var entry in events.ToArray())
            {
                var task=new EditorCombo();var ids=new List<string>{""};task.Items.Add("Choose objective…");if(scene!=null)foreach(var o in scene.Objectives){ids.Add(o.Key);task.Items.Add(o.Name);}
                if(!ids.Contains(entry.Objective??"")){ids.Add(entry.Objective);task.Items.Add("Missing · "+entry.Objective);}task.SelectedIndex=ids.IndexOf(entry.Objective??"");task.SelectedIndexChanged+=(s,e)=>{entry.Objective=ids[task.SelectedIndex];Notify();};EditorTheme.Add(rows,task);
                Choice("When",new[]{"Activated","Completed","Failed","Skipped"},entry.Event,v=>entry.Event=v);
                var party=new EditorCombo();var parties=new List<MapRuleSubject>{null};party.Items.Add("Any result / capturer");
                if(scene!=null&&scene.Forces!=null)foreach(var f in scene.Forces.Factions){party.Items.Add(f.DisplayName);parties.Add(new MapRuleSubject{Kind=0,Id=f.Key});}
                int picked=entry.Capturer==null?0:parties.FindIndex(p=>p!=null&&p.Kind==entry.Capturer.Kind&&p.Id==entry.Capturer.Id);if(picked<0){parties.Add(entry.Capturer);party.Items.Add("Saved selection · "+entry.Capturer.Id);picked=parties.Count-1;}party.SelectedIndex=picked;party.SelectedIndexChanged+=(s,e)=>{entry.Capturer=parties[party.SelectedIndex];Notify();};EditorTheme.Add(rows,EditorTheme.Field("Capturing party (zone / flag)",party));
                EditorTheme.Add(rows,EditorTheme.Button("Remove event",()=>{events.Remove(entry);Notify();ShowRow(mapRow,scene,assets);}));
            }
            EditorTheme.Add(rows,EditorTheme.Button("+ Add event",()=>{events.Add(new MapVisibilityEvent{Objective="",Event=1});Notify();ShowRow(mapRow,scene,assets);}));
        }
        void GroupFreeze(MapRow row)
        {
            if(scene.FreezeZones==null||scene.Forces==null)return;
            if(!String.IsNullOrEmpty(scene.FreezeZones.Problem)){Note(scene.FreezeZones.Problem);return;}
            var meta=scene.Forces.Groups.FirstOrDefault(g=>g.Key==row.EntityKey);if(meta==null||meta.Playable==0)return;
            var settings=scene.FreezeZones;var clone=settings.Clone();string id=String.IsNullOrEmpty(row.GroupRef)?row.Name:row.GroupRef;
            var current=clone.Coverage.FirstOrDefault(c=>c.Group==id||c.Group==row.Name||c.Group=="@"+meta.Key);
            if(current==null){current=new FreezeCoverage{Group=id,Zone=""};clone.Coverage.Add(current);}
            Heading("Freeze area");Note("Mission radius: "+settings.Radius+" m. Shared territory within "+row.EngineKey+". Automatic position is finalized at the commander when freeze begins.");
            var keys=new List<string>{"","-"};keys.AddRange(settings.Zones.Select(z=>z.Key));var labels=new List<string>{"Automatic / mission default","No generated area"};labels.AddRange(settings.Zones.Select(z=>"Custom · "+z.Label));
            addingFreezeFields=true;
            Choice("Area for this group",labels.ToArray(),Math.Max(0,keys.IndexOf(current.Zone??"")),v=>{current.Zone=keys[v];if(v>1&&clone.Mode==1)clone.Mode=3;StageFreeze(clone);});
            var radius=new NumericUpDown{Minimum=1,Maximum=5000,DecimalPlaces=1,Value=current.Radius>0?current.Radius:settings.Radius,Width=120,Enabled=current.Radius>0};
            Toggle("Use mission radius",current.Radius==0,v=>{radius.Enabled=!v;current.Radius=v?0:radius.Value;StageFreeze(clone);});radius.ValueChanged+=(s,e)=>{if(radius.Enabled){current.Radius=radius.Value;StageFreeze(clone);}};EditorTheme.Add(rows,EditorTheme.Field("Override radius (m)",radius));
            addingFreezeFields=false;
            EditorTheme.Add(rows,EditorTheme.Button("Apply group freeze area",()=>{if(FreezeChanged!=null)FreezeChanged(clone);},true));
        }
        void GroupAssignments(MapRow row)
        {
            if(scene==null||scene.FreezeZones==null||scene.Forces==null||String.IsNullOrEmpty(row.Name))return;
            if(!String.IsNullOrEmpty(scene.FreezeZones.Problem)){Note(scene.FreezeZones.Problem);return;}
            Heading("Freeze assignments");Note("Assign this existing zone to groups. It replaces their automatic circle in mixed mode; faction members share the combined territory.");
            var settings=scene.FreezeZones.Clone();
            addingFreezeFields=true;
            foreach(var group in scene.Forces.Groups.Where(g=>g.Playable!=0))
            {
                string id=GroupReference(group);
                var existing=settings.Coverage.FirstOrDefault(c=>c.Group==id||c.Group==group.Name||c.Group=="@"+group.Key);
                Toggle(group.EngineKey+" · "+(group.Assigned!=0?group.Callsign:group.Name),existing!=null&&existing.Zone==row.Name,v=>{var c=existing;if(c==null){c=new FreezeCoverage{Group=id};settings.Coverage.Add(c);existing=c;}c.Zone=v?row.Name:"";if(v&&settings.Mode!=2)settings.Mode=3;StageFreeze(settings);});
            }
            addingFreezeFields=false;
            EditorTheme.Add(rows,EditorTheme.Button("Apply zone assignments",()=>{if(FreezeChanged!=null)FreezeChanged(settings);},true));
        }
    }
}
