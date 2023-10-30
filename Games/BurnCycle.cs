using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games
{
    public class BurnCycle
    {
        
    }
}


// var input = @"C:\Dev\Projects\Gaming\Phaser\LaserLords\assets\tilemaps\planets\fornax_tp.png";
// var output = @"C:\Dev\Projects\Gaming\Phaser\LaserLords\assets\tilemaps\planets\fornax_tp_transparency.png";
// BitmapHelper.ReplaceColorWithTransparent(input, output, Color.FromArgb(255, 120, 40, 40));


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

