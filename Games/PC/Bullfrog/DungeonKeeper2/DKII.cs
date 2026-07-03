using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtractCLUT.Games.PC.Bullfrog.DungeonKeeper2
{
    /// <summary>
    /// Dungeon Keeper 2 WAD archive reader / extractor.
    ///
    /// Format (verified against DKII.EXE and the GOG retail data files, see DKII_Formats.md):
    ///   Header (0x58 bytes):
    ///     0x00  char[4]  magic "DWFB"
    ///     0x04  u32      version (2)
    ///     0x08  u8[0x40] unknown (zero)
    ///     0x48  u32      fileCount
    ///     0x4C  u32      nameBlobOffset (absolute)
    ///     0x50  u32      nameBlobSize (blob runs to EOF)
    ///     0x54  u32      unknown
    ///   Directory (fileCount x 0x28 bytes, at 0x58):
    ///     u32 unknown1, u32 nameOffset (absolute), u32 nameSize (incl. NUL),
    ///     u32 dataOffset (absolute), u32 compressedSize, u32 type (0 = stored, 4 = compressed),
    ///     u32 uncompressedSize, u32[3] unknown
    ///   Compressed payloads use an LZ77 variant, see <see cref="Decompress"/>.
    /// </summary>
    public static class DKII
    {
        private const uint WadMagic = 0x42465744; // "DWFB"
        private const int HeaderSize = 0x58;
        private const int EntrySize = 0x28;

        public sealed class WadEntry
        {
            public required string Name { get; init; }
            public uint DataOffset { get; init; }
            public uint CompressedSize { get; init; }
            public uint UncompressedSize { get; init; }
            public uint Type { get; init; }
            public bool IsCompressed => (Type & 4) != 0;
        }

        #region WAD parsing

        /// <summary>Parses the directory of a DK2 WAD file.</summary>
        public static List<WadEntry> ReadWadDirectory(byte[] wad)
        {
            if (wad.Length < HeaderSize)
                throw new InvalidDataException("File too small to be a DK2 WAD.");

            if (BitConverter.ToUInt32(wad, 0) != WadMagic)
                throw new InvalidDataException("Bad magic, expected 'DWFB'.");

            uint version = BitConverter.ToUInt32(wad, 4);
            if (version > 2)
                throw new InvalidDataException($"Unsupported WAD version {version} (expected <= 2).");

            uint fileCount = BitConverter.ToUInt32(wad, 0x48);

            var entries = new List<WadEntry>((int)fileCount);
            for (int i = 0; i < fileCount; i++)
            {
                int e = HeaderSize + i * EntrySize;
                uint nameOffset = BitConverter.ToUInt32(wad, e + 0x04);
                uint nameSize = BitConverter.ToUInt32(wad, e + 0x08);

                string name = Encoding.ASCII
                    .GetString(wad, (int)nameOffset, (int)nameSize)
                    .TrimEnd('\0');

                entries.Add(new WadEntry
                {
                    Name = name,
                    DataOffset = BitConverter.ToUInt32(wad, e + 0x0C),
                    CompressedSize = BitConverter.ToUInt32(wad, e + 0x10),
                    Type = BitConverter.ToUInt32(wad, e + 0x14),
                    UncompressedSize = BitConverter.ToUInt32(wad, e + 0x18),
                });
            }

            return entries;
        }

        /// <summary>Returns the (decompressed) payload of a single WAD entry.</summary>
        public static byte[] GetEntryData(byte[] wad, WadEntry entry)
        {
            var raw = new byte[entry.CompressedSize];
            Array.Copy(wad, entry.DataOffset, raw, 0, entry.CompressedSize);

            return entry.IsCompressed ? Decompress(raw) : raw;
        }

        #endregion

        #region Decompression

        /// <summary>
        /// DK2 WAD LZ77-variant decompressor (entry type 4).
        /// Stream layout: prologue byte (bit0 set = skip 3 extra bytes), one skipped byte,
        /// 24-bit BIG-endian decompressed size, then flag-driven literal runs and
        /// back-references into the output buffer.
        /// </summary>
        public static byte[] Decompress(byte[] src)
        {
            int i = 0, j = 0;

            if ((src[i++] & 1) != 0) i += 3;
            i++; // skip second prologue byte

            int decSize = (src[i] << 16) | (src[i + 1] << 8) | src[i + 2];
            i += 3;

            var dest = new byte[decSize];
            bool finished = false;

            while (!finished && i < src.Length)
            {
                int flag = src[i++];

                if ((flag & 0x80) == 0)
                {
                    // Short back-reference (1 extra byte)
                    int tmp = src[i++];
                    int counter = flag & 3;
                    while (counter-- != 0) dest[j++] = src[i++];

                    int k = j - ((flag & 0x60) << 3) - tmp - 1;
                    counter = ((flag >> 2) & 7) + 2;
                    do { dest[j++] = dest[k++]; } while (counter-- != 0);
                }
                else if ((flag & 0x40) == 0)
                {
                    // Medium back-reference (2 extra bytes)
                    int tmp = src[i++];
                    int tmp2 = src[i++];
                    int counter = tmp >> 6;
                    while (counter-- != 0) dest[j++] = src[i++];

                    int k = j - ((tmp & 0x3F) << 8) - tmp2 - 1;
                    counter = (flag & 0x3F) + 3;
                    do { dest[j++] = dest[k++]; } while (counter-- != 0);
                }
                else if ((flag & 0x20) == 0)
                {
                    // Long back-reference (3 extra bytes)
                    int t1 = src[i++];
                    int t2 = src[i++];
                    int t3 = src[i++];
                    int counter = flag & 3;
                    while (counter-- != 0) dest[j++] = src[i++];

                    int k = j - ((flag & 0x10) << 12) - (t1 << 8) - t2 - 1;
                    counter = t3 + ((flag & 0x0C) << 6) + 4;
                    do { dest[j++] = dest[k++]; } while (counter-- != 0);
                }
                else
                {
                    // Literal run / terminator
                    int counter = (flag & 0x1F) * 4 + 4;
                    if (counter > 0x70)
                    {
                        finished = true;
                        counter = flag & 3;
                    }
                    while (counter-- != 0) dest[j++] = src[i++];
                }
            }

            if (j != decSize)
                Console.WriteLine($"  Warning: decompressed {j} bytes, expected {decSize}.");

            return dest;
        }

        #endregion

        #region Extraction

        /// <summary>
        /// Extracts every file of a WAD archive to <paramref name="outputDir"/>,
        /// preserving any relative paths embedded in the entry names.
        /// </summary>
        public static void ExtractWad(string wadPath, string outputDir)
        {
            Console.WriteLine($"Extracting {Path.GetFileName(wadPath)}...");
            var wad = File.ReadAllBytes(wadPath);
            var entries = ReadWadDirectory(wad);
            Console.WriteLine($"  {entries.Count} entries.");

            int failures = 0;
            foreach (var entry in entries)
            {
                // Entry names can contain relative sub-paths ('\'); sanitize while keeping them.
                var relative = entry.Name.Replace('/', '\\').TrimStart('\\');
                if (relative.Contains(".."))
                {
                    Console.WriteLine($"  Skipping suspicious entry name: {entry.Name}");
                    continue;
                }

                var target = Path.Combine(outputDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                try
                {
                    File.WriteAllBytes(target, GetEntryData(wad, entry));
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.WriteLine($"  FAILED {entry.Name}: {ex.Message}");
                }
            }

            Console.WriteLine($"  Done. {entries.Count - failures} extracted, {failures} failed.");
        }

        /// <summary>Extracts all *.wad archives found in a DK2 data folder.</summary>
        public static void ExtractAllWads(string dataDir, string outputDir)
        {
            foreach (var wadPath in Directory.EnumerateFiles(dataDir, "*.wad", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }))
            {
                var wadName = Path.GetFileNameWithoutExtension(wadPath);
                ExtractWad(wadPath, Path.Combine(outputDir, wadName));
            }
        }

        #endregion
    }
}
