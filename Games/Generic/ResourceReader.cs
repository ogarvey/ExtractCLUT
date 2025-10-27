using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.Generic
{
    public static class ResourceReader
    {
        public static void ExtractResourceFile(string inputFile, string outputDir)
        {
            using var reader = new BinaryReader(File.OpenRead(inputFile));
            var fileCount = reader.ReadInt32();
            var offsets = new List<uint>();
            for (int i = 0; i < fileCount; i++)
            {
                offsets.Add(reader.ReadUInt32());
            }

            for (int i = 0; i < fileCount-1; i++)
            {
                reader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
                var nextOffset = offsets[i + 1];
                var length = nextOffset - offsets[i];
                var data = reader.ReadBytes((int)length);
                File.WriteAllBytes(Path.Combine(outputDir, $"{i:D3}.bin"), data);
            }
        }
    }
}
