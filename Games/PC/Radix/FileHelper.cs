using System.Text;

namespace ExtractCLUT.Games.PC.Radix
{
    public static class FileHelper
    {
        public static byte[] DecompressRadixBitmap(byte[] compressedData, int width, int height)
        {
            var decompressedData = new byte[width * height];
            
            // data starts with height * 4 bytes, 
            // each 4 bytes = ushort offset, one byte repeat count, one byte pixel count to copy and repeat,
            // each offset points to a line in the decompressed bitmap, and the repeat count and pixel count are used to decompress the data for that line
            // if pixel and repeat count is 0, the line is transparent, otherwise copy pixel count bytes from compressed data and repeat them repeat count times 
            // followed by the actual pixel data

            var lineOffsetAndCommands = new List<(ushort offset, byte startPixel, byte pixelCount)>();
            for (int i = 0; i < height; i++)
            {
                var offset = BitConverter.ToUInt16(compressedData, i * 4);
                var startPixel = compressedData[i * 4 + 2];
                var pixelCount = compressedData[i * 4 + 3];
                lineOffsetAndCommands.Add((offset, startPixel, pixelCount));
            }

            for (int i = 0; i < height; i++)
            {
                var (offset, startPixel, pixelCount) = lineOffsetAndCommands[i];
                if (pixelCount > 0)
                {
                    var pixelData = new byte[pixelCount];
                    Array.Copy(compressedData, offset, pixelData, 0, pixelCount);
                    // insert pixel data into decompressedData at the correct position
                    Array.Copy(pixelData, 0, decompressedData, i * width + startPixel, pixelCount);
                }
            }
            
            return decompressedData;
        }
        
        public static void ExtractRadixDatFile(string datPath, string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            using var datReader = new BinaryReader(File.OpenRead(datPath));
            datReader.BaseStream.Seek(0x11, SeekOrigin.Begin);
            var fileCount = datReader.ReadUInt32();
            var fileTableOffset = datReader.ReadUInt32();
            datReader.BaseStream.Seek(fileTableOffset, SeekOrigin.Begin);
            var fileEntries = new List<DatEntry>();
            for (int i = 0; i < fileCount; i++)
            {
                var nameBytes = datReader.ReadBytes(32);
                var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                var offset = datReader.ReadUInt32();
                var size = datReader.ReadUInt32();
                var unknown1 = datReader.ReadInt16();
                var flags = datReader.ReadUInt32();
                fileEntries.Add(new DatEntry
                {
                    Name = name,
                    Offset = offset,
                    Size = size,
                    Unknown1 = unknown1,
                    Flags = flags
                });
            }

            foreach (var entry in fileEntries)
            {
                datReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                var data = datReader.ReadBytes((int)entry.Size);
                var outputFilePath = Path.Combine(outputDir, entry.Name);
                File.WriteAllBytes(outputFilePath, data);
            }
        }
    }

    public class DatEntry
    {
        public string Name { get; set; } // 32 bytes
        public uint Offset { get; set; }
        public uint Size { get; set; }
        public short Unknown1 { get; set; }
        public uint Flags { get; set; }
    }
}
