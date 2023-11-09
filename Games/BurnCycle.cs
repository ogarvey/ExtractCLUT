using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Model;

namespace ExtractCLUT.Games
{
    public static class BurnCycle
    {
        private const string MAIN_DATA_FILE = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\BurnCycle.rtr";
        private const string MAIN_OUTPUT_FOLDER = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\output";
        public static void ExtractIndividualSectors(List<SectorInfo> sectors)
        {
            var outputFolder = Path.Combine(MAIN_OUTPUT_FOLDER, "individual-sectors");
            Directory.CreateDirectory(outputFolder);
            FileHelpers.WriteIndividualSectorsToFolder(sectors, outputFolder);
        }

        static void Extract33()
        {
            // var file = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\NewRecords\BurnCycle\eor\output\BurnCycle.rtr_eor_7720_18157440_33.bin";

            // var fileBytes = File.ReadAllBytes(file);
            // var bytes =  fileBytes.Skip(0x478c).Take(0x244F).ToArray();
            // var bytes2 = fileBytes.Skip(0x74f0).Take(0x51b4).ToArray();
            // var bytes3 = fileBytes.Skip(0xcfb8).Take(0x3678).ToArray();
            // var bytes4 = fileBytes.Skip(0x10f44).Take(0x5ac8).ToArray();
            // var bytes5 = fileBytes.Skip(0x17320).Take(0x3f8c).ToArray();
            // var bytes6 = fileBytes.Skip(0x1bbc0).Take(0x6cf0).ToArray();
            // var bytes7 = fileBytes.Skip(0x231C4).Take(0x48a0).ToArray();
            // var bytes8 = fileBytes.Skip(0x28378).Take(0x1848).ToArray();
            // var bytes9 = fileBytes.Skip(0x29eb4).Take(0x3f8c).ToArray();
            // var bytes10 = fileBytes.Skip(0x2e754).Take(0x63dc).ToArray();
            // var bytes11 = fileBytes.Skip(0x35444).Take(0x3f8c).ToArray();
            // var bytes12 = fileBytes.Skip(0x39ce4).Take(0x63dc).ToArray();
            // var bytes13 = fileBytes.Skip(0x409d4).Take(0x51ac).ToArray();

            // var combined = bytes.Concat(bytes2).ToArray();
            // combined = combined.Concat(bytes3).ToArray();
            // combined = combined.Concat(bytes4).ToArray();
            // combined = combined.Concat(bytes5).ToArray();
            // combined = combined.Concat(bytes6).ToArray();
            // combined = combined.Concat(bytes7).ToArray();
            // combined = combined.Concat(bytes8).ToArray();
            // combined = combined.Concat(bytes9).ToArray();
            // combined = combined.Concat(bytes10).ToArray();
            // combined = combined.Concat(bytes11).ToArray();
            // combined = combined.Concat(bytes12).ToArray();
            // combined = combined.Concat(bytes13).ToArray();}
        }
    }
}

// var bytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\records-eor\BurnCycle\data\BurnCycle_d_4.bin");

// var images = new List<Bitmap>();


// var palette = ColorHelper.ConvertBytesToRGB(bytes.Skip(0x4c).Take(0x180).ToArray());

// var imageBytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\records\BurnCycle\video\BurnCycle_v_1_1_CLUT7_Normal_6.bin");

//var longImage = Rle7_AllBytes(imageBytes, palette, 384, images);

// for (int i = 0; i < longImage.Length; i += 92160)
// {
//   var image = ImageFormatHelper.GenerateRle7Image(palette, longImage.Skip(i).Take(92160).ToArray(), 384, 240);
//   image.Save(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\records\BurnCycle\video\output\proper_CLUT7_Normal_6_palette_" + i + ".png");
//   images.Add(image);
// }

// using (var gifWriter = new GifWriter(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\records\BurnCycle\video\output\BurnCycle_v_1_1_CLUT7_Normal_6.bin.gif", 100, -1))
// {
//   foreach (var image in images)
//   {
//     gifWriter.WriteFrame(image);
//   }
// }

