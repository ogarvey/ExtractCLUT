using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC
{
    public static class DreamWeb
    {
        public static void ExtractRoomFile(string roomFile, string outputDir, string palFile)
        {
            var dwPalData = File.ReadAllBytes(palFile).Skip(0x30).Take(0x300).ToArray();
            var dwPal = ColorHelper.ConvertBytesToRgbIS(dwPalData);

            using var roomReader = new BinaryReader(File.OpenRead(roomFile));
            roomReader.ReadBytes(0x32); // header

            var lengths = new List<ushort>();
            for (int i = 0; i < 15; i++)
            {
                lengths.Add(roomReader.ReadUInt16());
            }

            roomReader.BaseStream.Seek(0x60, SeekOrigin.Begin);
            var backdropFlags = roomReader.ReadBytes(0xC0);
            var backDropBlocks = new List<byte[]>();
            var blocksDataLength = lengths[0] - 0xC0;
            for (int i = 0; i < blocksDataLength / 256; i++)
            {
                var blockData = roomReader.ReadBytes(256);
                backDropBlocks.Add(blockData);
                var blockImage = ImageFormatHelper.GenerateIMClutImage(dwPal, blockData, 16, 16, true);
                var outputPng = Path.Combine(outputDir, "Blocks", $"{i:D4}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPng)!);
                blockImage.Save(outputPng);
            }
            roomReader.BaseStream.Seek(0x60 + lengths[0], SeekOrigin.Begin);

            var mapData = roomReader.ReadBytes(lengths[1]);
            var mapLayout = new List<byte>();

            for (int y = 0; y < 60; ++y)
            {
                if ((y * 132) + 66 >= mapData.Length)
                    break;
                mapLayout.AddRange(mapData.Skip(y * 132).Take(66));
            }

            // each block is 16x16 pixels, so 66 blocks wide = 1056 pixels
            // and 60 blocks high = 960 pixels
            // Iterate through the map layout and build the full backdrop image
            var backdropImage = new Image<Rgb24>(66 * 16, 60 * 16);
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 66; x++)
                {
                    if (y * 66 + x >= mapLayout.Count)
                        continue;
                    var blockIndex = mapLayout[y * 66 + x];
                    if (blockIndex >= backDropBlocks.Count)
                        continue;
                    var blockData = backDropBlocks[blockIndex];
                    var blockImage = ImageFormatHelper.GenerateIMClutImage(dwPal, blockData, 16, 16, true);

                    backdropImage.Mutate(ctx => ctx.DrawImage(blockImage, new Point(x * 16, y * 16), 1f));
                }
            }

            var backdropOutputPng = Path.Combine(outputDir, "backdrop.png");
            Directory.CreateDirectory(Path.GetDirectoryName(backdropOutputPng)!);
            backdropImage.Save(backdropOutputPng);

            roomReader.BaseStream.Seek(0x60 + lengths[0] + lengths[1], SeekOrigin.Begin);
            // Segment 3 - Graphics

            var graphicsSeg1OutputDir = Path.Combine(outputDir, "GraphicsSegment1");
            Directory.CreateDirectory(graphicsSeg1OutputDir);
            try
            {
                ExtractGraphicsSegment(roomReader, lengths[2], graphicsSeg1OutputDir, palFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting graphics segment 1: {ex.Message}");
            }

            roomReader.BaseStream.Seek(0x60 + lengths[0] + lengths[1] + lengths[2] + lengths[3], SeekOrigin.Begin);
            
            var currentPos = roomReader.BaseStream.Position;
            // Segment 4,5,6 - Graphics
            var graphicsSeg2OutputDir = Path.Combine(outputDir, "GraphicsSegment2");
            Directory.CreateDirectory(graphicsSeg2OutputDir);
            try
            {
                ExtractGraphicsSegment(roomReader, lengths[4], graphicsSeg2OutputDir, palFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting graphics segment 2: {ex.Message}");
            }
            roomReader.BaseStream.Seek(currentPos + lengths[4], SeekOrigin.Begin);
            currentPos = roomReader.BaseStream.Position;

            var graphicsSeg3OutputDir = Path.Combine(outputDir, "GraphicsSegment3");
            Directory.CreateDirectory(graphicsSeg3OutputDir);
            try
            {
                ExtractGraphicsSegment(roomReader, lengths[5], graphicsSeg3OutputDir, palFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting graphics segment 3: {ex.Message}");
            }
            roomReader.BaseStream.Seek(currentPos + lengths[5], SeekOrigin.Begin);
            currentPos = roomReader.BaseStream.Position;

            var graphicsSeg4OutputDir = Path.Combine(outputDir, "GraphicsSegment4");
            Directory.CreateDirectory(graphicsSeg4OutputDir);
            try
            {
                ExtractGraphicsSegment(roomReader, lengths[6], graphicsSeg4OutputDir, palFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting graphics segment 4: {ex.Message}");
            }
            roomReader.BaseStream.Seek(currentPos + lengths[6], SeekOrigin.Begin);
            currentPos = roomReader.BaseStream.Position;

            // skip segment 7,8,9,10,11
            roomReader.ReadBytes(lengths[7]);
            roomReader.ReadBytes(lengths[8]);
            roomReader.ReadBytes(lengths[9]);
            roomReader.ReadBytes(lengths[10]);
            roomReader.ReadBytes(lengths[11]);

            // Segment 12 - More graphics
            var graphicsSeg5OutputDir = Path.Combine(outputDir, "GraphicsSegment5");
            Directory.CreateDirectory(graphicsSeg5OutputDir);
            try 
            {
                ExtractGraphicsSegment(roomReader, lengths[12], graphicsSeg5OutputDir, palFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting graphics segment 5: {ex.Message}");
            }
        }

        public static void ExtractGraphicsSegment(BinaryReader reader, int segmentLength, string outputDir, string palFile)
        {
            var dwPalData = File.ReadAllBytes(palFile).Skip(0x30).Take(0x300).ToArray();
            var dwPal = ColorHelper.ConvertBytesToRgbIS(dwPalData);

            var dimensionsWithOffset = new List<(byte Width, byte Height, int Offset, byte x, byte y)>();

            var segmentStartPos = reader.BaseStream.Position;
            while (reader.BaseStream.Position < segmentStartPos + 0x820)
            {
                var width = reader.ReadByte();
                var height = reader.ReadByte();
                var offset = reader.ReadUInt16();
                var x = reader.ReadByte();
                var y = reader.ReadByte();
                if (width == 0 && height == 0)
                    continue;
                dimensionsWithOffset.Add((width, height, offset, x, y));
            }
            reader.BaseStream.Seek(segmentStartPos + 0x820, SeekOrigin.Begin);
            var index = 0;
            foreach (var (width, height, offset, x, y) in dimensionsWithOffset)
            {
                if (offset + 0x820 >= segmentLength)
                    break;
                reader.BaseStream.Seek(segmentStartPos + offset + 0x820, SeekOrigin.Begin);
                var imageData = reader.ReadBytes(width * height);
                var image = ImageFormatHelper.GenerateIMClutImage(dwPal, imageData, width, height, true);
                var outputPng = Path.Combine(outputDir, $"{index:D4}_x{x}_y{y}.png");
                image.Save(outputPng);
                index++;
            }
        }


        public static void ExtractGFile(string gFile, string outputDir, string palFile)
        {
            var dwPalData = File.ReadAllBytes(palFile).Skip(0x30).Take(0x300).ToArray();
            var dwPal = ColorHelper.ConvertBytesToRgbIS(dwPalData);

            using var gReader = new BinaryReader(File.OpenRead(gFile));
            gReader.ReadBytes(0x32); // header

            var totalLength = gReader.ReadInt32();
            Console.WriteLine($"Total Length: {totalLength}");

            gReader.BaseStream.Seek(0x60, SeekOrigin.Begin);
            var dimensionsWithOffset = new List<(byte Width, byte Height, int Offset, byte x, byte y)>();

            while (gReader.BaseStream.Position < 0x880)
            {
                var width = gReader.ReadByte();
                var height = gReader.ReadByte();
                var offset = gReader.ReadUInt16();
                var x = gReader.ReadByte();
                var y = gReader.ReadByte();
                if (width == 0 && height == 0)
                    continue;
                dimensionsWithOffset.Add((width, height, offset, x, y));
            }

            gReader.BaseStream.Seek(0x880, SeekOrigin.Begin);

            Console.WriteLine($"Found {dimensionsWithOffset.Count} images. Current Pos={gReader.BaseStream.Position:X8}");

            for (int i = 0; i < dimensionsWithOffset.Count; i++)
            {
                var (width, height, offset, x, y) = dimensionsWithOffset[i];
                gReader.BaseStream.Seek(offset + 0x880, SeekOrigin.Begin);
                var imageData = gReader.ReadBytes(width * height);

                var image = ImageFormatHelper.GenerateIMClutImage(dwPal, imageData, width, height, true);
                var outputPng = Path.Combine(outputDir, $"{i:D4}_x{x}_y{y}.png");
                image.Save(outputPng);
            }
        }
    }
}
