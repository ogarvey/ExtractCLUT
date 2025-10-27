using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class DarkSun
    {
        public static void ParseGffFile(string filePath)
        {
            using var br = new BinaryReader(File.OpenRead(filePath));
            br.ReadBytes(0xc);
            var tableOffset = br.ReadUInt32();
            br.BaseStream.Seek(tableOffset+8, SeekOrigin.Begin);
            var sectionCount = br.ReadUInt16();
            for (int i = 0; i < sectionCount; i++)
            {
                var sectionType = br.ReadChars(4);
                var count = br.ReadUInt32();
                switch (new string(sectionType))
                {
                    case "BMP ":
                        // Process bitmap section
                        break;
                    case "PAL ":
                        // Process palette section
                        break;
                    default:
                        // Unknown section type
                        break;
                }
            }
        }
    }
}
