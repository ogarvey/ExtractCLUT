using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Model;

namespace ExtractCLUT.Games
{
    public static class BurnCycle
    {
        private const string MAIN_DATA_FILE = @"BurnCycle.rtr";
        private const string MAIN_OUTPUT_FOLDER = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\output";
        public static void ExtractIndividualSectors(List<SectorInfo> sectors)
        {
            var outputFolder = Path.Combine(MAIN_OUTPUT_FOLDER, "individual-sectors");
            Directory.CreateDirectory(outputFolder);
            FileHelpers.WriteIndividualSectorsToFolder(sectors, outputFolder);
        }
    }
}

// var file = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\NewRecords\BurnCycle\eor\output\BurnCycle.rtr_eor_166873_392485296_1055.bin";

// var fileBytes = File.ReadAllBytes(file);
// var palette = File.ReadAllBytes(file).Skip(0x4a).Take(0x180).ToArray();
// var colors = ColorHelper.ConvertBytesToRGB(palette);

// var images = new List<Bitmap>();

// var initialBytes = fileBytes.Skip(0x478c).Take(0x6cf0).ToArray();
// var secondaryBytes = fileBytes.Skip(0xbd90).Take(0x9140).ToArray();
// var tertiaryBytes = fileBytes.Skip(0x157e4).Take(0x6cf0).ToArray();
// var quaternaryBytes = fileBytes.Skip(0x1cde8).ToArray();

// var combinedBytes = initialBytes.Concat(secondaryBytes).ToArray();
// combinedBytes = combinedBytes.Concat(tertiaryBytes).ToArray();
// combinedBytes = combinedBytes.Concat(quaternaryBytes).ToArray();

// ImageFormatHelper.Rle7_AllBytes(combinedBytes, colors, 384, images);

// using (var gifWriter = new GifWriter(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\NewRecords\BurnCycle\eor\output\output\BurnCycle.rtr_eor_1055_loop_250ms.bin.gif", 250, 0))
// {
//   foreach (var image in images)
//   {
//     gifWriter.WriteFrame(image);
//   }
// }


