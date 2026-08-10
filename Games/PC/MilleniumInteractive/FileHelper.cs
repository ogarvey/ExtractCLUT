using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC.MilleniumInteractive
{
    public static class FileHelper
    {
        // lbmPath is the path to the LBM file containing the palette data
        public static void ExtractMchFile(string mchFile, string lbmPath, string outputDir)
        {
            var palData = File.ReadAllBytes(lbmPath).Skip(0x30).Take(768).ToArray();
            var palette = ColorHelper.ConvertBytesToRgbIS(palData, translate: false);
            Directory.CreateDirectory(outputDir);
            using var crnReader = new BinaryReader(File.OpenRead(mchFile));
            var count = crnReader.ReadUInt16();
            var offsets = new List<uint>();
            var widthsAndHeights = new List<(ushort width, ushort height)>();
            for (int i = 0; i < count; i++)
            {
                var height = crnReader.ReadUInt16();
                var width = crnReader.ReadUInt16();
                widthsAndHeights.Add((width, height));
                offsets.Add(crnReader.ReadUInt32());
            }

            for (int i = 0; i < count; i++)
            {
                var offset = offsets[i];

                crnReader.BaseStream.Seek(offset, SeekOrigin.Begin);

                var (height, width) = widthsAndHeights[i];

                var decompressedSize = width * height;
                var imageLines = new byte[height][];

                for (int h = 0; h < height; h++)
                {
                    imageLines[h] = new byte[width];
                }

                var firstPixel = crnReader.ReadByte();
                while (firstPixel != 0xFF)
                {
                    var lineIndex = crnReader.ReadByte();


                    var pixelCount = crnReader.ReadByte();
                    var pixels = crnReader.ReadBytes(pixelCount);
                    Array.Copy(pixels, 0, imageLines[lineIndex], firstPixel, pixels.Length);

                    firstPixel = crnReader.ReadByte();
                }

                var imageData = imageLines.SelectMany(line => line ?? new byte[width]).ToArray();
                var image = ImageFormatHelper.GenerateIMClutImage(
                  palette,
                  imageData,
                  width,
                  height,
                  useTransparency: true, [0]);
                // rotate the image 90 degrees clockwise
                //image.Mutate(x => x.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal));
                image.SaveAsPng(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(mchFile)}_{i:D3}.png"));
            }
        }
        // lbmPath is the path to the LBM file containing the palette data
        public static void ExtractCrnFile(string crnFile, string lbmPath, string outputDir)
        {
            var palData = File.ReadAllBytes(lbmPath).Skip(0x30).Take(768).ToArray();
            var palette = ColorHelper.ConvertBytesToRgbIS(palData, translate: false);
            Directory.CreateDirectory(outputDir);
            using var crnReader = new BinaryReader(File.OpenRead(crnFile));
            var count = crnReader.ReadUInt16();
            var offsets = new List<ushort>();
            for (int i = 0; i < count; i++)
            {
                offsets.Add(crnReader.ReadUInt16());
            }

            for (int i = 0; i < count; i++)
            {
                var offset = offsets[i];

                crnReader.BaseStream.Seek(offset, SeekOrigin.Begin);

                var height = crnReader.ReadByte();
                var width = crnReader.ReadByte();

                var decompressedSize = width * height;
                var imageLines = new byte[height][];

                for (int h = 0; h < height; h++)
                {
                    imageLines[h] = new byte[width];
                }

                var firstPixel = crnReader.ReadByte();
                while (firstPixel != 0xFF)
                {
                    var lineIndex = crnReader.ReadByte();


                    var pixelCount = crnReader.ReadByte();
                    var pixels = crnReader.ReadBytes(pixelCount);
                    Array.Copy(pixels, 0, imageLines[lineIndex], firstPixel, pixels.Length);

                    firstPixel = crnReader.ReadByte();
                }

                var imageData = imageLines.SelectMany(line => line ?? new byte[width]).ToArray();
                var image = ImageFormatHelper.GenerateIMClutImage(
                  palette,
                  imageData,
                  width,
                  height,
                  useTransparency: true, [0]);
                // rotate the image 90 degrees clockwise
                image.Mutate(x => x.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal));
                image.SaveAsPng(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(crnFile)}_{i:D3}.png"));
            }
        }
    }
}
