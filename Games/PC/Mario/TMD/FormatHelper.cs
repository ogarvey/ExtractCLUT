using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Mario.TMD
{
    public static class FormatHelper
    {
        public class AnxFile
        {
            public short FrameCount { get; set; }
            public byte CompressionType { get; set; }
            public ushort[] ParameterTable { get; set; } // 256 entries for type 0x03
            public AnxFrame[] Frames { get; set; }
        }

        public class AnxFrame
        {
            public byte CompressionType { get; set; }
            public short Width { get; set; }
            public short Height { get; set; }
            public byte[] CompressedData { get; set; }
        }

        public static AnxFile LoadAnxFile(byte[] fileData)
        {
            using var reader = new BinaryReader(new MemoryStream(fileData));

            var anx = new AnxFile();
            anx.FrameCount = reader.ReadInt16();
            if (anx.FrameCount < 0)
                anx.FrameCount = (short)Math.Abs(anx.FrameCount);

            anx.CompressionType = reader.ReadByte();
            byte constantByte = reader.ReadByte(); // Always 0x20

            uint unknown1 = reader.ReadUInt32();
            uint unknown2 = reader.ReadUInt32();

            // Read parameter table (256 ushorts = 0x200 bytes)
            if (anx.CompressionType == 0x03)
            {
                anx.ParameterTable = new ushort[256];
                for (int i = 0; i < 256; i++)
                {
                    anx.ParameterTable[i] = reader.ReadUInt16();
                }
            }

            // Read frame offsets and sizes
            uint[] frameOffsets = new uint[anx.FrameCount];
            uint[] frameSizes = new uint[anx.FrameCount];

            for (int i = 0; i < anx.FrameCount; i++)
                frameOffsets[i] = reader.ReadUInt32();

            for (int i = 0; i < anx.FrameCount; i++)
                frameSizes[i] = reader.ReadUInt32();

            // Read frames
            anx.Frames = new AnxFrame[anx.FrameCount];
            for (int i = 0; i < anx.FrameCount; i++)
            {
                reader.BaseStream.Position = frameOffsets[i];
                anx.Frames[i] = LoadAnxFrame(reader, (int)frameSizes[i]);
            }

            return anx;
        }

        public static AnxFrame LoadAnxFrame(BinaryReader reader, int size)
        {
            var frame = new AnxFrame();
            frame.CompressionType = reader.ReadByte();
            byte constantByte = reader.ReadByte(); // 0x20

            reader.BaseStream.Position += 0x0E; // Skip to width/height

            frame.Width = reader.ReadInt16();
            frame.Height = reader.ReadInt16();

            reader.BaseStream.Position += 0x10; // Skip padding

            short width2 = reader.ReadInt16();
            short height2 = reader.ReadInt16();

            // Read compressed data
            long dataStart = reader.BaseStream.Position;
            int dataSize = size - (int)(dataStart - (reader.BaseStream.Position - size));
            frame.CompressedData = reader.ReadBytes(dataSize);

            return frame;
        }

        public static byte[] DecompressAnxFrame(AnxFrame frame, ushort[] paramTable)
        {
            if (frame.CompressionType == 0x01)
            {
                return DecompressAnx(frame.CompressedData);
            }
            else if (frame.CompressionType == 0x03)
            {
                int paddedWidth = (frame.Width + 3) & ~3;
                uint decompressedSize = (uint)(paddedWidth * frame.Height);
                return DecompressAnxType3(frame.CompressedData, decompressedSize, paramTable);
            }

            throw new NotSupportedException($"Compression type {frame.CompressionType:X2} not supported");
        }
        public static byte[] DecompressAnxType3(byte[] compressedData, uint decompressedSize, ushort[] paramTable)
        {
            var output = new List<byte>();
            var lookupTable = new byte[256, 100];

            // Initialize the reverse lookup table
            InitializeReverseLookupTable(lookupTable, paramTable);

            int pos = 0;
            byte escapeByte = (byte)(paramTable[0] & 0xFF);

            while (pos < compressedData.Length && output.Count < decompressedSize)
            {
                byte code = compressedData[pos];
                pos++;

                // Check if this is an encoded run
                bool decoded = false;

                // Try to find the code in the lookup table
                for (int byteValue = 0; byteValue < 256 && !decoded; byteValue++)
                {
                    for (int runLength = 3; runLength < 100 && !decoded; runLength++)
                    {
                        if (lookupTable[byteValue, runLength] == code)
                        {
                            // Found it! Output the run
                            for (int i = 0; i < runLength; i++)
                            {
                                output.Add((byte)byteValue);
                            }
                            decoded = true;
                        }
                    }
                }

                if (!decoded)
                {
                    // Check if this is a 2-byte encoding
                    if (pos < compressedData.Length)
                    {
                        byte nextByte = compressedData[pos];

                        // Check if code is in table[escapeByte][runLength] format
                        for (int runLength = 3; runLength < 100 && !decoded; runLength++)
                        {
                            if (lookupTable[escapeByte, runLength] == code)
                            {
                                // This is: [code from table[escapeByte][runLength], actualByteValue]
                                for (int i = 0; i < runLength; i++)
                                {
                                    output.Add(nextByte);
                                }
                                pos++;
                                decoded = true;
                            }
                        }

                        // Check if this is table[byteValue][highByte] format
                        if (!decoded)
                        {
                            for (int byteValue = 0; byteValue < 256 && !decoded; byteValue++)
                            {
                                byte highByte = (byte)((paramTable[0] >> 8) & 0xFF);
                                if (lookupTable[byteValue, highByte] == code)
                                {
                                    // This is: [code from table[byteValue][highByte], runLength]
                                    int runLength = nextByte;
                                    for (int i = 0; i < runLength; i++)
                                    {
                                        output.Add((byte)byteValue);
                                    }
                                    pos++;
                                    decoded = true;
                                }
                            }
                        }
                    }
                }

                if (!decoded)
                {
                    // Check for 3-byte escape sequence: [escapeByte, runLength, byteValue]
                    if (code == escapeByte && pos + 1 < compressedData.Length)
                    {
                        byte runLength = compressedData[pos];
                        byte byteValue = compressedData[pos + 1];
                        pos += 2;

                        for (int i = 0; i < runLength; i++)
                        {
                            output.Add(byteValue);
                        }
                        decoded = true;
                    }
                }

                if (!decoded)
                {
                    // It's a literal byte
                    output.Add(code);
                }
            }

            return output.ToArray();
        }

        private static void InitializeReverseLookupTable(byte[,] table, ushort[] paramTable)
        {
            // Clear the table
            Array.Clear(table, 0, table.Length);

            ushort baseValue = paramTable[0];

            // Build the lookup table from the parameter table
            for (int i = 0; i < 256; i++)
            {
                if (paramTable[i] != baseValue)
                {
                    byte lowByte = (byte)(paramTable[i] & 0xFF);
                    byte highByte = (byte)((paramTable[i] >> 8) & 0xFF);

                    table[lowByte, highByte] = (byte)i;
                }
            }
        }
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
