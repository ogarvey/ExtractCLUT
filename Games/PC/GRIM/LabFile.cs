using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.GRIM
{
    public class LabFile
    {
        public string FileName { get; set; }
        public List<LabEntry> Entries { get; set; } = new List<LabEntry>();

        public LabFile(string fileName)
        {
            FileName = Path.GetFileNameWithoutExtension(fileName);

            using var reader = new BinaryReader(File.OpenRead(fileName));
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "LABN")
                throw new Exception("Invalid LAB file");

            reader.ReadInt32(); // Version

            var entryCount = reader.ReadUInt32();
            var stringTableSize = reader.ReadUInt32();
            reader.BaseStream.Seek(16 * (entryCount + 1), SeekOrigin.Begin);
            var stringTable = reader.ReadBytes((int)stringTableSize);
            var stReader = new BinaryReader(new MemoryStream(stringTable));
            reader.BaseStream.Seek(16, SeekOrigin.Begin); 
            for (int i = 0; i < entryCount; i++)
            {
                var nameOffset = reader.ReadInt32();
                stReader.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
                var name = stReader.ReadNullTerminatedString();
                var entry = new LabEntry
                {
                    Parent = this,
                    Offset = reader.ReadInt32(),
                    Length = reader.ReadInt32(),
                    Name = name.ToLower()
                };
                Entries.Add(entry);
                reader.ReadInt32(); // Unknown
            }
        }
    
    }

    public class LabEntry 
    {
        public LabFile Parent { get; set; }
        public string Name { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }
}
