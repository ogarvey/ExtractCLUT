using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public class Fable
    {
        public void ExtractAll()
        {

            var inputDir = @"C:\Dev\Projects\Gaming\VGR\PC\FABLE\part1\Extracted";
            var palInput = @"C:\Dev\Projects\Gaming\VGR\PC\FABLE\INSTALL\Extracted\PALETTE.RAW";

            var inputFiles = Directory.GetFiles(inputDir, "*.*");

            foreach (var input in inputFiles)
            {
                ExtractSprites(input, palInput);
            }
        }
        static Bitmap RenderSprite(byte[] spriteData, List<Color> palette, int width, int height, bool binaryOutput = false, string? binaryOutputPath = null)
        {
            Bitmap image = new Bitmap(width, height);
            var binaryImage = new byte[width * height];
            int x = 0;
            int y = 0;

            for (int i = 0; i < spriteData.Length && y < height;)
            {
                byte b = spriteData[i++];

                if (b == 0x00)
                {
                    // Fill the remainder of the line with transparency and move to the next line
                    x = 0;
                    y++;
                    continue;
                }

                if (b >= 0x80)
                {
                    // Number of transparent pixels
                    int numTransparentPixels = b & 0x7F;
                    for (int j = 0; j < numTransparentPixels; j++)
                    {
                        if (x < width)
                        {
                            image.SetPixel(x, y, Color.Transparent);
                            if (binaryOutput) binaryImage[y * width + x] = 0x00;
                            x++;
                        }
                    }
                }
                else
                {
                    // Number of colored pixels
                    int numColoredPixels = b;
                    for (int j = 0; j < numColoredPixels; j++)
                    {
                        if (i < spriteData.Length && x < width)
                        {
                            byte paletteIndex = (byte)(spriteData[i++] & 0x7f);
                            if (paletteIndex < palette.Count)
                            {
                                image.SetPixel(x, y, palette[paletteIndex]);
                            }
                            else
                            {
                                image.SetPixel(x, y, Color.Transparent); // Invalid palette index, set to transparent
                            }
                            if (binaryOutput) binaryImage[y * width + x] = paletteIndex;
                            x++;
                        }
                    }
                }
            }
            if (binaryOutput && !string.IsNullOrEmpty(binaryOutputPath)) File.WriteAllBytes(binaryOutputPath, binaryImage);
            return image;
        }

        static void ExtractSprites(string spriteFile, string spritePal)
        {

            var testPalData = File.ReadAllBytes(spritePal);
            var testPalette = ColorHelper.ConvertBytesToRGB(testPalData, 4);

            var testData = File.ReadAllBytes(spriteFile);
            if (testData[0] != 0x53) return;
            var outputDir = Path.Combine(Path.GetDirectoryName(spriteFile), $"{Path.GetFileName(spriteFile)}_images");
            Directory.CreateDirectory(outputDir);
            var binOutputFolder = Path.Combine(Path.GetDirectoryName(spriteFile), $"{Path.GetFileName(spriteFile)}_bin_output");
            Directory.CreateDirectory(binOutputFolder);

            var offsets = new List<int>();
            var count = BitConverter.ToInt16(testData.Skip(0x14).Take(2).ToArray(), 0);
            for (int i = 0; i < count; i++)
            {
                var offset = BitConverter.ToInt32(testData.Skip(0x16 + (i * 4)).Take(4).ToArray(), 0);
                offsets.Add(offset);
            }

            foreach (var (offset, index) in offsets.WithIndex())
            {
                var nextOffset = offsets.IndexOf(offset) == offsets.Count - 1 ? testData.Length : offsets[offsets.IndexOf(offset) + 1];
                var bytes = testData.Skip(offset).Take(nextOffset - offset).ToArray();
                var width = BitConverter.ToInt16(bytes.Skip(4).Take(2).ToArray(), 0);
                var height = BitConverter.ToInt16(bytes.Skip(6).Take(2).ToArray(), 0);
                var imageBytes = bytes.Skip(8).ToArray();
                var binaryName = Path.Combine(binOutputFolder, $"hero_{index}.bin");
                var testImage = RenderSprite(imageBytes, testPalette, width, height, true, binaryName);
                testImage.Save(@$"{outputDir}\{Path.GetFileName(spriteFile)}_{index}.png", ImageFormat.Png);
            }

        }
    }
}
