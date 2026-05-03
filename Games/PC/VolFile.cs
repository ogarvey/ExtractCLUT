using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class VolFile
    {
        public class FileEntry
        {
            public uint Flags;
            public uint Offset;
            public uint NameOffset;
            public uint CompressedSize;
            public uint Attributes;
            public long Timestamp;
            public string Name;
        }

        public static void Unpack(string volPath, string outputDir)
        {
            using (var fs = new FileStream(volPath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                // 1. Read Footer
                fs.Seek(-9, SeekOrigin.End);
                byte headerSize = br.ReadByte();
                byte[] magic = br.ReadBytes(8);

                // Verify Magic (Optional, but good for debugging)
                // User says it ends with VOLF. Let's print it.
                Console.WriteLine($"Magic: {BitConverter.ToString(magic)} ({Encoding.ASCII.GetString(magic)})");

                // 2. Read Header
                long headerOffset = fs.Length - headerSize;
                fs.Seek(headerOffset, SeekOrigin.Begin);
                uint crc = br.ReadUInt32();
                byte[] headerData = br.ReadBytes(headerSize - 4);

                // Debug Header
                Console.WriteLine("Header Data (Int32s):");
                for (int i = 0; i < headerData.Length; i += 4)
                {
                    if (i + 4 <= headerData.Length)
                        Console.WriteLine($"  [{i}]: {BitConverter.ToUInt32(headerData, i)} (0x{BitConverter.ToUInt32(headerData, i):X})");
                }

                // 3. Get Directory Offset
                // Header Structure:
                // [0..3]   CRC
                // [4..7]   Volume Size (matches FileSize - FooterSize)
                // ...
                // [52..55] Directory Info (Offset relative to end?)

                // headerData starts at offset 4 of the header (after CRC).
                // So headerData[0] is Header[4].
                // headerData[48] is Header[52].

                uint volumeSize = BitConverter.ToUInt32(headerData, 0); // Header[4]
                uint valAt52 = BitConverter.ToUInt32(headerData, 48);   // Header[52]

                // Formula: Offset = VolumeSize - (Header[52] & 0xFFFFFF)
                // Assuming VolumeStart is 0.
                long directoryOffset = volumeSize - (valAt52 & 0xFFFFFF);

                Console.WriteLine($"Volume Size (Header[4]): {volumeSize}");
                Console.WriteLine($"ValAt52 (Header[52]): {valAt52:X}");
                Console.WriteLine($"Calculated Directory Offset: {directoryOffset}");

                // 4. Read and Process Directory Block
                // We don't know the exact size, but it should be from Offset to VolumeSize.
                uint directorySize = (uint)(volumeSize - directoryOffset);
                byte[] directoryData = ReadBlock(fs, directoryOffset, directorySize); using (var dirMs = new MemoryStream(directoryData))
                using (var dirBr = new BinaryReader(dirMs))
                {
                    // String Table
                    uint stringTableSize = dirBr.ReadUInt32();
                    byte[] stringTable = dirBr.ReadBytes((int)stringTableSize);

                    // File Table
                    uint fileTableSize = dirBr.ReadUInt32();
                    int numEntries = (int)(fileTableSize / 36); // 36 bytes per entry
                    List<FileEntry> entries = new List<FileEntry>();

                    for (int i = 0; i < numEntries; i++)
                    {
                        FileEntry entry = new FileEntry();
                        entry.Flags = dirBr.ReadUInt32();
                        entry.Offset = dirBr.ReadUInt32();
                        entry.NameOffset = dirBr.ReadUInt32();
                        dirBr.ReadUInt32(); // Unknown 0xC
                        dirBr.ReadUInt32(); // Unknown 0x10
                        entry.CompressedSize = dirBr.ReadUInt32(); // 0x14
                        entry.Attributes = dirBr.ReadUInt32(); // 0x18
                        uint timeLow = dirBr.ReadUInt32(); // 0x1C
                        uint timeHigh = dirBr.ReadUInt32(); // 0x20
                        entry.Timestamp = ((long)timeHigh << 32) | timeLow;

                        // Parse Name
                        int nameIdx = (int)entry.NameOffset;
                        if (nameIdx < stringTable.Length)
                        {
                            int endIdx = nameIdx;
                            while (endIdx < stringTable.Length && stringTable[endIdx] != 0)
                                endIdx++;
                            entry.Name = Encoding.ASCII.GetString(stringTable, nameIdx, endIdx - nameIdx);
                        }

                        entries.Add(entry);
                    }

                    Console.WriteLine($"Found {entries.Count} entries.");

                    // 5. Extract Files
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        string destPath = Path.Combine(outputDir, entry.Name);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                        byte[] fileData = ReadBlock(fs, entry.Offset, entry.CompressedSize);
                        File.WriteAllBytes(destPath, fileData);
                        Console.WriteLine($"Extracted: {entry.Name}");
                    }
                }
            }
        }

        private static byte[] ReadBlock(Stream stream, long offset, uint size)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            // Try to read the VF header first
            byte[] header = new byte[12];
            int bytesRead = stream.Read(header, 0, 12);

            if (bytesRead == 12 &&
                header[0] == 0x56 && header[1] == 0x46 && header[2] == 0x00 && header[3] == 0x80)
            {
                // Found VF header
                uint val1 = BitConverter.ToUInt32(header, 4);
                uint val2 = BitConverter.ToUInt32(header, 8);

                // Assuming val1 is the compressed payload size
                uint payloadSize = val1;

                // Sanity check: payloadSize shouldn't exceed the remaining stream length
                if (payloadSize > stream.Length - stream.Position)
                {
                    Console.WriteLine($"Warning: VF Header payload size {payloadSize} seems too large. Stream pos: {stream.Position}, Length: {stream.Length}");
                }

                byte[] payload = new byte[payloadSize];
                stream.Read(payload, 0, (int)payloadSize);

                try
                {
                    using (var inputMs = new MemoryStream(payload))
                    using (var zlibStream = new ZLibStream(inputMs, CompressionMode.Decompress))
                    using (var outputMs = new MemoryStream())
                    {
                        zlibStream.CopyTo(outputMs);
                        return outputMs.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Decompression failed: {ex.Message}. Returning raw payload.");
                    return payload;
                }
            }
            else
            {
                // No VF header, read 'size' bytes as requested
                stream.Seek(offset, SeekOrigin.Begin);
                byte[] buffer = new byte[size];
                stream.Read(buffer, 0, (int)size);
                return buffer;
            }
        }
    }

}
