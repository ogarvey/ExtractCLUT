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
            uint op_count = 0;
            uint ext_offset = 0;
            uint ext_run = 0;

            var result = new List<byte>();

            while (result.Count < expectedSize)
            {
                if (_cursorPosition >= _boltFileData!.Length)
                {
                    Debugger.Break();
                    break;
                }
                var byteValue = ReadByte();
                op_count++;

                if ((byteValue & 0x80) != 0) // Check for the high bit
                {
                    if ((byteValue & 0x40) != 0) // extension in offset
                    {
                        ext_offset <<= 6;
                        ext_offset |= (uint)byteValue & 0x3F;
                    }
                    else if ((byteValue & 0x20) != 0) // extension in runlength
                    {
                        ext_run <<= 5;
                        ext_run |= (uint)byteValue & 0x1F;
                    }
                    else if ((byteValue & 0x10) != 0) // extension in both runlength and offset
                    {
                        ext_run <<= 2;
                        ext_offset <<= 2;

                        ext_offset |= (uint)(byteValue & 0b1100) >> 2;
                        ext_run |= (uint)(byteValue & 0b0011);
                    }
                    else // uncompressed
                    {
                        uint run_length = ((ext_run << 4) | (uint)(byteValue & 0xF)) + 1;
                        for (int i = 0; i < run_length; i++)
                        {
                            result.Add(ReadByte());
                        }
                        op_count = ext_offset = ext_run = 0;
                    }
                }
                else // lookup
                {
                    uint target_offset = (uint)(result.Count - 1 - ((ext_offset << 4) | (uint)(byteValue & 0xF)));
                    uint run_length = ((ext_run << 3) | (uint)(byteValue >> 4)) + op_count + 1;

                    if (result.Count <= target_offset)
                    {
                        return result;
                    }

                    for (int i = 0; i < run_length; i++)
                    {
                        result.Add(result[(int)target_offset + i]);
                    }
                    op_count = ext_offset = ext_run = 0;
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

        public static void ExtractBoltData(string inputFile, string outputFolder)
        {
            var data = File.ReadAllBytes(inputFile);
            _boltFileData = data;
            var headerOffsets = GetBoltOffsetData(data);

            foreach (var offset in headerOffsets)
            {
                ExtractBoltEntry(outputFolder, offset);
            }

            var dataOffsets = new List<int>();
            for (var i = 0; i < headerOffsets.Count; i++)
            {
                var offset = headerOffsets[i];
                var initialData = data.Skip((int)offset.Offset).Take(offset.FileCount * 0x10).ToArray();

                var boltOutputFolder = Path.Combine(outputFolder, "bolts1");
                var subBoltOutputFolder = Path.Combine(boltOutputFolder, "sub-bolts1");
                if (!Directory.Exists(boltOutputFolder))
                {
                    Directory.CreateDirectory(boltOutputFolder);
                }
                if (!Directory.Exists(subBoltOutputFolder))
                {
                    Directory.CreateDirectory(subBoltOutputFolder);
                }
                File.WriteAllBytes($"{boltOutputFolder}\\{offset.Offset}_Initial.bin", initialData);
                uint lastOffset = 0;
                for (int j = 0; j < initialData.Length; j += 16)
                {
                    var dataOffset = BitConverter.ToInt32(initialData.Skip(j + 8).Take(4).Reverse().ToArray(), 0);
                    if (dataOffset != 0)
                    {
                        dataOffsets.Add(dataOffset);
                    }
                    if (j + 16 >= initialData.Length)
                    {
                        lastOffset = (i + 1 == headerOffsets.Count) ? (uint)GetEndOfBoltData(data) : headerOffsets[i+1].Offset;
                    }
                }
                for (int j = 0; j < dataOffsets.Count; j++)
                {
                    var dataOffset = dataOffsets[j];
                    var dataLength = (j + 1 < dataOffsets.Count) ? dataOffsets[j + 1] - dataOffset : lastOffset - dataOffset;
                    var secondaryData = data.Skip(dataOffset).Take((int)dataLength).ToArray();
                    File.WriteAllBytes($"{subBoltOutputFolder}\\{offset.Offset}_{dataOffset}_Secondary.bin", secondaryData);
                }
                dataOffsets.Clear();
            }

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
