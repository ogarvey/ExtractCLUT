using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC.HopkinsFBI
{
    public static class ResExtractor
    {
        public static void ExtractResources(string inputFile, string outputDirectory)
        {
            var palOutputFolder = Path.Combine(outputDirectory, "PALS");
            Directory.CreateDirectory(palOutputFolder);

            // find the .cat file with the same name as the inputFile
            var catFile = Path.ChangeExtension(inputFile, ".cat");
            if (!File.Exists(catFile))
            {
                throw new FileNotFoundException($"Category file not found: {catFile}");
            }

            // Create the output directory if it doesn't exist
            Directory.CreateDirectory(outputDirectory);

            // Extract resources from the .cat file
            var offsets = new List<(string name, uint offset)>();

            using var catReader = new BinaryReader(File.OpenRead(catFile));
            var filename = new string(catReader.ReadChars(15)).TrimEnd('\0');
            while (filename != "FINIS")
            {
                var offset = catReader.ReadUInt32();
                catReader.ReadUInt32(); // Skip the next 4 bytes
                offsets.Add((filename, offset));
                Console.WriteLine($"Found resource: {filename} at offset {offset:X8}");
                filename = new string(catReader.ReadChars(15)).TrimEnd('\0');
            }

            using var resReader = new BinaryReader(File.OpenRead(inputFile));
            foreach (var ((name, offset), index) in offsets.WithIndex())
            {
                resReader.BaseStream.Seek(offset, SeekOrigin.Begin);
                var nextOffset = (index < offsets.Count - 1) ? offsets[index + 1].offset : (uint)resReader.BaseStream.Length;
                var data = resReader.ReadBytes((int)(nextOffset - offset));
                if (name.EndsWith(".PCX", StringComparison.OrdinalIgnoreCase))
                {
                    if (data[0] != 0x0A || data[1] != 0x05 || data[2] != 0x01) continue;
                    var imageO = ImageFormatHelper.ConvertPCX(data);
                    imageO.Save(Path.Combine(outputDirectory, Path.ChangeExtension(name, ".png")));
                    var palData = data.Skip(data.Length - 768).ToArray();
                    File.WriteAllBytes(Path.Combine(palOutputFolder, Path.ChangeExtension(name, ".bin")), palData);
                }
                else
                {
                    File.WriteAllBytes(Path.Combine(outputDirectory, name), data);
                } 
            }
        }
    }
}
