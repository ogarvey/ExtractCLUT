using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC.Cybermage
{
    public static class SpriteHelper
    {
        private static readonly int SpriteSlots = 120;
        public static void ExtractSpriteFile(string spriteFilePath, string outputDir, string? palPath = null)
        {
            List<Color> palette = null;
            if (palPath != null)
            {
                var paletteData = File.ReadAllBytes(palPath);
                palette = ColorHelper.ConvertBytesToRgbIS(paletteData);
            }

            using var spriteReader = new BinaryReader(File.OpenRead(spriteFilePath));
            var count = spriteReader.ReadByte();
            spriteReader.ReadByte();

            var xyList = new List<(uint x, uint y)>();
            for (int i = 0; i < count; i++)
            {
                var x = spriteReader.ReadUInt32();
                var y = spriteReader.ReadUInt32();
                xyList.Add((x, y));
            }
            // Skip the empty slots
            spriteReader.BaseStream.Seek(2 + (SpriteSlots * 8), SeekOrigin.Begin);

            var offsetList = new List<uint>();
            for (int i = 0; i < count; i++)
            {
                var offset = spriteReader.ReadUInt32();
                offsetList.Add(offset);
            }

            for (int i = 0; i < count; i++)
            {
                var offset = offsetList[i];
                var (x, y) = xyList[i];
                var dataLength = (i + 1 < count) ? (int)(offsetList[i + 1] - offset) : (int)(spriteReader.BaseStream.Length - offset);
                spriteReader.BaseStream.Seek(offset, SeekOrigin.Begin);
                var spriteData = spriteReader.ReadBytes(dataLength);
                var decodedSprite = DecodeSprite(spriteData);
                var outputFilePath = Path.Combine(outputDir, $"sprite_{i:D4}_{decodedSprite.Width}x{decodedSprite.Height}_{x}_{y}.bin");
                if (palette != null)
                {
                    var image = ImageFormatHelper.GenerateIMClutImage(palette, decodedSprite.Data, (int)decodedSprite.Width, (int)decodedSprite.Height, true, [0]);
                    var imageOutputPath = Path.Combine(outputDir, $"sprite_{i:D4}_{decodedSprite.Width}x{decodedSprite.Height}_{x}_{y}.png");
                    // rotate the image 90 degrees clockwise
                    image.Mutate(x => x.Rotate(RotateMode.Rotate90));
                    image.SaveAsPng(imageOutputPath);
                }
                else
                {
                    File.WriteAllBytes(outputFilePath, decodedSprite.Data);
                }
            }
        }

        public static SpriteOutput DecodeSprite(byte[] spriteData)
        {
            using var memoryStream = new MemoryStream(spriteData);
            using var reader = new BinaryReader(memoryStream);

            var decodedData = new List<byte>();

            var lineOffsets = new List<uint>();

            var initialOffset = reader.ReadUInt32();
            lineOffsets.Add(initialOffset);
            var width = 0;

            while (reader.BaseStream.Position < initialOffset)
            {
                lineOffsets.Add(reader.ReadUInt32());
            }
            var height = lineOffsets.Count;

            for (int lineIndex = 0; lineIndex < lineOffsets.Count; lineIndex++)
            {
                var lineOffset = lineOffsets[lineIndex];
                var nextLineOffset = (lineIndex + 1 < lineOffsets.Count) ? lineOffsets[lineIndex + 1] : (uint)spriteData.Length;
                reader.BaseStream.Seek(lineOffset, SeekOrigin.Begin);
                var lineData = new List<byte>();
                while (reader.BaseStream.Position < nextLineOffset)
                {
                    int runLength = reader.ReadInt16();
                    if (runLength > 0)
                    {
                        // Read the pixel data for the run
                        //runLength = Math.Min(runLength, width - lineData.Count); // Ensure we don't exceed the width
                        var pixelData = reader.ReadBytes(runLength);
                        lineData.AddRange(pixelData);
                    }
                    else if (runLength < 0)
                    {
                        // Skip the specified number of pixels (transparent)
                        int skipLength = -runLength;
                        lineData.AddRange(new byte[skipLength]);
                    }
                }
                if (lineIndex == 0 && width == 0)
                {
                    width = lineData.Count;
                }
                decodedData.AddRange(lineData);
            }
            return new SpriteOutput
            {
                Width = (uint)width,
                Height = (uint)height,
                Data = decodedData.ToArray()
            };
        }
    }

    public class SpriteOutput
    {
        public uint Width { get; set; }
        public uint Height { get; set; }
        public byte[] Data { get; set; }
    }
}
