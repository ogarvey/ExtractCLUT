using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;

namespace ExtractCLUT.Games.PC.JurassicWar
{
    public static class FileHelper
    {
        public static void ExtractTrsFile(string trsFile, string palPath, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            using var trsReader = new BinaryReader(File.OpenRead(trsFile));
            trsReader.ReadUInt32();
            var slotCount = trsReader.ReadUInt32();
            var actualCount = trsReader.ReadUInt32();
            var dataStartOffset = trsReader.ReadUInt32();

            var palData = File.ReadAllBytes(palPath);
            var palette = ColorHelper.ConvertBytesToRgbIS(palData);

            var fileEntries = new List<TrsFileEntry>();
            trsReader.BaseStream.Seek(0x20, SeekOrigin.Begin);

            for (int i = 0; i < slotCount; i++)
            {
                var offset = trsReader.ReadUInt32() + dataStartOffset;
                var width = trsReader.ReadUInt16();
                var height = trsReader.ReadUInt16();
                var xOffset = trsReader.ReadInt16();
                var yOffset = trsReader.ReadInt16();
                trsReader.ReadUInt32(); // Skip unknown field

                if (width > 0 && height > 0)
                {
                    fileEntries.Add(new TrsFileEntry
                    {
                        Offset = offset,
                        Width = width,
                        Height = height,
                        XOffset = xOffset,
                        YOffset = yOffset
                    });
                }
            }

            foreach (var entry in fileEntries)
            {
                trsReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                var pixelData = trsReader.ReadBytes(entry.Width * entry.Height);
                var image = ImageFormatHelper.GenerateIMClutImage(palette, pixelData, entry.Width, entry.Height, true, [0]);
                var outputFilePath = Path.Combine(outputDir, $"image_{entry.Offset:X8}.png");
                image.SaveAsPng(outputFilePath);
            }
        }

        public class TrsFileEntry
        {
            public uint Offset { get; set; }
            public ushort Width { get; set; }
            public ushort Height { get; set; }
            public short XOffset { get; set; }
            public short YOffset { get; set; }
        }

        public static void ExtractTrcFile(string trcFile, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            using var trcReader = new BinaryReader(File.OpenRead(trcFile));
            trcReader.ReadUInt32();
            var slotCount = trcReader.ReadUInt32();
            var actualCount = trcReader.ReadUInt32();
            var dataStartOffset = trcReader.ReadUInt32();
            var fileEntries = new List<TrcFileEntry>();
            trcReader.BaseStream.Seek(0x20, SeekOrigin.Begin);

            for (int i = 0; i < slotCount; i++)
            {
                var nameBytes = trcReader.ReadBytes(12);
                var name = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                var offset = trcReader.ReadUInt32() + dataStartOffset;
                var size = trcReader.ReadUInt32();
                var compressedSize = trcReader.ReadUInt32();
                var flags = trcReader.ReadUInt32();
                trcReader.ReadUInt32(); // Skip unknown field

                if (!string.IsNullOrWhiteSpace(name))
                {
                    fileEntries.Add(new TrcFileEntry
                    {
                        Name = name,
                        Offset = offset,
                        Size = size,
                        CompressedSize = compressedSize,
                        Flags = flags
                    });
                }
            }

            foreach (var entry in fileEntries)
            {
                trcReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                var data = trcReader.ReadBytes((int)entry.CompressedSize);
                var outputFilePath = Path.Combine(outputDir, entry.Name);
                File.WriteAllBytes(outputFilePath, data);
            }
        }
    }

    public class TrcFileEntry
    {
        public string Name { get; set; } // 0xC
        public uint Offset { get; set; }
        public uint Size { get; set; }
        public uint CompressedSize { get; set; }
        public uint Flags { get; set; }
    }
}
