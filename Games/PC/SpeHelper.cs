using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;

namespace ExtractCLUT.Games.PC
{
    public static class SpeHelper
    {
        public static void ProcessSpeFile(string filePath, bool useAlpha=false)
        {
            var outputFolder = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + "_Extracted");
            var transparencyFolder = Path.Combine(outputFolder, "Transparency");
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(transparencyFolder);
            using var speReader = new BinaryReader(File.OpenRead(filePath));

            var magic = speReader.ReadNullTerminatedString();
            if (magic != "SPEC1.0")
                throw new InvalidDataException("Invalid SPE file.");
            var fileCount = speReader.ReadUInt16();
            Console.WriteLine($"Processing SPE file: {filePath} with {fileCount} entries.");

            var entries = new List<SpeEntry>();
            for (int i = 0; i < fileCount; i++)
            {
                var typeFlag = speReader.ReadByte();
                var nameLength = speReader.ReadByte();
                var nameBytes = speReader.ReadBytes(nameLength);
                var name = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                speReader.ReadByte();
                var size = speReader.ReadUInt32();
                var offset = speReader.ReadUInt32();
                var entry = new SpeEntry
                {
                    TypeFlag = typeFlag,
                    Name = name,
                    Size = size,
                    Offset = offset
                };
                entries.Add(entry);
            }

            var palEntry = entries.FirstOrDefault(e => e.TypeFlag == 2);
            var pal = new List<Color>();
            if (palEntry != null)
            {
                speReader.BaseStream.Seek(palEntry.Offset, SeekOrigin.Begin);
                var colorCount = speReader.ReadUInt16();
                var palData = speReader.ReadBytes(colorCount * 3);
                pal = ColorHelper.ConvertBytesToRgbIS(palData);
                Console.WriteLine($"Extracted palette with {pal.Count} colors from entry '{palEntry.Name}'.");
            }
            else
            {
                Console.WriteLine("No palette entry found in SPE file.");
            }

            var imageEntries = entries.Where(e => e.TypeFlag == 0x15 || e.TypeFlag == 0x6 || e.TypeFlag == 0x5 || e.TypeFlag == 0x4).ToList();

            foreach (var imgEntry in imageEntries)
            {
                speReader.BaseStream.Seek(imgEntry.Offset, SeekOrigin.Begin);
                var width = speReader.ReadUInt16();
                var height = speReader.ReadUInt16();
                var imgData = speReader.ReadBytes((int)(imgEntry.Size - 4));
                var image = ImageFormatHelper.GenerateIMClutImage(pal, imgData, width, height);
                var outputImagePath = Path.Combine(outputFolder, imgEntry.Name + ".png");
                image.Save(outputImagePath);
                if (useAlpha)
                {
                    var transparencyImage = ImageFormatHelper.GenerateIMClutImage(pal, imgData, width, height, true);
                    var transparencyOutputPath = Path.Combine(transparencyFolder, imgEntry.Name + ".png");
                    transparencyImage.Save(transparencyOutputPath);
                }
            }
        }
    }

    public class SpeEntry
    {
        public byte TypeFlag { get; set; }
        public string Name { get; set; } = string.Empty;
        public uint Size { get; set; }
        public uint Offset { get; set; }
    }
}
