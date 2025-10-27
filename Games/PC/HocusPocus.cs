using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class HocusPocus
    {
        public static List<DatEntry> ParseCsv(string csvFilePath)
        {
            var entries = new List<DatEntry>();
            var lines = File.ReadAllLines(csvFilePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length >= 4)
                {
                    entries.Add(new DatEntry
                    {
                        Index = int.Parse(parts[0]),
                        Offset = int.Parse(parts[1]),
                        Length = int.Parse(parts[2]),
                        Name = parts[3]
                    });
                }
            }

            return entries;
        }

        public static void ExtractFiles(string datFilePath, List<DatEntry> entries, string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            using (var fileStream = new FileStream(datFilePath, FileMode.Open, FileAccess.Read))
            {
                foreach (var entry in entries)
                {
                    // Seek to the file's position in the DAT
                    fileStream.Seek(entry.Offset, SeekOrigin.Begin);

                    // Read the file data
                    var buffer = new byte[entry.Length];
                    fileStream.Read(buffer, 0, entry.Length);

                    // Write to output file
                    var outputPath = Path.Combine(outputDirectory, entry.Name);
                    File.WriteAllBytes(outputPath, buffer);
                }
            }
        }

        public static void ExtractAllFiles(string csvFilePath, string datFilePath, string outputDirectory)
        {
            var entries = ParseCsv(csvFilePath);
            ExtractFiles(datFilePath, entries, outputDirectory);
        }

        public static (int width, int height, byte[] data) ReadImgFileWithDimensions(string imgFilePath)
        {
            using (var fileStream = new FileStream(imgFilePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fileStream))
            {
                // Read header
                ushort iWidth4 = reader.ReadUInt16();
                ushort iHeight = reader.ReadUInt16();

                int actualWidth = iWidth4 * 4;
                byte[] imageData = ReadImgFileInternal(reader, iWidth4, iHeight);

                return (actualWidth, iHeight, imageData);
            }
        }

        private static byte[] ReadImgFileInternal(BinaryReader reader, ushort iWidth4, ushort iHeight)
        {
            int actualWidth = iWidth4 * 4;
            int blockSize = iWidth4 * iHeight;

            // Read the four interleaved blocks
            byte[] block0 = reader.ReadBytes(blockSize);
            byte[] block1 = reader.ReadBytes(blockSize);
            byte[] block2 = reader.ReadBytes(blockSize);
            byte[] block3 = reader.ReadBytes(blockSize);

            // Reconstruct the image data
            byte[] imageData = new byte[actualWidth * iHeight];

            for (int y = 0; y < iHeight; y++)
            {
                for (int x = 0; x < actualWidth; x++)
                {
                    int blockIndex = y * iWidth4 + (x / 4);
                    byte pixelValue;

                    switch (x % 4)
                    {
                        case 0: pixelValue = block0[blockIndex]; break;
                        case 1: pixelValue = block1[blockIndex]; break;
                        case 2: pixelValue = block2[blockIndex]; break;
                        case 3: pixelValue = block3[blockIndex]; break;
                        default: pixelValue = 0; break;
                    }

                    imageData[y * actualWidth + x] = pixelValue;
                }
            }

            return imageData;
        }
    }

    public class DatEntry
    {
        public int Index { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
