using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Eradicator
{
    public static class RidbExtractor
    {
        public static void ExtractRidbFile(string filePath)
        {
            using var ridbReader = new BinaryReader(File.OpenRead(filePath));

            // check magic string is RIDB
            var magic = new string(ridbReader.ReadChars(4));
            if (magic != "RIDB")
            {
                throw new InvalidDataException("Invalid RIDB file");
            }

            var outputFolder = Path.Combine(Path.GetDirectoryName(filePath)!, $"{Path.GetFileNameWithoutExtension(filePath)}_extracted");
            Directory.CreateDirectory(outputFolder);

            var entries = new List<RidbEntry>();
            var entryCount = ridbReader.ReadUInt32();
            var tableOffset = ridbReader.ReadUInt32();

            ridbReader.BaseStream.Seek(tableOffset, SeekOrigin.Begin);

            for (int i = 0; i < entryCount; i++)
            {
                var entry = new RidbEntry
                {
                    Name = new string(ridbReader.ReadChars(12)).TrimEnd('\0'),
                    Offset = ridbReader.ReadUInt32(),
                    Size = ridbReader.ReadUInt32()
                };
                entries.Add(entry);
            }

            // now that we have the entries, we can save them to files
            foreach (var entry in entries)
            {
                ridbReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                var data = ridbReader.ReadBytes((int)entry.Size);
                var outputPath = Path.Combine(outputFolder, entry.Name);
                File.WriteAllBytes(outputPath, data);
            }
        }
    }

    public class RidbEntry
    {
        public string Name { get; set; } = string.Empty;
        public uint Offset { get; set; }
        public uint Size { get; set; }
    }
}
