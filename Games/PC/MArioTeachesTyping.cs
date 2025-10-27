using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public static class MArioTeachesTyping
    {
        public static void ExtractSprites()
        {
            var casPalFile = @"C:\Dev\Gaming\PC\Dos\Games\MARIOCD\MARIO\MPALLET.GPO";
            var casPalBytes = File.ReadAllBytes(casPalFile).Take(0x300).ToArray();
            var casPalette = ColorHelper.ConvertBytesToRGB(casPalBytes, true);

            var outPalFile = @"C:\Dev\Gaming\PC\Dos\Games\MARIOCD\MARIO\LPALLET.GPO";
            var outPalBytes = File.ReadAllBytes(outPalFile).Take(0x300).ToArray();
            var outPalette = ColorHelper.ConvertBytesToRGB(outPalBytes, true);

            var swmPalFile = @"C:\Dev\Gaming\PC\Dos\Games\MARIOCD\MARIO\PPALLET.GPO";
            var swmPalBytes = File.ReadAllBytes(swmPalFile).Take(0x300).ToArray();
            var swmPalette = ColorHelper.ConvertBytesToRGB(swmPalBytes, true);

            var aniFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Dos\Games\MARIOCD\MARIO", "*.ANI");
            var outputDir = Path.Combine(Path.GetDirectoryName(aniFiles[0])!, "output");
            Directory.CreateDirectory(outputDir);
            var pal = new List<Color>();
            foreach (var aniFile in aniFiles)
            {
                // select palette based on aniFile name
                if (Path.GetFileNameWithoutExtension(aniFile).StartsWith("L"))
                {
                    pal = outPalette;
                }
                else if (Path.GetFileNameWithoutExtension(aniFile).StartsWith("M"))
                {
                    pal = casPalette;
                }
                else
                {
                    pal = swmPalette;
                }
                var aniOutputDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(aniFile));
                Directory.CreateDirectory(aniOutputDir);

                using var br = new BinaryReader(File.OpenRead(aniFile));
                // read 8 bytes at a time, ignore first two bytes, next two bytes are some sort of id, remaining 4 bytes are the offset to the start of the data
                var idsAndOffsets = new List<(uint id, uint offset)>();
                br.ReadBytes(2); // skip first two bytes
                var id = br.ReadUInt16(); // read the id
                var offset = br.ReadUInt32(); // read the offset
                idsAndOffsets.Add((id, offset));
                while (id != 0 && offset != 0)
                {
                    br.ReadBytes(2); // skip first two bytes
                    id = br.ReadUInt16(); // read the id
                    offset = br.ReadUInt32(); // read the offset
                    idsAndOffsets.Add((id, offset));
                }
                Console.WriteLine($"Current position: {br.BaseStream.Position}");
                foreach (var ((sId, sOffset), sIndex) in idsAndOffsets.WithIndex())
                {
                    if (sId == 0 || sOffset == 0) break; // end of file marker
                    br.BaseStream.Position = sOffset;
                    br.ReadBytes(4); // skip first four bytes
                    var width = br.ReadUInt16(); // read the width
                    var height = br.ReadUInt16(); // read the height
                    var data = br.ReadBytes(width * height); // read the data
                    var image = ImageFormatHelper.GenerateClutImage(pal, data, width, height, true);
                    var outputFile = Path.Combine(aniOutputDir, $"{Path.GetFileNameWithoutExtension(aniFile)}_{sIndex}.png");
                    image.Save(outputFile, ImageFormat.Png);
                }
            }

        }

    }
}
