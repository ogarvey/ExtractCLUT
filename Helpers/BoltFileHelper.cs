using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Helpers
{
    public static class BoltFileHelper
    {
        private static byte[]? _boltFileData;
        private static uint _cursorPosition;
        public const int FLAG_UNCOMPRESSED = 0x8000000;
        public static int GetEndOfBoltData(byte[] data) => BitConverter
            .ToInt32(data.Skip(0xC).Take(4).Reverse().ToArray(), 0);

        public static int GetStartOfBoltData(byte[] data) => BitConverter
            .ToInt32(data.Skip(0x18).Take(4).Reverse().ToArray(), 0);

        public static void SetCurrentPosition(uint position) => _cursorPosition = position;
        public static byte ReadByte() => _boltFileData![_cursorPosition++];
        public static List<int> GetBoltOffsets(byte[] data)
        {
            var offsets = new List<int>();

            var value = GetStartOfBoltData(data);
            offsets.Add(value);
            var index = 0x20;
            while (index < offsets[0])
            {
                value = BitConverter.ToInt32(data.Skip(index + 8).Take(4).Reverse().ToArray(), 0);
                offsets.Add(value);
                index += 16;
            }

            return offsets;
        }

        public static List<BoltOffset> GetBoltOffsetData(byte[] data)
        {
            _boltFileData = data;
            var offsets = new List<BoltOffset>();

            var dataStart = GetStartOfBoltData(data);

            var record = data.Skip(0x10).Take(0x10).ToArray();

            var offsetData = new BoltOffset(record);
            offsetData.Entries = PopulateEntries(offsetData);
            offsets.Add(offsetData);
            var index = 0x20;
            while (index < dataStart)
            {
                record = data.Skip(index).Take(0x10).ToArray();
                offsetData = new BoltOffset(record);
                offsetData.Entries = PopulateEntries(offsetData);
                offsets.Add(offsetData);
                index += 16;
            }

            return offsets;
        }

        private static List<BoltOffset> PopulateEntries(BoltOffset offsetData)
        {
            var entries = new List<BoltOffset>();
            // Get the entries
            if (offsetData.FileCount > 0)
            {
                for (var i = 0; i < offsetData.FileCount; i++)
                {
                    var entryRecord = new byte[0x10];
                    for (var j = 0; j < 0x10; j++)
                    {
                        entryRecord[j] = _boltFileData![offsetData.Offset + (i * 0x10) + j];
                    }
                    var entry = new BoltOffset(entryRecord);
                    entries.Add(entry);
                }
            }
            return entries;
        }

        public static void ExtractBoltFolder(string outputPath, List<BoltOffset> data)
        {
            foreach (var entry in data)
            {
                ExtractBoltFile(outputPath, entry);
            }
        }

        public static void ExtractBoltFile(string outputPath, BoltOffset data)
        {
            var result = new List<byte>();

            if (!data.IsCompressed)
            {
                SetCurrentPosition(data.Offset);
                var byteValue = _boltFileData[_cursorPosition];
                result.AddRange(Enumerable.Repeat(byteValue, (int)data.UncompressedSize));
            }
            else
            {
                // Decompress
                SetCurrentPosition(data.Offset);
                result = Decompress(data.UncompressedSize);
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            var outputFilePath = Path.Combine(outputPath, $"{data.NameHash}.bin");
            File.WriteAllBytes(outputFilePath, result.ToArray());
        }

        public static List<byte> Decompress(uint expectedSize)
        {
            var result = new List<byte>();
            uint run_length;
            uint relOffset;
            byte b;

            while (result.Count < expectedSize)
            {
                if (_cursorPosition >= _boltFileData!.Length)
                {
                    Debugger.Break();
                    break;
                }
                var byteValue = ReadByte();

                switch (byteValue >> 4)
                {
                    case 0x0:
                    case 0x1:
                        for (uint i = 0; i < (byteValue & 0x1F) + 1; ++i)
                        {
                            result.Add(ReadByte());
                        }
                        break;
                    case 0x2:
                        run_length = (uint)(byteValue & 0xF) + 1;
                        result.AddRange(Enumerable.Repeat((byte)0, (int)run_length));
                        break;
                    case 0x3:
                        b = ReadByte();
                        run_length = (uint)(byteValue & 0xF) + 3;
                        result.AddRange(Enumerable.Repeat(b, (int)run_length));
                        break;
                    case 0x4:
                    case 0x5:
                    case 0x6:
                    case 0x7:
                        run_length = (uint)(byteValue & 0x7) + 2;
                        relOffset = (uint)((byteValue >> 3) & 0x7) + 1;
                        for (uint i = 0; i < run_length; i++)
                        {
                            b = result[result.Count - (int)relOffset];
                            result.Add(b);
                        }
                        break;
                    case 0x8:
                        b = ReadByte();
                        run_length = (uint)(b & 0x3f) + 3;
                        relOffset = (uint)((((byteValue << 8) | b) >> 6) & 0x3f) + 1;
                        for (uint i = 0; i < run_length; i++)
                        {
                            b = result[result.Count - (int)relOffset];
                            result.Add(b);
                        }
                        break;
                    case 0x9:
                        b = ReadByte();
                        run_length = (uint)(b & 0x3) + 3;
                        relOffset = (uint)((((byteValue << 8) | b) >> 2) & 0x3ff) + 1;
                        for (uint i = 0; i < run_length; i++)
                        {
                            b = result[result.Count - (int)relOffset];
                            result.Add(b);
                        }
                        break;
                    case 0xA:
                        b = ReadByte();
                        run_length = (uint)b << 0x8;
                        b = ReadByte();
                        run_length |= b;

                        relOffset = (uint)(byteValue & 0xf) + 1;

                        for (uint i = 0; i < run_length; i++)
                        {
                            b = result[result.Count - (int)relOffset];
                            result.Add(b);
                        }
                        break;
                    case 0xB:
                        b = ReadByte();
                        run_length = (uint)(b & 0x3) << 0x8;
                        relOffset = (uint)(((((b & 0xff) << 8) | (byteValue << 16)) >> 10) & 0x3ff) + 1;

                        b = ReadByte();
                        run_length |= b;
                        run_length += 0x4;

                        for (uint i = 0; i < run_length; i++)
                        {
                            b = result[result.Count - (int)relOffset];
                            result.Add(b);
                        }
                        break;
                    case 0xC:
                    case 0xD:
                        run_length = (uint)(byteValue & 0x3) + 2;
                        relOffset = (uint)(byteValue >> 2) & 7;
                        for (uint i = 0; i < run_length; i++)
                        {
                            relOffset++;
                            b = result[result.Count - (int)relOffset];
                            result.Add(b);
                            relOffset++;
                        }
                        break;
                    case 0xE:
                        b = ReadByte();
                        run_length = (uint)(b & 0x3f) + 3;
                        relOffset = (uint)(((byteValue << 8) | b) >> 6) & 0x3f;
                        for (uint i = 0; i < run_length; i++)
                        {
                            relOffset++;
                            b = result[result.Count - (int)relOffset];
                            result.Add(b);
                            relOffset++;
                        }
                        break;
                    case 0xF:
                        b = ReadByte();
                        run_length = (uint)((b & 0x3) << 0x8);
                        relOffset = (uint)((((b & 0xff) << 8) | (byteValue << 16)) >> 10) & 0x3ff;

                        b = ReadByte();
                        run_length |= b;
                        run_length += 0x4;

                        for (uint i = 0; i < run_length; i++)
                        {
                            relOffset++;
                            b = result[result.Count - (int)relOffset];
                            result.Add(b);
                            relOffset++;
                        }
                        break;
                }

            }

            return result;
        }

        public static void ExtractBoltEntry(string outputPath, BoltOffset data)
        {
            if (data.IsFolder)
            {
                var folderPath = Path.Combine(outputPath, data.NameHash.ToString());
                ExtractBoltFolder(folderPath, data.Entries);
            }
            else
            {
                ExtractBoltFile(outputPath, data);
            }
        }

        public static void ParseBoltFile(string file)
        {
            var sb = new StringBuilder();
            var data = File.ReadAllBytes(file);
            var filename = Path.GetFileNameWithoutExtension(file);
            var offsetData = GetBoltOffsetData(data);

            sb.AppendLine($"Parsing BOLT Data for {filename}");
            sb.AppendLine("==========================================");
            sb.AppendLine();

            foreach (var (offset, index) in offsetData.WithIndex())
            {
                sb.AppendLine("Folder Data:");
                sb.AppendLine("==========================================");
                sb.AppendLine($"Offset: {offset.Offset:x4} - Flags: {offset.Flags:x4} - IsCompressed: {offset.IsCompressed}");
                sb.AppendLine($"UncompressedSize: {offset.UncompressedSize:x4} - NameHash: {offset.NameHash:x4}");
                sb.AppendLine("==========================================");
                sb.AppendLine();
                if (offset.IsFolder) sb.AppendLine("File Data:");
                if (offset.IsFolder) sb.AppendLine("==========================================");
                if (offset.IsFolder)
                {
                    foreach (var entry in offset.Entries)
                    {
                        sb.AppendLine($"Offset: {entry.Offset:x4} - Flags: {entry.Flags:x4} - IsCompressed: {entry.IsCompressed}");
                        sb.AppendLine($"UncompressedSize: {entry.UncompressedSize:x4} - NameHash: {entry.NameHash:x4}");
                        sb.AppendLine();
                    }
                }
                if (offset.IsFolder) sb.AppendLine();
                if (offset.IsFolder) sb.AppendLine("==========================================");
            }

            var outputPath = Path.Combine(Path.GetDirectoryName(file), $"{filename}_parsedData.txt");

            File.WriteAllText(outputPath, sb.ToString());
        }
        public static void ExtractBoltData(string inputFile, string outputFolder)
        {
            var data = File.ReadAllBytes(inputFile);
            var filename = Path.GetFileNameWithoutExtension(inputFile);
            _boltFileData = data;
            var headerOffsets = GetBoltOffsetData(data);

            foreach (var offset in headerOffsets)
            {
                ExtractBoltEntry(outputFolder, offset);
            }

            // var dataOffsets = new List<int>();
            // for (var i = 0; i < headerOffsets.Count; i++)
            // {
            //     var offset = headerOffsets[i];
            //     var initialData = data.Skip((int)offset.Offset).Take(offset.FileCount * 0x10).ToArray();

            //     var boltOutputFolder = Path.Combine(outputFolder, $"{filename}-bolts");
            //     var subBoltOutputFolder = Path.Combine(boltOutputFolder, "sub-bolts");
            //     if (!Directory.Exists(boltOutputFolder))
            //     {
            //         Directory.CreateDirectory(boltOutputFolder);
            //     }
            //     if (!Directory.Exists(subBoltOutputFolder))
            //     {
            //         Directory.CreateDirectory(subBoltOutputFolder);
            //     }
            //     File.WriteAllBytes($"{boltOutputFolder}\\{offset.Offset}_Initial.bin", initialData);
            //     uint lastOffset = 0;
            //     for (int j = 0; j < initialData.Length; j += 16)
            //     {
            //         var dataOffset = BitConverter.ToInt32(initialData.Skip(j + 8).Take(4).Reverse().ToArray(), 0);
            //         if (dataOffset != 0)
            //         {
            //             dataOffsets.Add(dataOffset);
            //         }
            //         if (j + 16 >= initialData.Length)
            //         {
            //             lastOffset = (i + 1 == headerOffsets.Count) ? (uint)GetEndOfBoltData(data) : headerOffsets[i + 1].Offset;
            //         }
            //     }
            //     for (int j = 0; j < dataOffsets.Count; j++)
            //     {
            //         var dataOffset = dataOffsets[j];
            //         var dataLength = (j + 1 < dataOffsets.Count) ? dataOffsets[j + 1] - dataOffset : lastOffset - dataOffset;
            //         var secondaryData = data.Skip(dataOffset).Take((int)dataLength).ToArray();
            //         File.WriteAllBytes($"{subBoltOutputFolder}\\{offset.Offset}_{dataOffset}_Secondary.bin", secondaryData);
            //     }
            //     dataOffsets.Clear();
            // }

        }
    }

    public class BoltOffset
    {
        public uint Offset { get; }
        public uint NameHash { get; }
        public uint UncompressedSize { get; }
        public uint Flags { get; }
        public List<BoltOffset> Entries { get; set; }
        public int FileCount => (int)(Flags & 0xFF);
        public bool IsFolder => NameHash == 0;
        public bool IsCompressed => (Flags & BoltFileHelper.FLAG_UNCOMPRESSED) == 0;

        public BoltOffset(byte[] data)
        {
            Flags = BitConverter.ToUInt32(data.Take(4).Reverse().ToArray(), 0);
            UncompressedSize = BitConverter.ToUInt32(data.Skip(0x4).Take(4).Reverse().ToArray(), 0);
            Offset = BitConverter.ToUInt32(data.Skip(0x8).Take(4).Reverse().ToArray(), 0);
            NameHash = BitConverter.ToUInt32(data.Skip(0xC).Take(4).Reverse().ToArray(), 0);
        }

        public override string ToString()
        {
            return $"Offset: {Offset}, Entries: {Entries}, NameHash: {NameHash}, UncompressedSize: {UncompressedSize}, Flags: {Flags}";
        }
    }
}
