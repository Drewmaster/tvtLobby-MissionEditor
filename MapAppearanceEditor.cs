using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace TvtLobby.MissionEditor
{
    public sealed partial class MapAppearanceEditor : UserControl
    {
        readonly TableLayoutPanel rows=EditorTheme.Stack();object target;public event EventHandler PropertyValueChanged;
        public object SelectedObject{get{return target;}set{target=value;Rebuild();}}
        public MapAppearanceEditor(){AutoScroll=true;BackColor=EditorTheme.Panel;Padding=new Padding(12);Controls.Add(rows);}
        void Rebuild()
        {
            rows.SuspendLayout();while(rows.Controls.Count>0)rows.Controls[0].Dispose();rows.RowCount=0;rows.RowStyles.Clear();if(target==null){rows.ResumeLayout();return;}
            foreach(var group in TypeDescriptor.GetProperties(target).Cast<PropertyDescriptor>().Where(p=>p.IsBrowsable).GroupBy(p=>p.Category))
            {
                var caption=EditorTheme.Label(group.Key.ToUpperInvariant(),8,EditorTheme.Amber,true);caption.Padding=new Padding(0,16,0,4);EditorTheme.Add(rows,caption);
                foreach(var property in group){var p=property;object value=p.GetValue(target);Control input;
                    if(p.PropertyType==typeof(bool)){var c=new EditorToggle{Text=Words(p.DisplayName),Checked=(bool)value,AutoSize=true};c.CheckedChanged+=(s,e)=>Write(p,c.Checked);input=c;}
                    else if(p.Name=="Stroke"||p.Name=="Fill"){var color=new MapColorField(Convert.ToString(value));color.Changed+=(s,e)=>Write(p,color.Value);input=EditorTheme.Field(p.Name=="Stroke"?"Outline color":"Fill color",color);}
                    else if(p.PropertyType.IsEnum&&p.PropertyType.IsDefined(typeof(FlagsAttribute),false)){var flags=EditorTheme.Stack();foreach(object item in Enum.GetValues(p.PropertyType)){int bit=Convert.ToInt32(item);if(bit==0)continue;var c=new EditorToggle{Text=Words(item.ToString()),Checked=(Convert.ToInt32(value)&bit)!=0,AutoSize=true};c.CheckedChanged+=(s,e)=>{int current=Convert.ToInt32(p.GetValue(target));Write(p,Enum.ToObject(p.PropertyType,c.Checked?current|bit:current&~bit));};EditorTheme.Add(flags,c);}input=EditorTheme.Field(Words(p.DisplayName),flags);}
                    else if(p.PropertyType.IsEnum){var c=new EditorCombo();c.Items.AddRange(Enum.GetValues(p.PropertyType).Cast<object>().ToArray());c.SelectedItem=value;c.SelectedIndexChanged+=(s,e)=>{if(c.SelectedItem!=null)Write(p,c.SelectedItem);};input=EditorTheme.Field(Words(p.DisplayName),c,p.Description);}
                    else if(p.PropertyType==typeof(int)||p.PropertyType==typeof(double)){var n=new NumericUpDown{Minimum=-100000,Maximum=100000,DecimalPlaces=p.PropertyType==typeof(double)?2:0,Value=Convert.ToDecimal(value),Width=110};n.ValueChanged+=(s,e)=>Write(p,Convert.ChangeType(n.Value,p.PropertyType));input=EditorTheme.Field(Words(p.DisplayName),n,p.Description);}
                    else{var t=new TextBox{Text=Convert.ToString(value),Multiline=p.Name=="Description",Height=p.Name=="Description"?65:30};t.TextChanged+=(s,e)=>Write(p,t.Text);input=EditorTheme.Field(Words(p.DisplayName),t,p.Description);}
                    EditorTheme.Add(rows,input);
                }
            }
            EditorTheme.Apply(rows);rows.ResumeLayout(true);
        }
        static string Words(string s){return System.Text.RegularExpressions.Regex.Replace(s,"([a-z])([A-Z])","$1 $2");}
        void Write(PropertyDescriptor p,object value){if(target==null)return;p.SetValue(target,value);if(PropertyValueChanged!=null)PropertyValueChanged(this,EventArgs.Empty);}
    }
    public sealed class MapColorField : UserControl
    {
        sealed class Swatch : EditorButton
        {
            public Color ColorValue {get;set;}
            protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var brush=new SolidBrush(ColorValue))e.Graphics.FillRectangle(brush,8,9,16,16);}
        }
        readonly double[] parts=new double[4];readonly NumericUpDown alpha;readonly Swatch color;string value;
        public string Value{get{return value;}}public event EventHandler Changed;
        public MapColorField(string source)
        {
            value=source;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
            var split=(source??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            for(int i=0;i<4;i++){double part;parts[i]=i<split.Length&&Double.TryParse(split[i],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out part)?part:1;}
            var row=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,WrapContents=true};Controls.Add(row);
            color=new Swatch{Width=140,AutoSize=false};UpdateColor();row.Controls.Add(color);
            alpha=new NumericUpDown{Minimum=0,Maximum=100,DecimalPlaces=1,Width=65,Value=(decimal)Math.Max(0,Math.Min(100,parts[3]*100)),Margin=new Padding(4,6,0,0)};
            row.Controls.Add(alpha);row.Controls.Add(new EditorLabel{Text="% opacity",AutoSize=true,Margin=new Padding(4,9,0,0),ForeColor=EditorTheme.Muted});
            alpha.ValueChanged+=(s,e)=>{parts[3]=(double)alpha.Value/100;Publish();};
            color.Click+=(s,e)=>{using(var pick=new ColorDialog{Color=color.ColorValue,FullOpen=true})if(pick.ShowDialog(this)==DialogResult.OK){parts[0]=pick.Color.R/255d;parts[1]=pick.Color.G/255d;parts[2]=pick.Color.B/255d;UpdateColor();Publish();}};
        }
        void UpdateColor(){color.ColorValue=Color.FromArgb(Byte(parts[0]),Byte(parts[1]),Byte(parts[2]));color.Text="    #"+color.ColorValue.R.ToString("X2")+color.ColorValue.G.ToString("X2")+color.ColorValue.B.ToString("X2");color.Invalidate();}
        static int Byte(double value){return (int)Math.Round(Math.Max(0,Math.Min(1,value))*255);}
        void Publish(){value=String.Join(" ",parts.Select(p=>p.ToString("G17",System.Globalization.CultureInfo.InvariantCulture)));if(Changed!=null)Changed(this,EventArgs.Empty);}
    }
}
