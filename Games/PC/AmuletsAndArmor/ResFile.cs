using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.AmuletsAndArmor
{
    public class ResFile
    {
        private string _filePath;
        private List<ResFileEntry> _entries { get; set; } = new List<ResFileEntry>();
        public string FileName => Path.GetFileName(_filePath);
        public IReadOnlyList<ResFileEntry> Entries => _entries.AsReadOnly();

        public ResFile(string filePath)
        {
            _filePath = filePath;
        }

        public int PopulateEntries()
        {
            _entries.Clear();
            try
            {
                using var resReader = new BinaryReader(File.Open(_filePath, FileMode.Open, FileAccess.Read));
                // confirm magic == `Res!`
                var magic = Encoding.ASCII.GetString(resReader.ReadBytes(4));
                if (magic != "Res!")
                {
                    return 0;
                }
                var fileTableOffset = resReader.ReadUInt32();
                var fileTableSize = resReader.ReadUInt32();
                var entryCount = resReader.ReadUInt16();
                resReader.BaseStream.Seek(fileTableOffset, SeekOrigin.Begin);
                for (int i = 0; i < entryCount; i++)
                {
                    var entry = new ResFileEntry
                    {
                        Magic = resReader.ReadBytes(4),
                        Name = Encoding.ASCII.GetString(resReader.ReadBytes(14)).TrimEnd('\0'),
                        Offset = resReader.ReadUInt32(),
                        Size = resReader.ReadUInt32(),
                        Unknown1 = resReader.ReadInt16(),
                        TypeFlag = resReader.ReadByte(),
                        Unknown2 = resReader.ReadBytes(10)
                    };
                    _entries.Add(entry);
                }
                return _entries.Count;
            }
            catch
            {
                return 0;
            }
        }

        public ResFileEntry? GetEntryByName(string name)
        {
            return _entries.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public byte[] ExtractEntryData(ResFileEntry entry)
        {
            byte[] data = Array.Empty<byte>();
            try
            {
                using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(entry.Offset, SeekOrigin.Begin);
                    data = new byte[entry.Size];
                    fs.Read(data, 0, (int)entry.Size);
                }
                return data;
            }
            catch
            {
                return data;
            }
        }
    }
    
    public class ResFileEntry
    {
        public byte[] Magic { get; set; } = new byte[4];
        public string Name { get; set; } = string.Empty; // 14 bytes
        public uint Offset { get; set; }
        public uint Size { get; set; }
        public short Unknown1 { get; set; }
        public byte TypeFlag { get; set; }
        public byte[] Unknown2 { get; set; } = new byte[10];
    }
}
