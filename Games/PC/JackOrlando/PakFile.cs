using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.JackOrlando
{
    public class PakFile
    {
        private readonly string _magic = "PAK\0";
        private BinaryReader _reader;
        public List<PakFileEntry> Entries { get; set; } = new List<PakFileEntry>();

        public PakFile(string filePath)
        {
            _reader = new BinaryReader(File.OpenRead(filePath));

            var magic = new string(_reader.ReadChars(4));
            if (magic != _magic)
            {
                throw new Exception("Not a valid PAK file.");
            }

            var fileCount = _reader.ReadUInt32();
            var firstFileOffset = _reader.ReadUInt32();

            _reader.BaseStream.Seek(0x10, SeekOrigin.Begin);
            for (int i = 0; i < fileCount; i++)
            {
                var offset = _reader.ReadUInt32();
                var size = _reader.ReadUInt32();
                _reader.ReadUInt32(); // name length, not needed
                var name = _reader.ReadNullTerminatedString();
                Entries.Add(new PakFileEntry(name, offset, size));
            }

            // sanity check
            if (Entries.Count > 0 && Entries[0].offset != firstFileOffset)
            {
                Console.WriteLine("Warning: First file offset does not match first entry offset.");
            }
        }

        public void ExtractAll(string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            foreach (var entry in Entries)
            {
                _reader.BaseStream.Seek(entry.offset, SeekOrigin.Begin);
                var data = _reader.ReadBytes((int)entry.size);
                var safeName = string.Join("_", entry.name.Split(Path.GetInvalidFileNameChars()));
                File.WriteAllBytes(Path.Combine(outputDirectory, safeName), data);
            }
        }
    }

    public record PakFileEntry(string name, uint offset, uint size);
}
