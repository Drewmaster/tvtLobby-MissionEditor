using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace TvtLobby.MissionEditor
{
    // Native RichText markup is the storage format. Links/assets retain stable keys.
    public sealed class BriefingTextEditor : UserControl
    {
        sealed class Reference {public int Start,Length;public string Key;public bool Image;}
        readonly List<Reference> refs=new List<Reference>();
        public readonly RichTextBox TextArea=new RichTextBox{Dock=DockStyle.Fill,DetectUrls=false,AcceptsTab=true,HideSelection=false,Font=EditorTheme.Font(11),ForeColor=EditorTheme.Text,BorderStyle=BorderStyle.FixedSingle};
        readonly FlowLayoutPanel toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=78,WrapContents=true};
        readonly Label help=EditorTheme.Label("",8.5f,EditorTheme.Muted);
        string original="",previous="";bool loading,changed,source;
        public event Action Changed;public event Action LinkRequested;public event Action ImageRequested;
        public BriefingTextEditor()
        {
            Height=380;Dock=DockStyle.Top;
            var frame=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,76));frame.RowStyles.Add(new RowStyle(SizeType.Percent,100));frame.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            frame.Controls.Add(toolbar);frame.Controls.Add(TextArea,0,1);frame.Controls.Add(help,0,2);Controls.Add(frame);
            Add("B",()=>Style(FontStyle.Bold));Add("I",()=>Style(FontStyle.Italic));Add("U",()=>Style(FontStyle.Underline));Add("Heading",()=>SetFontSize(16));Add("• List",()=>List(false));Add("1. List",()=>List(true));Add("Clear",()=>{TextArea.SelectionFont=EditorTheme.Font(11);Mark();});
            Add("Insert link",()=>{if(LinkRequested!=null)LinkRequested();});Add("Insert image",()=>{if(ImageRequested!=null)ImageRequested();});Add("Source / visual",()=>{string text=Read();LoadText(text,!source);changed=true;Mark();});
            TextArea.TextChanged+=(s,e)=>OnTextEdited();EditorTheme.Apply(this);
        }
        void Add(string name,Action action){var b=EditorTheme.Button(name,()=>{TextArea.Focus();action();});b.AutoSize=false;b.MinimumSize=new Size(34,30);b.Width=name.Length<=2?36:name=="Source / visual"?125:Math.Max(65,name.Length*8+12);b.Height=30;b.Margin=new Padding(0,0,5,4);if(name=="B")b.Font=EditorTheme.Font(9,FontStyle.Bold);if(name=="I")b.Font=EditorTheme.Font(9,FontStyle.Italic);if(name=="U")b.Font=EditorTheme.Font(9,FontStyle.Underline);toolbar.Controls.Add(b);}
        public void LoadText(string text,bool sourceMode=false)
        {
            original=text??"";source=sourceMode||Regex.Matches(original,@"<[^>]*>").Cast<Match>().Any(m=>!Regex.IsMatch(m.Value,@"^</?(b|i|u|h1|h2|p)>$|^<br\s*/?>$",RegexOptions.IgnoreCase));
            loading=true;refs.Clear();TextArea.Clear();
            if(source)TextArea.Text=original;
            else
            {
                FontStyle style=FontStyle.Regular;bool heading=false;Reference link=null;
                foreach(string token in Regex.Split(original,@"(<[^>]+>|\[link=[^\]]+\]|\[/link\]|\[image=[^\]]+\])"))
                {
                    if(token.StartsWith("[link=")){link=new Reference{Start=TextArea.TextLength,Key=token.Substring(6,token.Length-7)};continue;}
                    if(token=="[/link]"){if(link!=null){link.Length=TextArea.TextLength-link.Start;refs.Add(link);link=null;}continue;}
                    if(token.StartsWith("[image=")){string key=token.Substring(7,token.Length-8);var img=new Reference{Start=TextArea.TextLength,Key=key,Image=true};TextArea.SelectionFont=EditorTheme.Font(10,FontStyle.Bold);TextArea.SelectionColor=EditorTheme.Amber;TextArea.AppendText("[Image · "+key+"]");img.Length=TextArea.TextLength-img.Start;refs.Add(img);continue;}
                    if(token.StartsWith("<"))
                    {switch(token.ToLowerInvariant()){case "<b>":style|=FontStyle.Bold;break;case "</b>":style&=~FontStyle.Bold;break;case "<i>":style|=FontStyle.Italic;break;case "</i>":style&=~FontStyle.Italic;break;case "<u>":style|=FontStyle.Underline;break;case "</u>":style&=~FontStyle.Underline;break;case "<h1>":case "<h2>":heading=true;break;case "</h1>":case "</h2>":heading=false;break;case "<br>":case "<br/>":case "<br />":TextArea.AppendText("\n");break;case "</p>":TextArea.AppendText("\n");break;}continue;}
                    TextArea.SelectionFont=EditorTheme.Font(heading?16:11,style);TextArea.SelectionColor=link==null?EditorTheme.Text:EditorTheme.Amber;TextArea.AppendText(token);
                }
            }
            previous=TextArea.Text;TextArea.Select(0,0);loading=false;changed=false;
            help.Text=source?"Source mode preserves existing native markup. Formatting buttons wrap the selected text.":"Format selected text. Gold links and image placeholders retain their destinations; use Player preview to view illustrations.";
        }
        void OnTextEdited()
        {
            if(loading)return;string now=TextArea.Text;int start=0;while(start<previous.Length&&start<now.Length&&previous[start]==now[start])start++;
            int endOld=previous.Length,endNew=now.Length;while(endOld>start&&endNew>start&&previous[endOld-1]==now[endNew-1]){endOld--;endNew--;}
            int delta=now.Length-previous.Length;
            foreach(var r in refs.ToArray())
            {
                int end=r.Start+r.Length;
                if(end<=start)continue;
                if(r.Start>=endOld){r.Start+=delta;continue;}
                if(start<=r.Start&&endOld>=end){refs.Remove(r);continue;}
                if(r.Image){refs.Remove(r);continue;}
                int begin=Math.Min(r.Start,start);r.Length=Math.Max(0,end+delta-begin);r.Start=begin;
            }
            previous=now;Mark();
        }
        void Mark(){if(loading)return;changed=true;if(Changed!=null)Changed();}
        void Wrap(string before,string after){string selection=TextArea.SelectedText;TextArea.SelectedText=before+selection+after;Mark();}
        void Style(FontStyle flag){if(source){string tag=flag==FontStyle.Bold?"b":flag==FontStyle.Italic?"i":"u";Wrap("<"+tag+">","</"+tag+">");return;}var f=TextArea.SelectionFont??TextArea.Font;TextArea.SelectionFont=new Font(f,f.Style^flag);Mark();}
        void SetFontSize(float value){if(source){Wrap("<h1>","</h1>");return;}TextArea.SelectionFont=EditorTheme.Font(value,FontStyle.Bold);Mark();}
        void List(bool numbered){var lines=TextArea.SelectedText.Split('\n');TextArea.SelectedText=String.Join("\n",lines.Select((x,i)=>(numbered?(i+1)+". ":"• ")+x));Mark();}
        public string SelectedText{get{return TextArea.SelectedText;}}
        public void InsertLink(string key,string label){Insert(key,label,false);}
        public void InsertImage(string key){Insert(key,"[Image · "+key+"]",true);}
        void Insert(string key,string label,bool image)
        {
            if(source){TextArea.SelectedText=image?"[image="+key+"]":"[link="+key+"]"+label+"[/link]";return;}
            int start=TextArea.SelectionStart;int end=start+TextArea.SelectionLength;refs.RemoveAll(r=>r.Start<end&&r.Start+r.Length>start);TextArea.SelectedText=label;refs.Add(new Reference{Start=start,Length=label.Length,Key=key,Image=image});TextArea.Select(start,label.Length);TextArea.SelectionColor=EditorTheme.Amber;TextArea.Select(start+label.Length,0);TextArea.SelectionColor=EditorTheme.Text;Mark();
        }
        public string Read()
        {
            if(!changed)return original;if(source)return TextArea.Text;
            int start=TextArea.SelectionStart,length=TextArea.SelectionLength;var output=new StringBuilder();var text=TextArea.Text;
            int at=0;
            while(at<text.Length)
            {
                var r=refs.FirstOrDefault(x=>x.Start==at);
                if(r!=null&&r.Image){output.Append("[image="+r.Key+"]");at+=r.Length;continue;}
                if(r!=null)output.Append("[link="+r.Key+"]");
                int until=r!=null?Math.Min(text.Length,at+r.Length):refs.Where(x=>x.Start>at).Select(x=>x.Start).DefaultIfEmpty(text.Length).Min();
                while(at<until)
                {
                    TextArea.Select(at,1);var font=TextArea.SelectionFont??TextArea.Font;FontStyle style=font.Style;bool heading=font.Size>13;int end=at+1;
                    while(end<until){TextArea.Select(end,1);var next=TextArea.SelectionFont??TextArea.Font;if(next.Style!=style||(next.Size>13)!=heading)break;end++;}
                    if(heading)output.Append("<h1>");if((style&FontStyle.Bold)!=0)output.Append("<b>");if((style&FontStyle.Italic)!=0)output.Append("<i>");if((style&FontStyle.Underline)!=0)output.Append("<u>");
                    output.Append(text.Substring(at,end-at));
                    if((style&FontStyle.Underline)!=0)output.Append("</u>");if((style&FontStyle.Italic)!=0)output.Append("</i>");if((style&FontStyle.Bold)!=0)output.Append("</b>");if(heading)output.Append("</h1>");at=end;
                }
                if(r!=null)output.Append("[/link]");
            }
            TextArea.Select(start,length);return output.ToString();
        }
    }
}
