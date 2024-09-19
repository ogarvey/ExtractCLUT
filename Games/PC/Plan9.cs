using System.Drawing;
using System.Drawing.Imaging;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public class Plan9
    {
        public static void ExtractAll()
        {

            var path = @"C:\Dev\Projects\Gaming\VGR\PC\Plan-9-from-Outer-Space_DOS_EN\plan-9-from-outer-space\plan9\PLAN9.GFX";
            var output = @"C:\Dev\Projects\Gaming\VGR\PC\Plan-9-from-Outer-Space_DOS_EN\plan-9-from-outer-space\plan9\output";
            var palette = new List<Color>();
            var data = File.ReadAllBytes(path);
            var index = 0;
            var imageCount = 0;
            while (index < data.Length)
            {
                //if (imageCount == 295) Debugger.Break();
                if (imageCount < 3 || imageCount == 77 || imageCount == 95 || imageCount == 97 ||
                  imageCount == 99 || imageCount == 101 || imageCount == 118 || imageCount == 119 || imageCount == 249 ||
                  imageCount == 252 || imageCount == 291 || imageCount == 293 || imageCount == 294 ||
                  imageCount == 296)
                {
                    var colourCount = BitConverter.ToUInt16(data.Skip(index).Take(2).ToArray(), 0);
                    Console.WriteLine($"Colour Count: {colourCount}");
                    var paletteData = data.Skip(index + 2).Take(colourCount * 3).ToArray();
                    palette = ColorHelper.ConvertBytesToRGB(paletteData, 4);
                    index += 2 + (colourCount * 3);
                }

                var imageDataLength = BitConverter.ToUInt32(data.Skip(index).Take(4).ToArray(), 0);
                index += 4;
                Console.WriteLine($"Image Data Length: {imageDataLength}");
                var height = BitConverter.ToUInt16(data.Skip(index).Take(2).ToArray(), 0);
                var width = BitConverter.ToUInt16(data.Skip(index + 2).Take(2).ToArray(), 0);
                index += 4;

                Console.WriteLine($"Width: {width}, Height: {height}");
                Console.WriteLine($"Index: {index}");

                var imageData = data.Skip(index).Take((int)imageDataLength).ToArray();
                var image = ImageFormatHelper.GenerateClutImage(palette, imageData, width, height, (imageCount > 2 && imageCount != 119));
                image.Save(Path.Combine(output, $"{imageCount}.png"), ImageFormat.Png);
                imageCount++;
                index += (int)imageDataLength;
            }

        }
    }
}
