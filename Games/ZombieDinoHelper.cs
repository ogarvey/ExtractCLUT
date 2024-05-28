using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using OGLibCDi.Helpers;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using static ExtractCLUT.Helpers.FileHelpers;

namespace ExtractCLUT.Games
{
    public static class ZombieDinoHelper
    {
        private static List<Color> _clutPalette = new List<Color>();
        private static List<int> _sectorCounts = new List<int>();
        public static void ParseDataFile(string filePath)
        {
            var data = File.ReadAllBytes(filePath);
            // parse clut palette at 0x5a
            _clutPalette = ColorHelper.ReadClutBankPalettes(data.Skip(0x5a).ToArray(), 2);

            // parse sector counts at 0x46a
            data = data.Skip(0x46a).ToArray();
            var index = 0;
            while (data[index] != 0x00)
            {
                _sectorCounts.Add(data[index]);
                index++;
            }

            Console.WriteLine($"Found {_sectorCounts.Count} sectors");
        }

        public static List<Image> ParseAnimFile(string rl7InputFile, bool transparent = false)
        {
            var rl7Chunks = SplitBinaryFileintoSectors(rl7InputFile, 2324);
            
            var rl7Images = new List<Image>();
           
            for (int i = 0; i < _sectorCounts.Count; i++)
            {
                var rl7Chunk = rl7Chunks.Take(_sectorCounts[i]).ToList();
                var rl7Data = rl7Chunk.SelectMany(x => x).ToArray();
                rl7Chunks = rl7Chunks.Skip(_sectorCounts[i]).ToList();
                var rl7Image = ImageFormatHelper.GenerateRle7Image(_clutPalette, rl7Data, 384, 240, transparent);
                rl7Images.Add(rl7Image);
            }

            _clutPalette = new List<Color>();
            _sectorCounts = new List<int>();
            return rl7Images;
        }
    }
}
