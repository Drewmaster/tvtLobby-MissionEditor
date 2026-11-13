using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    // Shared visual language from mission-editor-v06. These controls own presentation only.
    public static class EditorTheme
    {
        public static readonly Color Background=Color.FromArgb(20,25,29),Panel=Color.FromArgb(31,39,45),Raised=Color.FromArgb(44,54,62),Line=Color.FromArgb(74,86,96),Text=Color.FromArgb(239,243,246),Muted=Color.FromArgb(178,192,202),Disabled=Color.FromArgb(155,171,183),Input=Color.FromArgb(15,20,24),Amber=Color.FromArgb(230,174,80),Green=Color.FromArgb(133,210,172);
        public static Font Font(float size=10,FontStyle style=FontStyle.Regular){return new Font("Segoe UI",size,style);}
        public static Label Label(string text,float size=10,Color? color=null,bool bold=false){return new EditorLabel{Text=text,UseMnemonic=false,AutoSize=true,Font=Font(size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color??Text,Margin=new Padding(0,0,0,8),MaximumSize=new Size(950,0)};}
        public static TableLayoutPanel Stack(int padding=0){var p=new TableLayoutPanel{ColumnCount=1,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Padding=new Padding(padding),Margin=Padding.Empty};p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return p;}
        public static void Add(TableLayoutPanel p,Control c){int row=p.RowCount++;p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(c,0,row);c.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;}
        public static EditorButton Button(string text,Action action,bool primary=false){var b=new EditorButton{Text=text,Primary=primary};b.Click+=(s,e)=>action();return b;}
        public static void Grid(DataGridView g){g.BackgroundColor=Background;g.BorderStyle=BorderStyle.None;g.GridColor=Line;g.EnableHeadersVisualStyles=false;g.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;g.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;g.ColumnHeadersDefaultCellStyle.BackColor=Background;g.ColumnHeadersDefaultCellStyle.ForeColor=Muted;g.ColumnHeadersDefaultCellStyle.Font=Font(8);g.DefaultCellStyle.BackColor=Background;g.DefaultCellStyle.ForeColor=Text;g.DefaultCellStyle.SelectionBackColor=Color.FromArgb(55,49,39);g.DefaultCellStyle.SelectionForeColor=Text;g.RowTemplate.Height=42;g.ColumnHeadersHeight=32;}
        public static void Apply(Control root)
        {
            NativeTheme(root);
            foreach(Control c in root.Controls){
                if(c is TextBoxBase){c.BackColor=Input;if(!(c is RichTextBox))c.ForeColor=Text;((TextBoxBase)c).BorderStyle=BorderStyle.FixedSingle;}
                else if(c is NumericUpDown){c.BackColor=Input;c.ForeColor=Text;((NumericUpDown)c).BorderStyle=BorderStyle.FixedSingle;}
                else if(c is DataGridView)Grid((DataGridView)c);
                else if(c is ListBox){c.BackColor=Background;c.ForeColor=Text;((ListBox)c).BorderStyle=BorderStyle.None;}
                else if(c is Button && !(c is EditorButton)){var b=(Button)c;b.FlatStyle=FlatStyle.Flat;b.BackColor=Raised;b.ForeColor=Text;b.FlatAppearance.BorderColor=Line;b.Padding=new Padding(8,4,8,4);}
                Apply(c);
            }
        }
        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr handle,string app,string ids);
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control,InputColors> nativeColors=new System.Runtime.CompilerServices.ConditionalWeakTable<Control,InputColors>();
        static void NativeTheme(Control control){nativeColors.GetValue(control,c=>new InputColors(c));}
        // Disabled native edit children otherwise use Windows' light/embossed palette.
        // Scope the override to this editor, including the Edit inside an editable combo.
        sealed class InputColors : NativeWindow
        {
            readonly Control owner;
            [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern int SetTextColor(IntPtr dc,int color);
            [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern int SetBkColor(IntPtr dc,int color);
            [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern int SetDCBrushColor(IntPtr dc,int color);
            [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr GetStockObject(int index);
            public InputColors(Control control){owner=control;owner.HandleCreated+=(s,e)=>Attach();owner.HandleDestroyed+=(s,e)=>ReleaseHandle();if(owner.IsHandleCreated)Attach();}
            void Attach(){AssignHandle(owner.Handle);SetWindowTheme(owner.Handle,"DarkMode_Explorer",null);}
            protected override void WndProc(ref Message m)
            {
                if(owner is TextBox && DisabledEdit.Paint(ref m,owner))return;
                base.WndProc(ref m);
                if(m.Msg!=0x133&&m.Msg!=0x138)return; // WM_CTLCOLOREDIT / WM_CTLCOLORSTATIC
                Control edit=Control.FromHandle(m.LParam);
                bool field=edit is TextBoxBase || owner is ComboBox || owner is UpDownBase;
                if(!field)return;
                var bg=edit!=null?edit.BackColor:owner.BackColor;
                var fg=owner.Enabled&&(edit==null||edit.Enabled)?(edit!=null?edit.ForeColor:owner.ForeColor):Disabled;
                SetTextColor(m.WParam,ColorTranslator.ToWin32(fg));SetBkColor(m.WParam,ColorTranslator.ToWin32(bg));SetDCBrushColor(m.WParam,ColorTranslator.ToWin32(bg));m.Result=GetStockObject(18); // DC_BRUSH, no allocated GDI resource
            }
        }
        public static TableLayoutPanel Field(string title,Control input,string help="")
        {var p=Stack();p.Margin=new Padding(0,0,0,16);Add(p,Label(title));input.Dock=DockStyle.Top;input.Margin=new Padding(0,0,0,7);Add(p,input);if(help!="")Add(p,Label(help,8.5f,Muted));return p;}
        public static Control Pair(Control first,Control second)
        {var p=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Margin=Padding.Empty};p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));first.Dock=second.Dock=DockStyle.Fill;first.Margin=new Padding(0,0,9,0);second.Margin=new Padding(9,0,0,0);p.Controls.Add(first);p.Controls.Add(second);return p;}
    }
    // Native Edit ignores WM_CTLCOLOR's text color when WS_DISABLED is set.
    // Paint only that non-interactive state; Windows still owns editing, selection and IME.
    internal static class DisabledEdit
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct PaintStruct{public IntPtr dc;public int erase;public RectangleNative rect;public int restore,update;[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray,SizeConst=32)]public byte[] reserved;}
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct RectangleNative{public int left,top,right,bottom;}
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr BeginPaint(IntPtr window,out PaintStruct paint);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool EndPaint(IntPtr window,ref PaintStruct paint);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out RectangleNative rect);
        public static bool Paint(ref Message m,Control owner)
        {
            if(owner.Enabled||(m.Msg!=0xF&&m.Msg!=0x318))return false; // WM_PAINT / WM_PRINTCLIENT
            PaintStruct paint=new PaintStruct();bool begin=m.Msg==0xF;
            IntPtr dc=begin?BeginPaint(m.HWnd,out paint):m.WParam;
            try{
                RectangleNative client;GetClientRect(m.HWnd,out client);
                using(var g=Graphics.FromHdc(dc))
                {
                    g.Clear(owner.BackColor);
                    var box=owner as TextBox;bool multiline=box!=null&&box.Multiline;
                    var flags=TextFormatFlags.NoPrefix|TextFormatFlags.PreserveGraphicsClipping|(multiline?TextFormatFlags.WordBreak:TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter);
                    if(box!=null&&box.TextAlign==HorizontalAlignment.Right)flags|=TextFormatFlags.Right;
                    if(box!=null&&box.TextAlign==HorizontalAlignment.Center)flags|=TextFormatFlags.HorizontalCenter;
                    string text=owner.Text;if(box!=null&&(box.UseSystemPasswordChar||box.PasswordChar!='\0'))text=new string(box.PasswordChar=='\0'?'●':box.PasswordChar,text.Length);
                    TextRenderer.DrawText(g,text,owner.Font,new Rectangle(0,0,client.right,client.bottom),EditorTheme.Disabled,flags);
                }
            }finally{if(begin)EndPaint(m.HWnd,ref paint);}
            m.Result=IntPtr.Zero;return true;
        }
    }
    public sealed class EditorLabel : Label
    {
        public EditorLabel(){UseCompatibleTextRendering=false;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);ForeColor=EditorTheme.Text;}
        protected override void OnPaint(PaintEventArgs e)
        {
            // A pending request disables interaction, not the explanatory text.
            var flags=TextFormatFlags.WordBreak|TextFormatFlags.PreserveGraphicsClipping|TextFormatFlags.PreserveGraphicsTranslateTransform;
            if(!UseMnemonic)flags|=TextFormatFlags.NoPrefix;
            if(AutoEllipsis)flags|=TextFormatFlags.EndEllipsis;
            if(TextAlign==ContentAlignment.TopCenter||TextAlign==ContentAlignment.MiddleCenter||TextAlign==ContentAlignment.BottomCenter)flags|=TextFormatFlags.HorizontalCenter;
            if(TextAlign==ContentAlignment.TopRight||TextAlign==ContentAlignment.MiddleRight||TextAlign==ContentAlignment.BottomRight)flags|=TextFormatFlags.Right;
            if(TextAlign==ContentAlignment.MiddleLeft||TextAlign==ContentAlignment.MiddleCenter||TextAlign==ContentAlignment.MiddleRight)flags|=TextFormatFlags.VerticalCenter;
            if(TextAlign==ContentAlignment.BottomLeft||TextAlign==ContentAlignment.BottomCenter||TextAlign==ContentAlignment.BottomRight)flags|=TextFormatFlags.Bottom;
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(Padding.Left,Padding.Top,Math.Max(0,Width-Padding.Horizontal),Math.Max(0,Height-Padding.Vertical)),ForeColor,flags);
        }
    }
    public class EditorButton : Button
    {
        public bool Primary,Navigation,Selected;
        public int LayoutHeight;
        public string Detail="",Badge="";
        bool hover;
        public EditorButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;AutoSize=true;MinimumSize=new Size(80,34);Padding=new Padding(12,5,12,5);Margin=new Padding(0,0,8,6);Font=EditorTheme.Font(9);UseMnemonic=false;Cursor=Cursors.Hand;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color bg=Primary&&Enabled?EditorTheme.Amber:Selected?Color.FromArgb(53,47,37):Navigation?Parent.BackColor:EditorTheme.Raised;
            if(hover&&Enabled)bg=Primary?Color.FromArgb(240,190,114):Color.FromArgb(52,61,68);
            using(var brush=new SolidBrush(bg))g.FillRectangle(brush,ClientRectangle);
            if(!Navigation||Selected){using(var pen=new Pen(Selected?Color.FromArgb(121,98,62):Primary&&Enabled?EditorTheme.Amber:EditorTheme.Line))g.DrawRectangle(pen,0,0,Width-1,Height-1);}
            if(Navigation&&Selected){using(var b=new SolidBrush(EditorTheme.Amber))g.FillRectangle(b,0,0,3,Height);}
            Color fg=!Enabled?EditorTheme.Disabled:Primary?Color.FromArgb(32,27,19):Selected?Color.FromArgb(243,217,176):EditorTheme.Text;
            var rect=new Rectangle(12,Detail==""?0:10,Width-24-(Badge==""?0:24),Detail==""?Height:24);
            TextRenderer.DrawText(g,Text,Font,rect,fg,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|(Navigation||Detail!=""?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            if(Detail!="")using(var f=EditorTheme.Font(8))TextRenderer.DrawText(g,Detail,f,new Rectangle(12,36,Width-24,Height-39),Enabled?EditorTheme.Muted:EditorTheme.Disabled,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            if(Badge!=""){using(var b=new SolidBrush(EditorTheme.Amber))g.FillEllipse(b,Width-31,12,20,20);using(var f=EditorTheme.Font(8))TextRenderer.DrawText(g,Badge,f,new Rectangle(Width-31,12,20,20),EditorTheme.Background,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-9,Height-9),EditorTheme.Amber,bg);
        }
    }
    public sealed class EditorToggle : CheckBox
    {
        public bool Compact;
        public EditorToggle(){AutoSize=false;Size=new Size(78,30);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.CheckButton;}
        protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
        public override Size GetPreferredSize(Size proposed){return Compact?new Size(78,30):new Size(Math.Max(110,TextRenderer.MeasureText(Text,Font).Width+52),32);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent==null?EditorTheme.Background:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;int y=(Height-18)/2;using(var path=new GraphicsPath()){path.AddArc(0,y,18,18,90,180);path.AddArc(14,y,18,18,270,180);path.CloseFigure();using(var b=new SolidBrush(Checked?Color.FromArgb(149,115,53):Color.FromArgb(50,60,66)))g.FillPath(b,path);using(var p=new Pen(Checked?Color.FromArgb(178,136,72):Color.FromArgb(102,116,122)))g.DrawPath(p,path);}using(var b=new SolidBrush(Checked?Color.FromArgb(255,241,216):EditorTheme.Muted))g.FillEllipse(b,Checked?16:4,y+3,12,12);TextRenderer.DrawText(g,Compact?(Checked?"On":"Off"):Text,Font,new Rectangle(42,0,Width-42,Height),Enabled?EditorTheme.Text:EditorTheme.Disabled,TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle);}
    }
    public class EditorCombo : ComboBox
    {
        sealed class EditWindow : NativeWindow
        {readonly EditorCombo owner;public EditWindow(EditorCombo control,IntPtr handle){owner=control;AssignHandle(handle);}protected override void WndProc(ref Message m){if(!owner.IgnoreClosedWheel(ref m)&&!DisabledEdit.Paint(ref m,owner))base.WndProc(ref m);}}
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct ComboInfo{public int size;public DisabledEdit.RectangleNative item,button;public int state;public IntPtr combo,edit,list;}
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool GetComboBoxInfo(IntPtr combo,ref ComboInfo info);
        EditWindow editWindow;
        // Both vertical and horizontal wheel messages can change a native combo.
        // Protect closed selections, including the Edit child, even when focused.
        bool IgnoreClosedWheel(ref Message m)
        {
            if((m.Msg!=0x020A&&m.Msg!=0x020E)||DroppedDown||DropDownStyle==ComboBoxStyle.Simple)return false;
            m.Result=IntPtr.Zero;return true;
        }
        protected override void WndProc(ref Message m){if(!IgnoreClosedWheel(ref m))base.WndProc(ref m);}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(DropDownStyle==ComboBoxStyle.DropDown){var info=new ComboInfo{size=System.Runtime.InteropServices.Marshal.SizeOf(typeof(ComboInfo))};if(GetComboBoxInfo(Handle,ref info)&&info.edit!=IntPtr.Zero)editWindow=new EditWindow(this,info.edit);}}
        protected override void OnHandleDestroyed(EventArgs e){if(editWindow!=null){editWindow.ReleaseHandle();editWindow=null;}base.OnHandleDestroyed(e);}
        public EditorCombo(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=24;FlatStyle=FlatStyle.Flat;BackColor=EditorTheme.Input;ForeColor=EditorTheme.Text;DropDownStyle=ComboBoxStyle.DropDownList;Font=EditorTheme.Font(9);SelectedIndexChanged+=(s,e)=>Invalidate();TextChanged+=(s,e)=>Invalidate();}
        protected override void OnDrawItem(DrawItemEventArgs e){var bg=(e.State&DrawItemState.Selected)!=0?Color.FromArgb(55,49,39):BackColor;using(var b=new SolidBrush(bg))e.Graphics.FillRectangle(b,e.Bounds);if(e.Index>=0)TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),Enabled?ForeColor:EditorTheme.Disabled,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(BackColor);
            if(DropDownStyle==ComboBoxStyle.DropDownList)TextRenderer.DrawText(g,Text,Font,new Rectangle(8,0,Math.Max(1,Width-32),Height),Enabled?ForeColor:EditorTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            using(var p=new Pen(Enabled?EditorTheme.Text:EditorTheme.Muted,1.5f)){int x=Width-12,y=Height/2;g.DrawLine(p,x-3,y-2,x,y+1);g.DrawLine(p,x,y+1,x+3,y-2);}
            using(var p=new Pen(Focused?EditorTheme.Amber:Color.FromArgb(73,83,90)))g.DrawRectangle(p,0,0,Width-1,Height-1);
        }
    }
    public sealed class EditorTabs : TabControl
    {
        public EditorTabs(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);DrawMode=TabDrawMode.OwnerDrawFixed;SizeMode=TabSizeMode.Fixed;ItemSize=new Size(200,34);Padding=new Point(12,6);SelectedIndexChanged+=(s,e)=>Invalidate();}
        void DrawTab(Graphics g,int index){var r=GetTabRect(index);using(var b=new SolidBrush(EditorTheme.Background))g.FillRectangle(b,r);TextRenderer.DrawText(g,TabPages[index].Text,Font,r,index==SelectedIndex?EditorTheme.Text:EditorTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(index==SelectedIndex)using(var p=new Pen(EditorTheme.Amber,2))g.DrawLine(p,r.Left,r.Bottom-2,r.Right,r.Bottom-2);}
        protected override void OnDrawItem(DrawItemEventArgs e){DrawTab(e.Graphics,e.Index);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(EditorTheme.Background);for(int i=0;i<TabPages.Count;i++)DrawTab(e.Graphics,i);using(var p=new Pen(EditorTheme.Line))e.Graphics.DrawLine(p,0,ItemSize.Height+3,Width,ItemSize.Height+3);}
    }
    public sealed class SettingRow : UserControl
    {
        public readonly string Key;public readonly Control Input;readonly Label title,help,defaultLabel;readonly object original;
        public bool Modified {get;private set;}
        public SettingRow(string key,string name,string explanation,Control input,object defaultValue)
        {
            Key=key;Input=input;original=defaultValue;Dock=DockStyle.Top;Margin=Padding.Empty;BackColor=EditorTheme.Background;
            title=EditorTheme.Label(name,9.5f,null,true);help=EditorTheme.Label(explanation,8.5f,EditorTheme.Muted);defaultLabel=EditorTheme.Label("",8,EditorTheme.Muted);Controls.AddRange(new Control[]{title,help,defaultLabel,input});input.AccessibleName=name;input.EnabledChanged+=(s,e)=>RefreshValue();SizeChanged+=(s,e)=>Arrange();RefreshValue();
        }
        object Value(){var c=Input as CheckBox;var n=Input as NumericUpDown;var combo=Input as ComboBox;return c!=null?(object)(c.Checked?1:0):n!=null?n.Value:combo!=null?(Key=="Weather"?(object)(combo.SelectedIndex>0?combo.Text:""):combo.SelectedIndex):(object)Input.Text;}
        string Display(object value){if(Input is CheckBox)return Convert.ToInt32(value)!=0?"On":"Off";var combo=Input as ComboBox;if(combo!=null){if(Key=="Weather")return "Terrain default";int i=Convert.ToInt32(value);return i>=0&&i<combo.Items.Count?combo.Items[i].ToString():value.ToString();}return Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);}
        public void RefreshValue(){Modified=Input is NumericUpDown?Convert.ToDecimal(Value())!=Convert.ToDecimal(original):Convert.ToString(Value(),System.Globalization.CultureInfo.InvariantCulture)!=Convert.ToString(original,System.Globalization.CultureInfo.InvariantCulture);defaultLabel.Text="Default: "+Display(original)+(Modified?"  · Modified":"");defaultLabel.ForeColor=Modified?EditorTheme.Amber:EditorTheme.Muted;title.ForeColor=Input.Enabled?EditorTheme.Text:EditorTheme.Muted;Arrange();Invalidate();}
        public void Reset(){var c=Input as CheckBox;var n=Input as NumericUpDown;var combo=Input as ComboBox;if(c!=null)c.Checked=Convert.ToInt32(original)!=0;else if(n!=null)n.Value=Convert.ToDecimal(original);else if(combo!=null)combo.SelectedIndex=Key=="Weather"?0:Convert.ToInt32(original);else if(Input is TextBox)((TextBox)Input).Text=Convert.ToString(original);}
        void Arrange()
        {
            if(Width<50)return;
            int inputWidth=Input is CheckBox?78:Input is NumericUpDown?115:Math.Min(270,Math.Max(155,Width*36/100));
            int textWidth=Math.Max(90,Width-inputWidth-24),y=15;
            foreach(var label in new[]{title,help,defaultLabel})
            {
                bool hasText=label.Text.Length>0;label.Visible=hasText;label.AutoSize=false;label.MaximumSize=Size.Empty;
                int h=hasText?TextRenderer.MeasureText(label.Text,label.Font,new Size(textWidth,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height:0;
                label.SetBounds(0,y,textWidth,h);if(hasText)y+=h+(label==title?5:8);
            }
            int height=Math.Max(73,y+8);if(Height!=height)Height=height;
            Input.SetBounds(Width-inputWidth,Math.Max(16,(height-Input.Height)/2),inputWidth,Input.Height);
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var p=new Pen(EditorTheme.Line))e.Graphics.DrawLine(p,0,Height-1,Width,Height-1);}
    }
    public sealed class SettingsDeck : UserControl
    {
        sealed class Category {public string Name,Detail;public TableLayoutPanel Body;public EditorButton Nav;public Panel Scroll;public Action Reset;public Func<int> Count;public readonly List<SettingRow> Rows=new List<SettingRow>();}
        readonly List<Category> categories=new List<Category>();readonly FlowLayoutPanel nav=new FlowLayoutPanel();readonly Panel detail=new Panel();readonly Label summary=EditorTheme.Label("",9,EditorTheme.Muted),title=EditorTheme.Label("",18,null,true),subtitle=EditorTheme.Label("",9,EditorTheme.Muted);readonly EditorButton reset;readonly Panel header=new Panel();int selected;
        public event Action SelectionChanged;
        public bool Embedded {get;set;}
        public int SelectedIndex {get{return selected;}set{selected=Math.Max(0,Math.Min(categories.Count-1,value));Display();if(SelectionChanged!=null)SelectionChanged();}}
        public int CategoryCount {get{return categories.Count;}}
        public int ModifiedCount {get{return categories.Sum(c=>c.Rows.Count(r=>r.Modified));}}
        public SettingsDeck(){Dock=DockStyle.Fill;BackColor=EditorTheme.Background;summary.Padding=new Padding(14,12,14,12);summary.BackColor=Color.FromArgb(32,39,40);summary.AutoSize=false;Controls.Add(summary);nav.FlowDirection=FlowDirection.TopDown;nav.WrapContents=false;nav.AutoScroll=true;nav.BackColor=Color.FromArgb(24,31,34);nav.Padding=new Padding(12);Controls.Add(nav);Controls.Add(detail);header.Controls.AddRange(new Control[]{title,subtitle});reset=EditorTheme.Button("Restore defaults",()=>ResetSelected());header.Controls.Add(reset);detail.Controls.Add(header);SizeChanged+=(s,e)=>Arrange();}
        public TableLayoutPanel AddSection(string name,string description){var cat=new Category{Name=name,Detail=description,Body=EditorTheme.Stack(),Scroll=new Panel{AutoScroll=true,Padding=new Padding(24,0,24,12)}};cat.Body.Dock=DockStyle.Top;cat.Scroll.Controls.Add(cat.Body);cat.Nav=new EditorButton{Text=name,Detail=description,Navigation=true,AutoSize=false,Size=new Size(195,86),Margin=new Padding(0,0,0,6)};cat.Nav.Click+=(s,e)=>SelectedIndex=categories.IndexOf(cat);categories.Add(cat);nav.Controls.Add(cat.Nav);detail.Controls.Add(cat.Scroll);Display();return cat.Body;}
        public void Row(TableLayoutPanel body,string key,string name,string help,Control input,object def){var row=new SettingRow(key,name,help,input,def);categories.First(c=>c.Body==body).Rows.Add(row);EditorTheme.Add(body,row);}
        public void AddPage(string name,string description,Control page,Action restore,Func<int> modified,int position)
        {
            var body=AddSection(name,description);var cat=categories.Last();cat.Scroll.Controls.Remove(body);body.Dispose();cat.Body=null;
            cat.Scroll.AutoScroll=false;cat.Scroll.Padding=Padding.Empty;page.Dock=DockStyle.Fill;cat.Scroll.Controls.Add(page);cat.Reset=restore;cat.Count=modified;
            categories.Remove(cat);categories.Insert(Math.Max(0,Math.Min(position,categories.Count)),cat);nav.Controls.SetChildIndex(cat.Nav,categories.IndexOf(cat));
            // All navigation callbacks resolve their current index after insertion.
            Display();
        }
        public void SelectSection(string name){int index=categories.FindIndex(c=>c.Name==name);if(index>=0)SelectedIndex=index;}
        public string SelectedSection {get{return categories.Count==0?"":categories[selected].Name;}}
        public void Group(TableLayoutPanel body,string name,string help=""){var p=EditorTheme.Stack(10);p.BackColor=Color.FromArgb(35,44,47);p.Margin=new Padding(0,12,0,0);EditorTheme.Add(p,EditorTheme.Label(name,10,null,true));if(help!="")EditorTheme.Add(p,EditorTheme.Label(help,8.5f,EditorTheme.Muted));EditorTheme.Add(body,p);}
        public void Note(TableLayoutPanel body,string text){var label=EditorTheme.Label(text,9,Color.FromArgb(164,183,187));label.BackColor=Color.FromArgb(32,40,40);label.Padding=new Padding(13);label.Margin=new Padding(0,18,0,8);EditorTheme.Add(body,label);}
        int Count(Category c){return c.Count!=null?c.Count():c.Rows.Count(r=>r.Modified);}
        public void UpdateValues(string text){summary.Text=text;foreach(var c in categories){foreach(var row in c.Rows)row.RefreshValue();int count=Count(c);c.Nav.Badge=count==0?"":count.ToString();c.Nav.Invalidate();}reset.Enabled=Enabled&&categories.Count>0&&Count(categories[selected])>0;}
        public void ResetSelected(){if(categories.Count==0)return;var c=categories[selected];if(c.Reset!=null)c.Reset();else foreach(var row in c.Rows)row.Reset();Display();}
        void Display(){if(categories.Count==0)return;for(int i=0;i<categories.Count;i++){categories[i].Scroll.Visible=i==selected;categories[i].Nav.Selected=i==selected;categories[i].Nav.Invalidate();}title.Text=categories[selected].Name;subtitle.Text=categories[selected].Detail;reset.Enabled=Count(categories[selected])>0;Arrange();}
        void Arrange()
        {
            if(Width<80)return;
            summary.Visible=nav.Visible=header.Visible=!Embedded;
            if(Embedded){detail.SetBounds(0,0,Width,Height);foreach(var c in categories)c.Scroll.SetBounds(0,0,Width,Height);return;}
            bool small=Width<850,shortWindow=Height<460;
            int summaryHeight=shortWindow?34:46,top=summaryHeight+(shortWindow?8:16),navHeight=shortWindow?46:64,navOffset=shortWindow?48:70,headerHeight=shortWindow?66:95;
            summary.MaximumSize=Size.Empty;summary.Padding=new Padding(14,shortWindow?7:12,14,0);summary.SetBounds(0,0,Width,summaryHeight);
            nav.SetBounds(0,top,small?Width:220,small?navHeight:Math.Max(0,Height-top));nav.Padding=new Padding(12,shortWindow?3:12,12,0);nav.FlowDirection=small?FlowDirection.LeftToRight:FlowDirection.TopDown;nav.WrapContents=false;
            foreach(var c in categories){c.Nav.Size=new Size(small?Math.Max(140,(Width-34)/Math.Max(1,categories.Count)):195,small?38:86);c.Nav.Detail=small?"":c.Detail;}
            int detailTop=small?top+navOffset:top;detail.SetBounds(small?0:220,detailTop,small?Width:Width-220,Math.Max(0,Height-detailTop));
            header.SetBounds(24,0,Math.Max(0,detail.Width-48),headerHeight);title.AutoSize=false;title.MaximumSize=Size.Empty;title.SetBounds(0,7,Math.Max(90,header.Width-150),28);
            subtitle.SetBounds(0,40,Math.Max(90,header.Width-145),22);subtitle.MaximumSize=new Size(Math.Max(90,header.Width-145),0);reset.SetBounds(Math.Max(0,header.Width-135),8,135,34);
            foreach(var c in categories)c.Scroll.SetBounds(0,headerHeight,detail.Width,Math.Max(0,detail.Height-headerHeight));
            nav.PerformLayout();if(categories.Count>0)nav.ScrollControlIntoView(categories[selected].Nav);
        }
    }
}
