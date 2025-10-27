using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC.LastRites
{
    public static class LifFile
    {
        public static void Extract(string filePath, string outputDir)
        {
            // Implementation for extracting .lif files
            using var lifReader = new BinaryReader(File.OpenRead(filePath));
            lifReader.BaseStream.Seek(0x50000, SeekOrigin.Begin); // skip fade tables etc
            var palData = lifReader.ReadBytes(0x300);
            var palette = ColorHelper.ConvertBytesToRGB(palData, true);

            var unknown = lifReader.ReadUInt32();
            var offsets = new List<uint>();
            for (int i = 0; i < 0x408 / 4; i++)
            {
                offsets.Add(lifReader.ReadUInt32());
            }

            for (int i = 0; i < offsets.Count - 1; i++)
            {
                var offset = offsets[i];
                lifReader.BaseStream.Seek(offset, SeekOrigin.Begin);
                var count = lifReader.ReadUInt32();
                var subOffsets = new List<uint>();
                for (int j = 0; j < count; j++)
                {
                    subOffsets.Add(lifReader.ReadUInt32());
                }
            }
        }
    }
}
