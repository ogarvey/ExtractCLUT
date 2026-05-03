using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.Abomination
{
    public class CltEntry
    {
        public string Path { get; set; } = ""; // 0x104 bytes, null-terminated ASCII
        public int Offset { get; set; }
        public int Length { get; set; }
    }
    public static class CltExtractor
    {
        public static int ExtractCltFile(string filepath, string outputFolder)
        {
            if (!File.Exists(filepath))
            {
                Console.WriteLine($"File not found: {filepath}");
                return 0;
            }

            using var cltReader = new BinaryReader(File.OpenRead(filepath));

            var magic = cltReader.ReadBytes(4);
            if (Encoding.ASCII.GetString(magic) != "AWAD")
            {
                Console.WriteLine($"Invalid CLT file: {filepath}");
                return 0;
            }

            var cltEntries = new List<CltEntry>();
            Directory.CreateDirectory(outputFolder);

            var fileCount = cltReader.ReadInt32();

            for (int i = 0; i < fileCount; i++)
            {
                var path = cltReader.ReadNullTerminatedString();
                // Skip padding to 0x104 bytes
                cltReader.BaseStream.Seek(0x104 - (path.Length + 1), SeekOrigin.Current);
                var entry = new CltEntry
                {
                    Path = path,
                    Length = cltReader.ReadInt32(),
                    Offset = cltReader.ReadInt32()
                };
                cltEntries.Add(entry);
            }

            if (filepath.Contains("sprites"))
            {
                var images = new List<Image<Rgba32>>();

                foreach (var entry in cltEntries.Where(e => e.Path.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)))
                {
                    cltReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                    cltReader.ReadInt32(); // null padding
                    var imageDataSize = cltReader.ReadInt32();
                    var width = cltReader.ReadInt16();
                    var height = cltReader.ReadInt16();
                    var bytesPerPixel = cltReader.ReadInt16();
                    var imageData = cltReader.ReadBytes(imageDataSize);
                    if (imageData.Length < width * height * bytesPerPixel)
                    {
                        Console.WriteLine($"Image data too small for entry {entry.Path}, skipping.");
                        continue;
                    }
                    var image = ImageFormatHelper.Decode16BitImage(imageData, 0, width, height);
                    images.Add(image);
                }

                if (images.Count == 0)
                {
                    Console.WriteLine($"No images extracted from {filepath}");
                    return 0;
                }

                for (int i = 0; i < images.Count; i++)
                {
                    string outputPath = Path.Combine(outputFolder, new string(cltEntries.Where(e => e.Path.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)).ToList()[i].Path.Skip(1).ToArray())); // remove leading slash
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    images[i].SaveAsPng(outputPath);
                    Console.WriteLine($"Saved image: {outputPath}");
                }

                foreach (var entry in cltEntries.Where(e => !e.Path.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)))
                {
                    cltReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                    var data = cltReader.ReadBytes(entry.Length);
                    var outputPath = Path.Combine(outputFolder, new string(entry.Path.Skip(1).ToArray())); // remove leading slash
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    File.WriteAllBytes(outputPath, data);
                    Console.WriteLine($"Extracted: {outputPath}");
                }

                return cltEntries.Count;
            }
            else if (filepath.Contains("levels"))
            {
                foreach (var (entry, ei) in cltEntries.WithIndex())
                {
                    cltReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                    var nextOffset = (ei < cltEntries.Count - 1) ? cltEntries[ei + 1].Offset : cltReader.BaseStream.Length;
                    var dataSize = (int)(nextOffset - entry.Offset);
                    var data = cltReader.ReadBytes(dataSize);
                    var outputPath = Path.Combine(outputFolder, new string(entry.Path.Skip(1).ToArray())); // remove leading slash
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    File.WriteAllBytes(outputPath, data);
                    Console.WriteLine($"Extracted: {outputPath}");
                }
                return cltEntries.Count;
            }
            else
            {
                foreach (var entry in cltEntries)
                {
                    cltReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                    var data = cltReader.ReadBytes(entry.Length);
                    var outputPath = Path.Combine(outputFolder, new string(entry.Path.Skip(1).ToArray())); // remove leading slash
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    File.WriteAllBytes(outputPath, data);
                    Console.WriteLine($"Extracted: {outputPath}");
                }
                return cltEntries.Count;
            }
        }
    }
}
