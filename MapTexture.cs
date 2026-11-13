using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TvtLobby.MissionEditor
{
    public static class MapTexture
    {
        static readonly object DecodeGate=new object();
        public const int PreviewSize=4096;
        public const long CacheLimit=512L*1024*1024;
        public static void TrimCache(string directory,string keep,long limit)
        {
            var files=new DirectoryInfo(directory).GetFiles("*.png").OrderBy(f=>f.LastWriteTimeUtc).ToList();
            long bytes=files.Sum(f=>f.Length);
            foreach(var file in files){if(bytes<=limit)break;if(file.FullName.Equals(keep,StringComparison.OrdinalIgnoreCase))continue;try{long length=file.Length;file.Delete();bytes-=length;}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        public static string DecodeReply(MapReply reply)
        {
            if(File.Exists(reply.Background))return Decode(reply.Background);
            foreach(string candidate in reply.BackgroundCandidates)
            {
                try {return Decode(candidate);}
                catch(FileNotFoundException) { }
            }
            // Packed vanilla mounts have no absolute filename in Workbench. Resolve
            // their installed addon directory, not a machine-specific drive or asset copy.
            var roots=new System.Collections.Generic.List<string>();
            if(!String.IsNullOrEmpty(reply.AddonDirectory))roots.Add(reply.AddonDirectory.Trim('"'));
            if(!String.IsNullOrEmpty(reply.WorkbenchExecutable))
            {
                var workbench=new DirectoryInfo(Path.GetDirectoryName(reply.WorkbenchExecutable));
                if(workbench.Parent!=null&&workbench.Parent.Parent!=null)roots.Add(Path.Combine(workbench.Parent.Parent.FullName,"Arma Reforger","addons"));
            }
            using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1874880"))
            {
                string installed=key==null?null:key.GetValue("InstallLocation") as string;
                if(!String.IsNullOrEmpty(installed))roots.Add(Path.Combine(installed,"addons"));
            }
            string relative=(reply.BackgroundResource??"").Replace('/',Path.DirectorySeparatorChar);
            if(Path.IsPathRooted(relative)||relative.Split(Path.DirectorySeparatorChar).Contains(".."))throw new InvalidDataException("Invalid map resource path.");
            foreach(string root in roots)
            {
                if(!Directory.Exists(root))continue;
                foreach(string addon in Directory.GetDirectories(root))
                {
                    try{return Decode(Path.Combine(addon,relative));}catch(FileNotFoundException){ }
                }
            }
            throw new FileNotFoundException("The map texture was not found in the world's loaded addons.");
        }
        private static int Int(byte[] bytes,int offset) { return BitConverter.ToInt32(bytes,offset); }
        private static string Tag(byte[] bytes,int offset) {return Encoding.ASCII.GetString(bytes,offset,4);}
        public static string Decode(string source)
        {lock(DecodeGate)return DecodeLocked(source);}
        private static string DecodeLocked(string source)
        {
            if(String.IsNullOrEmpty(source)||Path.GetExtension(source).Equals(".png",StringComparison.OrdinalIgnoreCase))return source;
            byte[] encoded=MapArchive.Read(source);string digest;
            using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(encoded)).Replace("-","").ToLowerInvariant();
            string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"tvtLobby","MapCache");
            Directory.CreateDirectory(cache);string png=Path.Combine(cache,"v2-"+digest+".png");if(File.Exists(png)){File.SetLastWriteTimeUtc(png,DateTime.UtcNow);TrimCache(cache,png,CacheLimit);return png;}
            byte[] dds=Unpack(encoded);
            string converter=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"../vendor/DirectXTex/texconv.exe"));
            if(!File.Exists(converter))throw new FileNotFoundException("The bundled map texture converter is missing. Restore Tools/MissionEditor/vendor.",converter);
            string stage=Path.Combine(cache,"work-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
            string ddsPath=Path.Combine(stage,"preview.dds"),converted=Path.Combine(stage,"preview.png");
            var start=new ProcessStartInfo(converter,"-nologo -y -ft png -f R8G8B8A8_UNORM -m 1 -nogpu --single-proc -o \""+stage+"\" \""+ddsPath+"\"")
                {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
            try
            {
                File.WriteAllBytes(ddsPath,dds);
                using(var process=Process.Start(start))
                {
                    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                    if(!process.WaitForExit(30000)){process.Kill();throw new IOException("Map texture conversion timed out.");}
                    if(process.ExitCode!=0||!File.Exists(converted))throw new IOException("Map texture conversion failed: "+stdout.Result+stderr.Result);
                }
                File.Move(converted,png);
            }
            finally {File.Delete(ddsPath);File.Delete(converted);Directory.Delete(stage);}
            TrimCache(cache,png,CacheLimit);
            return png;
        }
        // EDDS retains the DDS header but stores COPY/LZ4 mip chunks in reverse order.
        private static int MipBytes(byte[] data,int width,int height)
        {
            string tag=Tag(data,84);int block=0,bpp=0;
            if(tag=="DXT1"||tag=="ATI1"||tag=="BC4U")block=8;
            else if(tag=="DXT3"||tag=="DXT5"||tag=="ATI2"||tag=="BC5U")block=16;
            else if(tag=="DX10")
            {
                int format=Int(data,128);
                if(format>=70&&format<=72||format>=79&&format<=81)block=8;
                else if(format>=73&&format<=78||format>=82&&format<=84||format>=94&&format<=99)block=16;
                else if(format==28||format==29||format==87||format==91)bpp=4;
                else throw new InvalidDataException("Unsupported map pixel format: "+format);
                if(Int(data,132)!=3||Int(data,140)!=1||(Int(data,136)&4)!=0)throw new InvalidDataException("Map texture must be a single 2D image.");
            }
            else {int bits=Int(data,88);if(bits!=8&&bits!=16&&bits!=24&&bits!=32)throw new InvalidDataException("Unsupported map pixel format.");bpp=bits/8;}
            return checked(block>0?Math.Max(1,(width+3)/4)*Math.Max(1,(height+3)/4)*block:width*height*bpp);
        }
        public static byte[] Unpack(byte[] data)
        {
            if(data.Length<128||Tag(data,0)!="DDS "||Int(data,4)!=124)throw new InvalidDataException("Not a DDS/EDDS map texture.");
            int height=Int(data,12),width=Int(data,16),mips=Math.Max(1,Int(data,28));
            if(width<1||height<1||width>16384||height>16384||mips>16)throw new InvalidDataException("Unsupported map texture dimensions.");
            int header=Tag(data,84)=="DX10"?148:128;
            if(data.Length<header+8)throw new InvalidDataException("Truncated texture header.");
            int level=0;
            while(Math.Max(width,height)>PreviewSize&&level+1<mips){width=Math.Max(1,width/2);height=Math.Max(1,height/2);level++;}
            if(Math.Max(width,height)>PreviewSize)throw new InvalidDataException("Map texture has no mip at 4096 pixels or below. Regenerate its mipmaps in Resource Browser.");
            int expected=MipBytes(data,width,height);byte[] raw=null;
            if(Tag(data,header)=="COPY"||Tag(data,header)=="LZ4 ")
            {
                int cursor=checked(header+8*mips);
                for(int mip=0;mip<mips;mip++)
                {
                    if(header+8*mip+8>data.Length)throw new InvalidDataException("Truncated mip table.");
                    int size=Int(data,header+8*mip+4);string tag=Tag(data,header+8*mip);
                    if(size<=0||size>data.Length-cursor)throw new InvalidDataException("Invalid mip size.");
                    if(mip==mips-1-level)
                    {
                        if(tag=="COPY"){if(size!=expected)throw new InvalidDataException("Incorrect mip payload length.");raw=new byte[size];Buffer.BlockCopy(data,cursor,raw,0,size);}
                        else if(tag=="LZ4 ")raw=DecodeLz4(data,cursor,size,expected);
                        else throw new InvalidDataException("Unsupported EDDS chunk: "+tag);
                    }
                    cursor+=size;
                }
                if(cursor!=data.Length||raw==null)throw new InvalidDataException("Incomplete EDDS mip payload.");
            }
            else
            {
                int offset=header,w=Int(data,16),h=Int(data,12);
                for(int mip=0;mip<level;mip++){offset=checked(offset+MipBytes(data,w,h));w=Math.Max(1,w/2);h=Math.Max(1,h/2);}
                if(expected>data.Length-offset)throw new InvalidDataException("Truncated DDS mip payload.");
                raw=new byte[expected];Buffer.BlockCopy(data,offset,raw,0,expected);
            }
            byte[] result=new byte[header+raw.Length];Buffer.BlockCopy(data,0,result,0,header);Buffer.BlockCopy(raw,0,result,header,raw.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(1),0,result,28,4);Buffer.BlockCopy(BitConverter.GetBytes(width),0,result,16,4);Buffer.BlockCopy(BitConverter.GetBytes(height),0,result,12,4);
            Buffer.BlockCopy(BitConverter.GetBytes((Int(data,8)&8)!=0?expected/height:expected),0,result,20,4);
            if(header==148)
            {
                int format=Int(result,128);
                if(format==99)format=98;else if(format==78)format=77;else if(format==72)format=71;else if(format==91)format=87;
                Buffer.BlockCopy(BitConverter.GetBytes(format),0,result,128,4);
            }
            return result;
        }
        private static byte[] DecodeLz4(byte[] data,int offset,int size,int limit)
        {
            if(size<4)throw new InvalidDataException("Truncated LZ4 header.");
            int expected=Int(data,offset);if(expected<=0||expected!=limit||expected>64*1024*1024)throw new InvalidDataException("Invalid decompressed mip size.");
            byte[] output=new byte[expected];int pos=offset+4,end=offset+size,written=0;
            while(pos<end)
            {
                if(end-pos<4)throw new InvalidDataException("Truncated LZ4 block.");
                int length=Int(data,pos)&0x7fffffff;pos+=4;if(length<=0||length>end-pos)throw new InvalidDataException("Invalid LZ4 block size.");
                int stop=pos+length;
                while(pos<stop)
                {
                    int token=data[pos++],literal=token>>4;
                    if(literal==15){int n;do{if(pos>=stop)throw new InvalidDataException("Truncated LZ4 literal.");n=data[pos++];literal+=n;}while(n==255);}
                    if(literal>stop-pos||literal>output.Length-written)throw new InvalidDataException("LZ4 literal exceeds bounds.");
                    Buffer.BlockCopy(data,pos,output,written,literal);pos+=literal;written+=literal;if(pos==stop)break;
                    if(stop-pos<2)throw new InvalidDataException("Truncated LZ4 match.");
                    int distance=data[pos]|(data[pos+1]<<8);pos+=2;int match=(token&15)+4;
                    if((token&15)==15){int n;do{if(pos>=stop)throw new InvalidDataException("Truncated LZ4 length.");n=data[pos++];match+=n;}while(n==255);}
                    if(distance<1||distance>written||match>output.Length-written)throw new InvalidDataException("LZ4 match exceeds bounds.");
                    for(int i=0;i<match;i++){output[written]=output[written-distance];written++;}
                }
            }
            if(written!=expected)throw new InvalidDataException("Unexpected decompressed mip length.");return output;
        }
    }
}

