using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC.Anvil
{
    public static class D3grExtractor
    {
        public static void ExtractD3grFiles(string inputFilePath, string outputDirectory, List<Color> defaultPalette, bool useTransparency)
        {
            using var dRreader = new BinaryReader(File.OpenRead(inputFilePath));
            var d3grFile = new D3grFile(dRreader);
            var offsets = new List<uint>();

            for (int i = 0; i < d3grFile.SubFileCount; i++)
            {
                offsets.Add(dRreader.ReadUInt32());
            }

            if (d3grFile.PaletteOffset != 0)
            {
                dRreader.BaseStream.Seek(d3grFile.PaletteOffset, SeekOrigin.Begin);
                var paletteSize = dRreader.ReadUInt16();
                var paletteReplaceIndex = dRreader.ReadByte();
                dRreader.ReadByte(); // Padding byte
                var palData = dRreader.ReadBytes(paletteSize * 3);

                if (paletteSize != 256)
                {
                    // use replacement index to modify defaultPalette
                    var replacementColors = ColorHelper.ConvertBytesToRGB(palData, true);
                    for (int i = 0; i < replacementColors.Count; i++)
                    {
                        var targetIndex = (paletteReplaceIndex + i) % 256;
                        if (targetIndex < defaultPalette.Count)
                        {
                            defaultPalette[targetIndex] = replacementColors[i];
                        }
                    }
                }
                else
                {
                    defaultPalette = ColorHelper.ConvertBytesToRGB(palData, true);
                }
            }

            if (offsets.Count > 0 && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            
            foreach (var (offset, index) in offsets.WithIndex())
            {
                dRreader.BaseStream.Seek(d3grFile.DataStartOffset + offset, SeekOrigin.Begin);
                var dataSize = dRreader.ReadUInt32() - 0x10; // Size includes the header
                var dataType = dRreader.ReadUInt32();
                if (dataSize == 0)
                {
                    //Console.WriteLine($"Warning: Data size is zero at index {index}, skipping.");
                    continue;
                }
                if (dataType <= 2)
                {
                    // uncompressed
                    var xOffset = dRreader.ReadInt16();
                    var yOffset = dRreader.ReadInt16();
                    var height = dRreader.ReadUInt16();
                    var width = dRreader.ReadUInt16();
                    var imageData = dRreader.ReadBytes((int)dataSize);
                    var image = ImageFormatHelper.GenerateClutImage(defaultPalette, imageData, width, height, useTransparency);
                    var imagePath = Path.Combine(outputDirectory, $"{index}_{xOffset}_{yOffset}.png");
                    image.Save(imagePath);
                }
                else if ((dataType & 4) != 0)
                {
                    var xOffset = dRreader.ReadInt16();
                    var yOffset = dRreader.ReadInt16();
                    var height = dRreader.ReadUInt16();
                    var width = dRreader.ReadUInt16();
                    var compressedData = dRreader.ReadBytes((int)dataSize);
                    var decompressedData = DfaDecompress.DecompressType3(new BinaryReader(new MemoryStream(compressedData)));
                    var image = ImageFormatHelper.GenerateClutImage(defaultPalette, decompressedData, width, height, useTransparency);
                    var imagePath = Path.Combine(outputDirectory, $"{index}_{xOffset}_{yOffset}.png");
                    image.Save(imagePath);
                }
                else
                {
                    throw new NotSupportedException($"Unknown data type {dataType} at index {index}, skipping.");
                }
            }
        }
    }

    public class D3grFile
    {
        public const string D3grMagic = "D3GR";
        public uint ResourceType { get; set; } // Not actually 100% on this one
        public uint DataStartOffset { get; set; }
        public uint PaletteOffset { get; set; }
        public int SubFileCount { get; set; }

        public D3grFile(BinaryReader reader)
        {
            // Read and validate magic number
            var magic = new string(reader.ReadChars(4));
            if (magic != D3grMagic)
            {
                throw new InvalidDataException("Not a valid D3GR file.");
            }

            // Read header fields
            ResourceType = reader.ReadUInt32();
            DataStartOffset = reader.ReadUInt32();
            PaletteOffset = reader.ReadUInt32();
            reader.ReadBytes(8); // Always null bytes in observed files
            SubFileCount = reader.ReadInt32();
        }
    }
}
