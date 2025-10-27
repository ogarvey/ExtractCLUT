using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.TLJ
{
    public class Xarc
    {
        public string FilePath { get; set; } = string.Empty;
        public List<XarcEntry> Entries { get; set; } = new();

        public Xarc(string filePath)
        {
            FilePath = filePath;
            using var xReader = new BinaryReader(File.OpenRead(filePath));
            // Read the XARC file header and entries
            if (xReader.ReadUInt32() != 0x00000001)
            {
                throw new InvalidDataException("Invalid XARC file.");
            }

            var count = xReader.ReadUInt32();
            var offset = xReader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var entry = new XarcEntry
                {
                    Offset = offset,
                    Name = xReader.ReadNullTerminatedString(),
                    Size = xReader.ReadInt32(),
                };
                Entries.Add(entry);
                offset += entry.Size;
                xReader.ReadInt32(); // padding
            }

            foreach (var entry in Entries)
            {
                xReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                entry.Data = xReader.ReadBytes(entry.Size);
            }
        }
    }

    public class XarcEntry
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string Name { get; set; } = string.Empty;
    }
}
