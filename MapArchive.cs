using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;

namespace TvtLobby.MissionEditor
{
    // Read just the requested resource from the addon selected by Workbench.
    // No extraction tree and no modification of installed game/addon archives.
    public static class MapArchive
    {
        sealed class Index { public long Length,Stamp;public Dictionary<string,long[]> Entries=new Dictionary<string,long[]>(StringComparer.OrdinalIgnoreCase); }
        static readonly Dictionary<string,Index> indexes=new Dictionary<string,Index>(StringComparer.OrdinalIgnoreCase);
        public static byte[] Read(string resourcePath)
        {
            var file=new FileInfo(resourcePath);
            if(file.Exists){if(file.Length>256L*1024*1024)throw new InvalidDataException("Map texture is too large.");return File.ReadAllBytes(resourcePath);}
            DirectoryInfo folder=file.Directory;
            for(int depth=0;folder!=null&&depth<12;depth++,folder=folder.Parent)
            {
                if(!folder.Exists)continue;
                string[] archives=Directory.GetFiles(folder.FullName,"*.pak",SearchOption.TopDirectoryOnly);
                if(archives.Length==0)continue;
                string relative=resourcePath.Substring(folder.FullName.Length+1).Replace('\\','/');
                foreach(string archive in archives){byte[] found=Find(archive,relative);if(found!=null)return found;}
                throw new FileNotFoundException("Terrain texture not found in its addon archives: "+relative);
            }
            throw new FileNotFoundException("Cannot locate the terrain map texture: "+resourcePath);
        }
        private static string Tag(BinaryReader reader){return Encoding.ASCII.GetString(reader.ReadBytes(4));}
        private static uint Big(BinaryReader reader){byte[] b=reader.ReadBytes(4);if(b.Length!=4)throw new EndOfStreamException();return ((uint)b[0]<<24)|((uint)b[1]<<16)|((uint)b[2]<<8)|b[3];}
        private static byte[] Find(string archive,string wanted)
        {lock(indexes)return FindLocked(archive,wanted);}
        private static byte[] FindLocked(string archive,string wanted)
        {
            using(var file=File.Open(archive,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            using(var reader=new BinaryReader(file,Encoding.UTF8))
            {
                Index cached;long stamp=File.GetLastWriteTimeUtc(archive).Ticks;
                if(!indexes.TryGetValue(archive,out cached)||cached.Length!=file.Length||cached.Stamp!=stamp)
                {
                cached=new Index{Length=file.Length,Stamp=stamp};
                if(Tag(reader)!="FORM"||Big(reader)+8L!=file.Length||Tag(reader)!="PAC1")throw new InvalidDataException("Unsupported PAK: "+archive);
                long index=0,indexEnd=0,dataStart=0,dataEnd=0;uint version=0;
                while(file.Position<file.Length)
                {
                    string tag=Tag(reader);uint size=Big(reader);long end=file.Position+size;if(end>file.Length)throw new InvalidDataException("Truncated PAK chunk.");
                    if(tag=="HEAD")version=reader.ReadUInt32();
                    else if(tag=="FILE"){index=file.Position;indexEnd=end;}
                    else if(tag=="DATA"){dataStart=file.Position;dataEnd=end;}
                    file.Position=end;
                }
                if(version!=0x10003||index==0||dataStart==0)throw new InvalidDataException("Unsupported PAK layout.");
                file.Position=index;Search(reader,"",0,indexEnd,dataStart,dataEnd,cached.Entries);indexes[archive]=cached;
                }
                long[] entry;if(!cached.Entries.TryGetValue(wanted,out entry))return null;
                file.Position=entry[0];byte[] packed=reader.ReadBytes((int)entry[1]);if(packed.Length!=entry[1])throw new EndOfStreamException();
                if(entry[3]==0){if(packed.Length!=entry[2])throw new InvalidDataException("PAK payload size mismatch.");return packed;}
                // PAK compressed entries use zlib: skip its two-byte header and four-byte Adler checksum.
                if(packed.Length<6||(packed[0]&15)!=8)throw new InvalidDataException("Invalid zlib payload.");
                using(var input=new MemoryStream(packed,2,packed.Length-6))
                using(var deflate=new DeflateStream(input,CompressionMode.Decompress))
                {
                    byte[] output=new byte[(int)entry[2]];int offset=0,read;
                    while(offset<output.Length&&(read=deflate.Read(output,offset,output.Length-offset))>0)offset+=read;
                    if(offset!=output.Length||deflate.ReadByte()!=-1)throw new InvalidDataException("Unexpected decompressed PAK size.");return output;
                }
            }
        }
        private static void Search(BinaryReader r,string parent,int depth,long end,long dataStart,long dataEnd,Dictionary<string,long[]> entries)
        {
            if(depth>100||r.BaseStream.Position+2>end)throw new InvalidDataException("Invalid PAK index.");
            int kind=r.ReadByte(),length=r.ReadByte();string name=Encoding.UTF8.GetString(r.ReadBytes(length));
            string path=String.IsNullOrEmpty(parent)?name:String.IsNullOrEmpty(name)?parent:parent+"/"+name;
            if(kind==0){uint count=r.ReadUInt32();if(count>1000000)throw new InvalidDataException("Invalid PAK entry count.");for(uint i=0;i<count;i++)Search(r,path,depth+1,end,dataStart,dataEnd,entries);}
            else if(kind==1)
            {
                uint offset=r.ReadUInt32(),packed=r.ReadUInt32(),unpacked=r.ReadUInt32();r.ReadUInt32();r.ReadUInt16();byte compressed=r.ReadByte();r.ReadByte();r.ReadUInt32();
                if(compressed<=1 && offset>=dataStart && offset+(long)packed<=dataEnd && packed<=256*1024*1024 && unpacked<=256*1024*1024)
                {
                    entries[path]=new long[]{offset,packed,unpacked,compressed};
                }
            }
            else throw new InvalidDataException("Invalid PAK entry kind.");
            if(r.BaseStream.Position>end)throw new InvalidDataException("PAK index exceeded its bounds.");
        }
    }
}
