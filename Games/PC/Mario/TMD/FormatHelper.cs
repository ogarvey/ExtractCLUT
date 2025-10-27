using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Mario.TMD
{
    public static class FormatHelper
    {
        public static byte[] DecompressAnx(byte[] data)
        {
            var output = new List<byte>();

            using var dataReader = new BinaryReader(new MemoryStream(data));

            while (dataReader.BaseStream.Position < dataReader.BaseStream.Length)
            {
                var flag = dataReader.ReadByte();
                if (flag == 0x01)
                {
                    // Literal run
                    var value = dataReader.ReadByte();
                    var runLength = dataReader.ReadByte();
                    for (int i = 0; i < runLength; i++)
                    {
                        output.Add(value);
                    }
                }
                else
                {
                    output.Add(flag);
                }
            }
            return output.ToArray();
        }

        public static void ExtractResFile(string inputFile, string outputDir)
        {
            using var reader = new BinaryReader(File.OpenRead(inputFile));
            reader.BaseStream.Seek(0xA1, SeekOrigin.Begin);
            var fileCount = reader.ReadUInt16();
            var fileEntries = new List<(string Name, uint Offset, uint Size, ushort Flags)>();
            reader.BaseStream.Seek(0xB8, SeekOrigin.Begin);
            for (int i = 0; i < fileCount; i++)
            {
                var offset = reader.ReadUInt32();
                var size = reader.ReadUInt32();
                var nameBytes = reader.ReadBytes(0x11);
                var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                reader.ReadBytes(6);
                var flags = reader.ReadUInt16();
                fileEntries.Add((name, offset, size, flags));
            }

            foreach (var entry in fileEntries)
            {
                reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                var fileData = reader.ReadBytes((int)entry.Size);
                var outputPath = Path.Combine(outputDir, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllBytes(outputPath, fileData);
            }
        }

        public static void ExtractV2LFile(string inputFile, string outputDir)
        {
            using var reader = new BinaryReader(File.OpenRead(inputFile));
            reader.BaseStream.Seek(0x1F, SeekOrigin.Begin);
            var fileCount = reader.ReadUInt16();
            var fileEntries = new List<(string Name, uint Offset, uint Size, ushort Flags)>();
            reader.BaseStream.Seek(0x36, SeekOrigin.Begin);
            for (int i = 0; i < fileCount; i++)
            {
                var offset = reader.ReadUInt32();
                var size = reader.ReadUInt32();
                var nameBytes = reader.ReadBytes(0x11);
                var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                var flags = reader.ReadUInt16();
                fileEntries.Add((name, offset, size, flags));
            }

            foreach (var entry in fileEntries)
            {
                reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                var fileData = reader.ReadBytes((int)entry.Size);
                var outputPath = Path.Combine(outputDir, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllBytes(outputPath, fileData);
            }
        }
    }
}
