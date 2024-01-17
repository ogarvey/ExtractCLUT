using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Image = System.Drawing.Image;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games
{
    public class ShaolinHelper
    {
        void GetAnimation()
        {
            var dyuvBytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SHAOLIN\Output\Temples\DYUV\temples.rtf_1_1_DYUV_Normal_Even_7229.bin");
            var files = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SHAOLIN\Output\Temples\RL7\9", "*.bin");
            var dyuvImage = ImageFormatHelper.DecodeDYUVImage(dyuvBytes, 384, 280);

            var images = new List<Image>();
            var tpImages = new List<Image>();
            foreach (var file in files)
            {
                var bytes = File.ReadAllBytes(file);
                var palBytes = bytes.Skip(0x04).Take(0x180).ToArray();
                var palette = ColorHelper.ConvertBytesToRGB(palBytes);
                //var image = GenerateRle7Image(palette, bytes.Skip(0x184).ToArray(), 384, 280, true);
                var transpImage = ImageFormatHelper.GenerateRle7Image(palette, bytes.Skip(0x184).ToArray(), 384, 280, true);
                //images.Add(image.Scale4());
                tpImages.Add(transpImage);
            }

            //CreateGifFromImageList(images, @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SHAOLIN\Output\Temples\RL7\6\gifs\shaolin6_x4.gif", 20);
            ImageFormatHelper.CreateGifFromImageList(tpImages, @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SHAOLIN\Output\Temples\RL7\9\gifs\shaolin9_bg.gif", 10, 1, dyuvImage);

        }
    }
}
