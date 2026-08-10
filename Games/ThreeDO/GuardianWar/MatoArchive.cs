using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.ThreeDO.GuardianWar
{
    public static class MatoArchive
    {
        public static void ExtractArchiveV2(string filePath)
        {
            using var reader = new BinaryReader(File.OpenRead(filePath));
            var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic != "mato")
            {
                throw new InvalidDataException($"Invalid MATO archive magic: {magic}");
            }

            var totalSize = reader.ReadBigEndianUInt32();
            var matoCount = reader.ReadBigEndianUInt32();
            var byteSkip = (matoCount - 1) * 4;

            reader.BaseStream.Seek(0x20 + byteSkip, SeekOrigin.Begin);
            var tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (tag != "OBJC")
            {
                Console.WriteLine($"Invalid MATO archive tag: {tag}");
                return;
            }
            var chunkSize = reader.ReadBigEndianUInt32()-8;
            var chunkData = reader.ReadBytes((int)chunkSize);
            tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (tag != "OCOC")
            {
                Console.WriteLine($"Invalid MATO archive tag: {tag}");
                return;
            }
            chunkSize = reader.ReadBigEndianUInt32()-8;
            chunkData = reader.ReadBytes((int)chunkSize);
            tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (tag != "OGRC")
            {
                Console.WriteLine($"Invalid MATO archive tag: {tag}");
                return;
            }
            chunkSize = reader.ReadBigEndianUInt32() - 8;
            chunkData = reader.ReadBytes((int)chunkSize);
            tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (tag != "OPAC")
            {
                Console.WriteLine($"Invalid MATO archive tag: {tag}");
                return;
            }
            chunkSize = reader.ReadBigEndianUInt32() - 8;
            chunkData = reader.ReadBytes((int)chunkSize);
            tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (tag != "OPOC")
            {
                Console.WriteLine($"Invalid MATO archive tag: {tag}");
                return;
            }
            chunkSize = reader.ReadBigEndianUInt32()-4;
            chunkData = reader.ReadBytes((int)chunkSize);

            var ccbMatoSize = reader.ReadBigEndianUInt32();
            var ccbMatoData = reader.ReadBytes((int)ccbMatoSize);

            using var ccbReader = new BinaryReader(new MemoryStream(ccbMatoData));
            ccbReader.ReadBytes(0x08);
            var count = ccbReader.ReadBigEndianUInt32();
            ccbReader.ReadBytes(0x04);
            var offsets = new List<uint>();
            for (int i = 0; i < count; i++)
            {
                var offset = ccbReader.ReadBigEndianUInt32()+8;
                offsets.Add(offset);
            }


            var outputDir = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath));
            Directory.CreateDirectory(outputDir);

            for (int i = 0; i < offsets.Count - 1; i++)
            {
                var nextOffset = (i + 1 < offsets.Count) ? offsets[i + 1] : (uint)ccbReader.BaseStream.Length;
                var calculatedSize = nextOffset - offsets[i];
                if (calculatedSize <= 0x20)
                {
                    Console.WriteLine($"Warning: Calculated size for file {i:D4} is ({calculatedSize}). Skipping extraction.");
                    continue;
                }
                ccbReader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
                var size = ccbReader.ReadBigEndianUInt32();
                var data = ccbReader.ReadBytes((int)size);
                var outputFilePath = Path.Combine(outputDir, $"{i:D4}.cel");
                File.WriteAllBytes(outputFilePath, data);
                Console.WriteLine($"Extracted {outputFilePath} ({data.Length} bytes)");
            }
        }

        public static void ExtractArchiveV1(string filePath)
        {
            using var reader = new BinaryReader(File.OpenRead(filePath));
            reader.BaseStream.Seek(0x20, SeekOrigin.Begin);
            var containerOffset = reader.ReadBigEndianUInt32();

            reader.BaseStream.Seek(containerOffset, SeekOrigin.Begin);
            var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic != "mato")
            {
                throw new InvalidDataException($"Invalid MATO archive magic: {magic}");
            }
            var totalSize = reader.ReadBigEndianUInt32();
            var count = reader.ReadBigEndianUInt32();
            reader.ReadBytes(8); // Skip 8 bytes

            var offsets = new List<uint>();
            for (int i = 0; i < count; i++)
            {
                var offset = reader.ReadBigEndianUInt32() + containerOffset + 4;
                offsets.Add(offset);
            }

            var outputDir = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath));
            Directory.CreateDirectory(outputDir);

            for (int i = 0; i < offsets.Count-1; i++)
            {
                reader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
                var size = reader.ReadBigEndianUInt32();
                var data = reader.ReadBytes((int)size);
                var outputFilePath = Path.Combine(outputDir, $"{i:D4}.cel");
                File.WriteAllBytes(outputFilePath, data);
                Console.WriteLine($"Extracted {outputFilePath} ({data.Length} bytes)");
            }
        }
    }
}
