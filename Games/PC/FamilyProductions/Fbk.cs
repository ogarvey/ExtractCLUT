using System;
using System.Collections.Generic;
using System.IO;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.FamilyProductions
{
    public class FbkEntry
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Pixels { get; set; } = Array.Empty<byte>();
    }

    public class FbkFile
    {
        private const int HeaderSize = 0x1E;
        private const int ImageWidth = 320;
        private const int ImageHeight = 200;

        public List<FbkEntry> Entries { get; } = new List<FbkEntry>();

        public static FbkFile Load(string path)
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < HeaderSize)
                throw new InvalidDataException($"FBK file too small: {path}");

            var pixels = new byte[ImageWidth * ImageHeight];
            int sourceOffset = HeaderSize;

            for (int y = 0; y < ImageHeight; y++)
            {
                int rowOffset = y * ImageWidth;
                int rowPixels = 0;

                while (rowPixels < ImageWidth)
                {
                    if (sourceOffset >= data.Length)
                        throw new InvalidDataException($"FBK pixel data ended in row {y} of {path}");

                    byte control = data[sourceOffset++];
                    if ((control & 0xC0) == 0xC0)
                    {
                        int count = control & 0x3F;
                        if (sourceOffset >= data.Length)
                            throw new InvalidDataException($"FBK run is missing its value in row {y} of {path}");

                        byte value = data[sourceOffset++];
                        if (rowPixels + count > ImageWidth)
                            throw new InvalidDataException($"FBK run exceeds row {y} width in {path}");

                        for (int i = 0; i < count; i++)
                            pixels[rowOffset + rowPixels++] = value;
                    }
                    else
                    {
                        pixels[rowOffset + rowPixels++] = control;
                    }
                }
            }

            if (sourceOffset != data.Length)
                throw new InvalidDataException($"FBK file contains {data.Length - sourceOffset} trailing bytes: {path}");

            var file = new FbkFile();
            file.Entries.Add(new FbkEntry
            {
                Width = ImageWidth,
                Height = ImageHeight,
                Pixels = pixels
            });
            return file;
        }

        public void SaveImages(string outputDirectory, List<SixLabors.ImageSharp.Color> palette, string fbkFilePath)
        {
            Directory.CreateDirectory(outputDirectory);
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                using var image = new Image<Rgba32>(entry.Width, entry.Height);
                for (int y = 0; y < entry.Height; y++)
                {
                    for (int x = 0; x < entry.Width; x++)
                    {
                        int pixelIndex = y * entry.Width + x;
                        int paletteIndex = entry.Pixels[pixelIndex];
                        if (paletteIndex < palette.Count)
                            image[x, y] = (Rgba32)palette[paletteIndex];
                    }
                }

                var outputPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(fbkFilePath)}_{i:D4}.png");
                image.Save(outputPath);
            }
        }
    }
}
